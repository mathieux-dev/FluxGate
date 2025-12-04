using FluxPay.Core.Entities;
using FluxPay.Core.Providers;
using FluxPay.Core.Services;
using FluxPay.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;

namespace FluxPay.Infrastructure.Services;

public class WebhookService : IWebhookService
{
    private readonly FluxPayDbContext _dbContext;
    private readonly IProviderFactory _providerFactory;
    private readonly INonceStore _nonceStore;
    private readonly IHmacSignatureService _hmacService;
    private readonly IEncryptionService _encryptionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuditService _auditService;

    public WebhookService(
        FluxPayDbContext dbContext,
        IProviderFactory providerFactory,
        INonceStore nonceStore,
        IHmacSignatureService hmacService,
        IEncryptionService encryptionService,
        IHttpClientFactory httpClientFactory,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _providerFactory = providerFactory;
        _nonceStore = nonceStore;
        _hmacService = hmacService;
        _encryptionService = encryptionService;
        _httpClientFactory = httpClientFactory;
        _auditService = auditService;
    }

    public async Task<bool> ValidateProviderWebhookAsync(string provider, string signature, string payload, long timestamp, string nonce)
    {
        var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timestampDiff = Math.Abs(currentTimestamp - timestamp);
        
        if (timestampDiff > 120)
        {
            await _auditService.LogAsync(new AuditEntry
            {
                MerchantId = null,
                Actor = $"provider:{provider}",
                Action = "webhook.rejected.timestamp_skew",
                ResourceType = "Webhook",
                ResourceId = null,
                Changes = new { provider, timestampDiff, maxAllowed = 120 }
            });
            return false;
        }

        var isNonceUnique = await _nonceStore.IsNonceUniqueAsync($"provider:{provider}", nonce);
        if (!isNonceUnique)
        {
            await _auditService.LogAsync(new AuditEntry
            {
                MerchantId = null,
                Actor = $"provider:{provider}",
                Action = "webhook.rejected.nonce_reused",
                ResourceType = "Webhook",
                ResourceId = null,
                Changes = new { provider, nonce }
            });
            return false;
        }

        var providerAdapter = _providerFactory.GetProvider(provider);
        var isSignatureValid = await providerAdapter.ValidateWebhookSignatureAsync(signature, payload, timestamp);
        
        if (!isSignatureValid)
        {
            await _auditService.LogAsync(new AuditEntry
            {
                MerchantId = null,
                Actor = $"provider:{provider}",
                Action = "webhook.rejected.invalid_signature",
                ResourceType = "Webhook",
                ResourceId = null,
                Changes = new { provider }
            });
            return false;
        }

        await _nonceStore.StoreNonceAsync($"provider:{provider}", nonce, TimeSpan.FromHours(24));
        
        return true;
    }

    public async Task ProcessProviderWebhookAsync(ProviderWebhookEvent webhookEvent)
    {
        var webhookReceived = new WebhookReceived
        {
            Id = Guid.NewGuid(),
            Provider = webhookEvent.Provider,
            EventType = webhookEvent.EventType,
            Payload = webhookEvent.Payload,
            Processed = false,
            ReceivedAt = DateTime.UtcNow
        };

        _dbContext.WebhooksReceived.Add(webhookReceived);
        await _dbContext.SaveChangesAsync();

        try
        {
            if (!string.IsNullOrEmpty(webhookEvent.ProviderPaymentId))
            {
                var payment = await _dbContext.Payments
                    .FirstOrDefaultAsync(p => p.ProviderPaymentId == webhookEvent.ProviderPaymentId);

                if (payment != null)
                {
                    var oldStatus = payment.Status;
                    var newStatus = MapProviderStatusToPaymentStatus(webhookEvent.Status);

                    if (newStatus.HasValue && oldStatus != newStatus.Value)
                    {
                        payment.Status = newStatus.Value;
                        payment.UpdatedAt = DateTime.UtcNow;

                        await _auditService.LogAsync(new AuditEntry
                        {
                            MerchantId = payment.MerchantId,
                            Actor = $"provider:{webhookEvent.Provider}",
                            Action = "payment.status_changed",
                            ResourceType = "Payment",
                            ResourceId = payment.Id,
                            Changes = new { oldStatus = oldStatus.ToString(), newStatus = newStatus.Value.ToString() }
                        });

                        await _dbContext.SaveChangesAsync();

                        var merchantWebhookEvent = new WebhookEvent
                        {
                            EventType = $"payment.{newStatus.Value.ToString().ToLowerInvariant()}",
                            PaymentId = payment.Id,
                            PaymentStatus = newStatus.Value.ToString(),
                            AmountCents = payment.AmountCents,
                            Method = payment.Method.ToString(),
                            CreatedAt = payment.CreatedAt,
                            PaidAt = newStatus.Value == PaymentStatus.Paid ? DateTime.UtcNow : null,
                            Metadata = payment.Metadata != null ? JsonSerializer.Deserialize<Dictionary<string, string>>(payment.Metadata) : null
                        };

                        await SendMerchantWebhookAsync(payment.MerchantId, merchantWebhookEvent);
                    }
                }
            }

            webhookReceived.Processed = true;
            webhookReceived.ProcessedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            await _auditService.LogAsync(new AuditEntry
            {
                MerchantId = null,
                Actor = $"provider:{webhookEvent.Provider}",
                Action = "webhook.processing_failed",
                ResourceType = "Webhook",
                ResourceId = webhookReceived.Id,
                Changes = new { error = ex.Message }
            });
            throw;
        }
    }

