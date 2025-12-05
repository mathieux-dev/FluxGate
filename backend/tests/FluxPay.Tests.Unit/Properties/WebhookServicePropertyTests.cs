using FsCheck;
using FsCheck.Xunit;
using FluxPay.Core.Entities;
using FluxPay.Core.Providers;
using FluxPay.Core.Services;
using FluxPay.Infrastructure.Data;
using FluxPay.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using System.Text.Json;

namespace FluxPay.Tests.Unit.Properties;

public class WebhookServicePropertyTests : IDisposable
{
    private readonly FluxPayDbContext _dbContext;
    private readonly IProviderFactory _providerFactory;
    private readonly INonceStore _nonceStore;
    private readonly IHmacSignatureService _hmacService;
    private readonly IEncryptionService _encryptionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuditService _auditService;

    public WebhookServicePropertyTests()
    {
        var options = new DbContextOptionsBuilder<FluxPayDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new FluxPayDbContext(options);

        _providerFactory = Substitute.For<IProviderFactory>();
        _nonceStore = Substitute.For<INonceStore>();
        _hmacService = Substitute.For<IHmacSignatureService>();
        _encryptionService = Substitute.For<IEncryptionService>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _auditService = Substitute.For<IAuditService>();
    }

    [Property(MaxTest = 10)]
    public void Property_17_Inbound_Webhook_Validation_Completeness(NonEmptyString signature, NonEmptyString payload)
    {
        var provider = "pagarme";
        var nonce = Guid.NewGuid();
        
        var providerAdapter = Substitute.For<IProviderAdapter>();
        providerAdapter.ValidateWebhookSignatureAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>())
            .Returns(true);

        _providerFactory.GetProvider(provider).Returns(providerAdapter);

