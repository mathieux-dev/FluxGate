using FluxPay.Core.Entities;
using FluxPay.Core.Providers;
using FluxPay.Core.Services;
using FluxPay.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FluxPay.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly FluxPayDbContext _dbContext;
    private readonly IProviderFactory _providerFactory;
    private readonly IEncryptionService _encryptionService;
    private readonly IAuditService _auditService;

    public PaymentService(
        FluxPayDbContext dbContext,
        IProviderFactory providerFactory,
        IEncryptionService encryptionService,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _providerFactory = providerFactory;
        _encryptionService = encryptionService;
        _auditService = auditService;
    }

    public async Task<PaymentResult> CreatePaymentAsync(CreatePaymentRequest request, Guid merchantId)
    {
        ValidatePaymentRequest(request);

        var merchant = await _dbContext.Merchants
            .FirstOrDefaultAsync(m => m.Id == merchantId && m.Active);
        
        if (merchant == null)
        {
            throw new InvalidOperationException("Merchant not found or inactive");
        }

        var customer = await GetOrCreateCustomerAsync(request.Customer, merchantId);

        return request.Method switch
        {
            PaymentMethod.CreditCard or PaymentMethod.DebitCard => await CreateCardPaymentAsync(request, merchantId, customer.Id),
            PaymentMethod.Pix => await CreatePixPaymentAsync(request, merchantId, customer.Id),
            PaymentMethod.Boleto => await CreateBoletoPaymentAsync(request, merchantId, customer.Id),
            _ => throw new ArgumentException($"Unsupported payment method: {request.Method}")
        };
    }

    public async Task<Payment> GetPaymentAsync(Guid paymentId, Guid merchantId)
    {
        var payment = await _dbContext.Payments
            .Include(p => p.Customer)
            .Include(p => p.Transactions)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.MerchantId == merchantId);

        if (payment == null)
        {
            throw new InvalidOperationException("Payment not found");
        }

        return payment;
    }

    public async Task<PaymentRefundResult> RefundPaymentAsync(Guid paymentId, RefundRequest request, Guid merchantId)
    {
        var payment = await _dbContext.Payments
            .Include(p => p.Transactions)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.MerchantId == merchantId);

        if (payment == null)
        {
            throw new InvalidOperationException("Payment not found");
        }

        if (payment.Status != PaymentStatus.Paid && payment.Status != PaymentStatus.Authorized)
        {
            throw new InvalidOperationException($"Payment cannot be refunded. Current status: {payment.Status}");
        }

        if (request.AmountCents <= 0 || request.AmountCents > payment.AmountCents)
        {
            throw new ArgumentException("Invalid refund amount");
        }

        var totalRefunded = payment.Transactions
            .Where(t => t.Type == TransactionType.Refund && t.Status == TransactionStatus.Success)
            .Sum(t => t.AmountCents);

        if (totalRefunded + request.AmountCents > payment.AmountCents)
        {
            throw new InvalidOperationException("Refund amount exceeds remaining payment balance");
        }

        var provider = _providerFactory.GetProvider(payment.Provider);
        var refundResult = await provider.RefundAsync(payment.ProviderPaymentId!, request.AmountCents);

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            PaymentId = payment.Id,
            Type = TransactionType.Refund,
            Status = refundResult.Success ? TransactionStatus.Success : TransactionStatus.Failed,
            AmountCents = request.AmountCents,
            ProviderTxId = refundResult.ProviderRefundId,
            Payload = refundResult.RawResponse != null ? JsonSerializer.Serialize(refundResult.RawResponse) : null,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Transactions.Add(transaction);

        if (refundResult.Success)
        {
            var newTotalRefunded = totalRefunded + request.AmountCents;
            if (newTotalRefunded >= payment.AmountCents)
            {
                payment.Status = PaymentStatus.Refunded;
            }
            payment.UpdatedAt = DateTime.UtcNow;

            await _auditService.LogAsync(new AuditEntry
            {
                MerchantId = merchantId,
                Actor = $"merchant:{merchantId}",
                Action = "payment.refunded",
                ResourceType = "Payment",
                ResourceId = payment.Id,
                Changes = new { refundAmount = request.AmountCents, reason = request.Reason }
            });
        }

        await _dbContext.SaveChangesAsync();

        return new PaymentRefundResult
        {
            RefundId = transaction.Id,
            PaymentId = payment.Id,
            AmountCents = request.AmountCents,
            Status = refundResult.Status,
            CreatedAt = transaction.CreatedAt
        };
    }

    private async Task<PaymentResult> CreateCardPaymentAsync(CreatePaymentRequest request, Guid merchantId, Guid customerId)
    {
        if (string.IsNullOrEmpty(request.CardToken))
        {
            throw new ArgumentException("Card token is required for card payments");
        }

        var provider = _providerFactory.GetProviderForPaymentMethod(request.Method);

        var authRequest = new AuthorizationRequest
        {
            CardToken = request.CardToken,
            AmountCents = request.AmountCents,
            CustomerName = request.Customer.Name,
            CustomerEmail = request.Customer.Email,
            CustomerDocument = request.Customer.Document,
            Metadata = request.Metadata,
            Capture = true
        };

        var authResult = await provider.AuthorizeAsync(authRequest);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            CustomerId = customerId,
            AmountCents = request.AmountCents,
            Method = request.Method,
            Status = authResult.Success ? PaymentStatus.Authorized : PaymentStatus.Failed,
            Provider = provider.ProviderName,
            ProviderPaymentId = authResult.ProviderPaymentId,
            ProviderPayload = authResult.RawResponse != null ? JsonSerializer.Serialize(authResult.RawResponse) : null,
            Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            PaymentId = payment.Id,
            Type = TransactionType.Authorization,
            Status = authResult.Success ? TransactionStatus.Success : TransactionStatus.Failed,
            AmountCents = request.AmountCents,
            ProviderTxId = authResult.ProviderTransactionId,
            Payload = authResult.RawResponse != null ? JsonSerializer.Serialize(authResult.RawResponse) : null,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Payments.Add(payment);
        _dbContext.Transactions.Add(transaction);

        await _auditService.LogAsync(new AuditEntry
        {
            MerchantId = merchantId,
            Actor = $"merchant:{merchantId}",
            Action = "payment.created",
            ResourceType = "Payment",
            ResourceId = payment.Id,
            Changes = new { method = request.Method.ToString(), amount = request.AmountCents }
        });

        await _dbContext.SaveChangesAsync();

        return new PaymentResult
        {
            PaymentId = payment.Id,
            Status = payment.Status,
            AmountCents = payment.AmountCents,
            Method = payment.Method,
            CreatedAt = payment.CreatedAt
        };
    }

    private async Task<PaymentResult> CreatePixPaymentAsync(CreatePaymentRequest request, Guid merchantId, Guid customerId)
    {
        var pixProvider = _providerFactory.GetPixProvider();

        var pixRequest = new PixRequest
        {
            AmountCents = request.AmountCents,
            CustomerName = request.Customer.Name,
            CustomerEmail = request.Customer.Email,
            CustomerDocument = request.Customer.Document,
            ExpirationMinutes = 60,
            Metadata = request.Metadata
        };

        var pixResult = await pixProvider.CreatePixPaymentAsync(pixRequest);

        if (!pixResult.Success)
        {
            throw new InvalidOperationException($"PIX payment creation failed: {pixResult.ErrorMessage}");
        }

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            CustomerId = customerId,
            AmountCents = request.AmountCents,
            Method = PaymentMethod.Pix,
            Status = PaymentStatus.Pending,
            Provider = "gerencianet",
            ProviderPaymentId = pixResult.ProviderPaymentId,
            ProviderPayload = pixResult.RawResponse != null ? JsonSerializer.Serialize(pixResult.RawResponse) : null,
            Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Payments.Add(payment);

        await _auditService.LogAsync(new AuditEntry
        {
            MerchantId = merchantId,
            Actor = $"merchant:{merchantId}",
            Action = "payment.created",
            ResourceType = "Payment",
            ResourceId = payment.Id,
            Changes = new { method = "Pix", amount = request.AmountCents }
        });

        await _dbContext.SaveChangesAsync();

        return new PaymentResult
        {
            PaymentId = payment.Id,
            Status = payment.Status,
            AmountCents = payment.AmountCents,
            Method = payment.Method,
            CreatedAt = payment.CreatedAt,
            Pix = new PixData
            {
                QrCode = pixResult.QrCode!,
                QrCodeUrl = pixResult.QrCodeUrl,
                ExpiresAt = pixResult.ExpiresAt
            }
        };
    }

    private async Task<PaymentResult> CreateBoletoPaymentAsync(CreatePaymentRequest request, Guid merchantId, Guid customerId)
    {
        var boletoProvider = _providerFactory.GetBoletoProvider();

        var boletoRequest = new BoletoRequest
        {
            AmountCents = request.AmountCents,
            CustomerName = request.Customer.Name,
            CustomerEmail = request.Customer.Email,
            CustomerDocument = request.Customer.Document,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Metadata = request.Metadata
        };

        var boletoResult = await boletoProvider.CreateBoletoPaymentAsync(boletoRequest);

        if (!boletoResult.Success)
        {
            throw new InvalidOperationException($"Boleto payment creation failed: {boletoResult.ErrorMessage}");
        }

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            CustomerId = customerId,
            AmountCents = request.AmountCents,
            Method = PaymentMethod.Boleto,
            Status = PaymentStatus.Pending,
            Provider = "gerencianet",
            ProviderPaymentId = boletoResult.ProviderPaymentId,
            ProviderPayload = boletoResult.RawResponse != null ? JsonSerializer.Serialize(boletoResult.RawResponse) : null,
            Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Payments.Add(payment);

        await _auditService.LogAsync(new AuditEntry
        {
            MerchantId = merchantId,
            Actor = $"merchant:{merchantId}",
            Action = "payment.created",
            ResourceType = "Payment",
            ResourceId = payment.Id,
            Changes = new { method = "Boleto", amount = request.AmountCents }
        });

        await _dbContext.SaveChangesAsync();

        return new PaymentResult
        {
            PaymentId = payment.Id,
            Status = payment.Status,
            AmountCents = payment.AmountCents,
            Method = payment.Method,
            CreatedAt = payment.CreatedAt,
            Boleto = new BoletoData
            {
                Barcode = boletoResult.Barcode!,
                DigitableLine = boletoResult.DigitableLine,
                PdfUrl = boletoResult.PdfUrl,
                ExpiresAt = boletoResult.ExpiresAt
            }
        };
    }

    private async Task<Customer> GetOrCreateCustomerAsync(CustomerInfo customerInfo, Guid merchantId)
    {
        var emailHash = _encryptionService.Hash(customerInfo.Email.ToLowerInvariant());
        var documentHash = _encryptionService.Hash(customerInfo.Document);

        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(c => c.MerchantId == merchantId && c.DocumentHash == documentHash);

        if (customer == null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                MerchantId = merchantId,
                Name = customerInfo.Name,
                EmailHash = emailHash,
                DocumentHash = documentHash,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Customers.Add(customer);
        }

        return customer;
    }

    private void ValidatePaymentRequest(CreatePaymentRequest request)
    {
        if (request.AmountCents <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero");
        }

        if (request.Customer == null)
        {
            throw new ArgumentException("Customer information is required");
        }

        if (string.IsNullOrWhiteSpace(request.Customer.Name))
        {
            throw new ArgumentException("Customer name is required");
        }

        if (string.IsNullOrWhiteSpace(request.Customer.Email))
        {
            throw new ArgumentException("Customer email is required");
        }

        if (string.IsNullOrWhiteSpace(request.Customer.Document))
        {
            throw new ArgumentException("Customer document is required");
        }

        if (request.Method == PaymentMethod.CreditCard || request.Method == PaymentMethod.DebitCard)
        {
            if (string.IsNullOrWhiteSpace(request.CardToken))
            {
                throw new ArgumentException("Card token is required for card payments");
            }
        }
    }
}