    public async Task SendMerchantWebhookAsync(Guid merchantId, WebhookEvent webhookEvent)
    {
        var merchantWebhook = await _dbContext.MerchantWebhooks
            .FirstOrDefaultAsync(w => w.MerchantId == merchantId && w.Active);

        if (merchantWebhook == null)
        {
            return;
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = Guid.NewGuid().ToString();
        var traceId = Guid.NewGuid().ToString();

        var payload = JsonSerializer.Serialize(new
        {
            @event = webhookEvent.EventType,
            payment = new
            {
                id = webhookEvent.PaymentId,
                status = webhookEvent.PaymentStatus,
                amount_cents = webhookEvent.AmountCents,
                method = webhookEvent.Method,
                created_at = webhookEvent.CreatedAt,
                paid_at = webhookEvent.PaidAt
            },
            metadata = webhookEvent.Metadata
        });

        var webhookSecret = _encryptionService.Decrypt(merchantWebhook.SecretEncrypted);
        var message = $"{timestamp}.{nonce}.{payload}";
        var signature = _hmacService.ComputeSignature(webhookSecret, message);

        var webhookDelivery = new WebhookDelivery
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            PaymentId = webhookEvent.PaymentId,
            EventType = webhookEvent.EventType,
            Payload = payload,
            Status = WebhookDeliveryStatus.Pending,
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.WebhookDeliveries.Add(webhookDelivery);
        await _dbContext.SaveChangesAsync();

        try
        {
            var result = await DeliverWebhookAsync(merchantWebhook.EndpointUrl, payload, signature, timestamp, nonce, traceId);

            if (result.Success)
            {
                webhookDelivery.Status = WebhookDeliveryStatus.Success;
                webhookDelivery.AttemptCount = 1;
                merchantWebhook.LastSuccessAt = DateTime.UtcNow;
            }
            else
            {
                webhookDelivery.Status = WebhookDeliveryStatus.Failed;
                webhookDelivery.AttemptCount = 1;
                webhookDelivery.LastError = result.ErrorMessage;
                webhookDelivery.NextRetryAt = CalculateNextRetryTime(1);
            }

            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            webhookDelivery.Status = WebhookDeliveryStatus.Failed;
            webhookDelivery.AttemptCount = 1;
            webhookDelivery.LastError = ex.Message;
            webhookDelivery.NextRetryAt = CalculateNextRetryTime(1);
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<WebhookDeliveryResult> TestMerchantWebhookAsync(Guid merchantId, string endpointUrl)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = Guid.NewGuid().ToString();
        var traceId = Guid.NewGuid().ToString();

        var payload = JsonSerializer.Serialize(new
        {
            @event = "test.webhook",
            test = true,
            timestamp = DateTime.UtcNow
        });

        var merchantWebhook = await _dbContext.MerchantWebhooks
            .FirstOrDefaultAsync(w => w.MerchantId == merchantId && w.Active);

        string signature;
        if (merchantWebhook != null)
        {
            var webhookSecret = _encryptionService.Decrypt(merchantWebhook.SecretEncrypted);
            var message = $"{timestamp}.{nonce}.{payload}";
            signature = _hmacService.ComputeSignature(webhookSecret, message);
        }
        else
        {
            signature = "test_signature";
        }

        return await DeliverWebhookAsync(endpointUrl, payload, signature, timestamp, nonce, traceId);
    }

    private async Task<WebhookDeliveryResult> DeliverWebhookAsync(
        string endpointUrl, 
        string payload, 
        string signature, 
        long timestamp, 
        string nonce, 
        string traceId)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);

        var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
        request.Headers.Add("X-Signature", signature);
        request.Headers.Add("X-Timestamp", timestamp.ToString());
        request.Headers.Add("X-Nonce", nonce);
        request.Headers.Add("X-Trace-Id", traceId);
        request.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var response = await httpClient.SendAsync(request);
            stopwatch.Stop();

            var responseBody = await response.Content.ReadAsStringAsync();

            return new WebhookDeliveryResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                ResponseBody = responseBody,
                ErrorMessage = response.IsSuccessStatusCode ? null : $"HTTP {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            return new WebhookDeliveryResult
            {
                Success = false,
                StatusCode = 0,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }

    private DateTime CalculateNextRetryTime(int attemptCount)
    {
        var delays = new[] { 1, 5, 15, 30, 60, 120, 240, 480, 720, 1440 };
        var delayMinutes = attemptCount <= delays.Length ? delays[attemptCount - 1] : delays[^1];
        return DateTime.UtcNow.AddMinutes(delayMinutes);
    }

    private PaymentStatus? MapProviderStatusToPaymentStatus(string? providerStatus)
    {
        if (string.IsNullOrEmpty(providerStatus))
        {
            return null;
        }

        return providerStatus.ToLowerInvariant() switch
        {
            "paid" or "confirmed" or "approved" or "captured" => PaymentStatus.Paid,
            "pending" or "waiting" => PaymentStatus.Pending,
            "failed" or "rejected" or "declined" => PaymentStatus.Failed,
            "refunded" => PaymentStatus.Refunded,
            "expired" => PaymentStatus.Expired,
            "cancelled" or "canceled" => PaymentStatus.Cancelled,
            "authorized" => PaymentStatus.Authorized,
            _ => null
        };
    }
}