        _nonceStore.IsNonceUniqueAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        _nonceStore.StoreNonceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>())
            .Returns(Task.CompletedTask);

        _auditService.LogAsync(Arg.Any<AuditEntry>())
            .Returns(Task.CompletedTask);

        var service = new WebhookService(
            _dbContext,
            _providerFactory,
            _nonceStore,
            _hmacService,
            _encryptionService,
            _httpClientFactory,
            _auditService);

        var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var result = service.ValidateProviderWebhookAsync(
            provider,
            signature.Get,
            payload.Get,
            currentTimestamp,
            nonce.ToString()).Result;

        providerAdapter.Received(1).ValidateWebhookSignatureAsync(
            signature.Get, payload.Get, currentTimestamp);
        _nonceStore.Received(1).IsNonceUniqueAsync(
            $"provider:{provider}", nonce.ToString());

        Assert.True(result);
    }

    [Property(MaxTest = 10)]
    public void Property_18_Invalid_Webhook_Signature_Rejection(NonEmptyString signature, NonEmptyString payload)
    {
        var provider = "pagarme";
        var nonce = Guid.NewGuid();
        
        var auditService = Substitute.For<IAuditService>();
        auditService.LogAsync(Arg.Any<AuditEntry>())
            .Returns(Task.CompletedTask);
        
        var providerAdapter = Substitute.For<IProviderAdapter>();
        providerAdapter.ValidateWebhookSignatureAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>())
            .Returns(false);

        var providerFactory = Substitute.For<IProviderFactory>();
        providerFactory.GetProvider(provider).Returns(providerAdapter);

        var nonceStore = Substitute.For<INonceStore>();
        nonceStore.IsNonceUniqueAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        var service = new WebhookService(
            _dbContext,
            providerFactory,
            nonceStore,
            _hmacService,
            _encryptionService,
            _httpClientFactory,
            auditService);

        var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var result = service.ValidateProviderWebhookAsync(
            provider,
            signature.Get,
            payload.Get,
            currentTimestamp,
            nonce.ToString()).Result;

        auditService.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == "webhook.rejected.invalid_signature"));

        Assert.False(result);
    }

    [Property(MaxTest = 10)]
    public void Property_19_Webhook_Timestamp_Skew_Rejection(NonEmptyString signature, NonEmptyString payload)
    {
        var provider = "pagarme";
        var nonce = Guid.NewGuid();
        
        var auditService = Substitute.For<IAuditService>();
        auditService.LogAsync(Arg.Any<AuditEntry>())
            .Returns(Task.CompletedTask);

        var service = new WebhookService(
            _dbContext,
            _providerFactory,
            _nonceStore,
            _hmacService,
            _encryptionService,
            _httpClientFactory,
            auditService);

        var oldTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        var result = service.ValidateProviderWebhookAsync(
            provider,
            signature.Get,
            payload.Get,
            oldTimestamp,
            nonce.ToString()).Result;

        auditService.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == "webhook.rejected.timestamp_skew"));

        Assert.False(result);
    }

    [Property(MaxTest = 10)]
    public void Property_20_Webhook_Nonce_Replay_Protection(NonEmptyString signature, NonEmptyString payload)
    {
        var provider = "pagarme";
        var nonce = Guid.NewGuid();
        
        var auditService = Substitute.For<IAuditService>();
        auditService.LogAsync(Arg.Any<AuditEntry>())
            .Returns(Task.CompletedTask);
        
        var nonceStore = Substitute.For<INonceStore>();
        nonceStore.IsNonceUniqueAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        var service = new WebhookService(
            _dbContext,
            _providerFactory,
            nonceStore,
            _hmacService,
            _encryptionService,
            _httpClientFactory,
            auditService);

        var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var result = service.ValidateProviderWebhookAsync(
            provider,
            signature.Get,
            payload.Get,
            currentTimestamp,
            nonce.ToString()).Result;

        auditService.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == "webhook.rejected.nonce_reused"));

        Assert.False(result);
    }

    [Property(MaxTest = 10)]
    public void Property_21_Valid_Webhook_Processing_Flow(NonEmptyString providerPaymentId, PositiveInt amountCents)
    {
        var provider = "pagarme";
        var merchantId = Guid.NewGuid();
        
        var merchant = new Merchant
        {
            Id = merchantId,
            Name = "Test Merchant",
            Email = "test@merchant.com",
            ProviderConfigEncrypted = "encrypted",
            Active = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            AmountCents = amountCents.Get,
            Method = PaymentMethod.CreditCard,
            Status = PaymentStatus.Pending,
            Provider = provider,
            ProviderPaymentId = providerPaymentId.Get,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Merchants.Add(merchant);
        _dbContext.Payments.Add(payment);
        _dbContext.SaveChanges();

        _auditService.LogAsync(Arg.Any<AuditEntry>())
            .Returns(Task.CompletedTask);

        var service = new WebhookService(
            _dbContext,
            _providerFactory,
            _nonceStore,
            _hmacService,
            _encryptionService,
            _httpClientFactory,
            _auditService);

        var webhookEvent = new ProviderWebhookEvent
        {
            Provider = provider,
            EventType = "payment.paid",
            Payload = JsonSerializer.Serialize(new { status = "paid" }),
            ProviderPaymentId = providerPaymentId.Get,
            Status = "paid"
        };

        service.ProcessProviderWebhookAsync(webhookEvent).Wait();

        var webhookReceived = _dbContext.WebhooksReceived
            .FirstOrDefault(w => w.Provider == provider && w.EventType == "payment.paid");
        var updatedPayment = _dbContext.Payments.Find(payment.Id);

        _dbContext.Merchants.Remove(merchant);
        _dbContext.Payments.Remove(payment);
        if (webhookReceived != null)
        {
            _dbContext.WebhooksReceived.Remove(webhookReceived);
        }
        _dbContext.SaveChanges();

        Assert.True(webhookReceived != null && 
                   webhookReceived.Processed && 
                   updatedPayment?.Status == PaymentStatus.Paid);
    }

    [Property(MaxTest = 10)]
    public void Property_7_PIX_Confirmation_State_Transition(NonEmptyString providerPaymentId, PositiveInt amountCents)
    {
        var provider = "gerencianet";
        var merchantId = Guid.NewGuid();
        
        var merchant = new Merchant
        {
            Id = merchantId,
            Name = "Test Merchant",
            Email = "test@merchant.com",
            ProviderConfigEncrypted = "encrypted",
            Active = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var pixPayment = new Payment
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            AmountCents = amountCents.Get,
            Method = PaymentMethod.Pix,
            Status = PaymentStatus.Pending,
            Provider = provider,
            ProviderPaymentId = providerPaymentId.Get,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Merchants.Add(merchant);
        _dbContext.Payments.Add(pixPayment);
        _dbContext.SaveChanges();

        _auditService.LogAsync(Arg.Any<AuditEntry>())
            .Returns(Task.CompletedTask);

        var service = new WebhookService(
            _dbContext,
            _providerFactory,
            _nonceStore,
            _hmacService,
            _encryptionService,
            _httpClientFactory,
            _auditService);

        var webhookEvent = new ProviderWebhookEvent
        {
            Provider = provider,
            EventType = "pix.paid",
            Payload = JsonSerializer.Serialize(new { status = "paid" }),
            ProviderPaymentId = providerPaymentId.Get,
            Status = "paid"
        };

        service.ProcessProviderWebhookAsync(webhookEvent).Wait();

        var updatedPayment = _dbContext.Payments.Find(pixPayment.Id);

        _dbContext.Merchants.Remove(merchant);
        _dbContext.Payments.Remove(pixPayment);
        var webhookReceived = _dbContext.WebhooksReceived
            .FirstOrDefault(w => w.Provider == provider && w.EventType == "pix.paid");
        if (webhookReceived != null)
        {
            _dbContext.WebhooksReceived.Remove(webhookReceived);
        }
        var webhookDeliveries = _dbContext.WebhookDeliveries
            .Where(w => w.PaymentId == pixPayment.Id).ToList();
        foreach (var delivery in webhookDeliveries)
        {
            _dbContext.WebhookDeliveries.Remove(delivery);
        }
        _dbContext.SaveChanges();

        Assert.True(updatedPayment?.Status == PaymentStatus.Paid);
    }

    [Property(MaxTest = 10)]
    public void Property_10_Boleto_Confirmation_State_Transition(NonEmptyString providerPaymentId, PositiveInt amountCents)
    {
        var provider = "gerencianet";
        var merchantId = Guid.NewGuid();
        
        var merchant = new Merchant
        {
            Id = merchantId,
            Name = "Test Merchant",
            Email = "test@merchant.com",
            ProviderConfigEncrypted = "encrypted",
            Active = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var boletoPayment = new Payment
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            AmountCents = amountCents.Get,
            Method = PaymentMethod.Boleto,
            Status = PaymentStatus.Pending,
            Provider = provider,
            ProviderPaymentId = providerPaymentId.Get,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Merchants.Add(merchant);
        _dbContext.Payments.Add(boletoPayment);
        _dbContext.SaveChanges();

        _auditService.LogAsync(Arg.Any<AuditEntry>())
            .Returns(Task.CompletedTask);

        var service = new WebhookService(
            _dbContext,
            _providerFactory,
            _nonceStore,
            _hmacService,
            _encryptionService,
            _httpClientFactory,
            _auditService);

        var webhookEvent = new ProviderWebhookEvent
        {
            Provider = provider,
            EventType = "boleto.paid",
            Payload = JsonSerializer.Serialize(new { status = "paid" }),
            ProviderPaymentId = providerPaymentId.Get,
            Status = "paid"
        };

        service.ProcessProviderWebhookAsync(webhookEvent).Wait();

        var updatedPayment = _dbContext.Payments.Find(boletoPayment.Id);

        _dbContext.Merchants.Remove(merchant);
        _dbContext.Payments.Remove(boletoPayment);
        var webhookReceived = _dbContext.WebhooksReceived
            .FirstOrDefault(w => w.Provider == provider && w.EventType == "boleto.paid");
        if (webhookReceived != null)
        {
            _dbContext.WebhooksReceived.Remove(webhookReceived);
        }
        var webhookDeliveries = _dbContext.WebhookDeliveries
            .Where(w => w.PaymentId == boletoPayment.Id).ToList();
        foreach (var delivery in webhookDeliveries)
        {
            _dbContext.WebhookDeliveries.Remove(delivery);
        }
        _dbContext.SaveChanges();

        Assert.True(updatedPayment?.Status == PaymentStatus.Paid);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
