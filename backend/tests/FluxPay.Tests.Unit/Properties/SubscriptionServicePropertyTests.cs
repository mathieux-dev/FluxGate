using FsCheck;
using FsCheck.Xunit;
using FluxPay.Core.Entities;
using FluxPay.Core.Providers;
using FluxPay.Core.Services;
using FluxPay.Infrastructure.Data;
using FluxPay.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using System.Security.Cryptography;

namespace FluxPay.Tests.Unit.Properties;

public class SubscriptionServicePropertyTests : IDisposable
{
    private readonly string _originalEncryptionKey;
    private readonly EncryptionService _encryptionService;

    public SubscriptionServicePropertyTests()
    {
        _originalEncryptionKey = Environment.GetEnvironmentVariable("MASTER_ENCRYPTION_KEY") ?? string.Empty;

        var encryptionKey = new byte[32];
        RandomNumberGenerator.Fill(encryptionKey);
        Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", Convert.ToBase64String(encryptionKey));

        _encryptionService = new EncryptionService();
    }

    public void Dispose()
    {
        if (string.IsNullOrEmpty(_originalEncryptionKey))
            Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", null);
        else
            Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", _originalEncryptionKey);
    }

    [Property(MaxTest = 100)]
    public void Subscription_Creation_Completeness_Should_Create_Via_PagarMe_And_Return_Subscription_ID()
    {
        Prop.ForAll(
            GenerateValidSubscriptionRequest(),
            request =>
            {
                var options = new DbContextOptionsBuilder<FluxPayDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;
                using var dbContext = new FluxPayDbContext(options);

                var merchantId = Guid.NewGuid();
                var merchant = new Merchant
                {
                    Id = merchantId,
                    Name = "Test Merchant",
                    Email = "merchant@test.com",
                    ProviderConfigEncrypted = "{}",
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.Merchants.Add(merchant);
                dbContext.SaveChanges();

                var providerSubscriptionId = $"sub_{Guid.NewGuid()}";
                var mockSubscriptionProvider = Substitute.For<IProviderAdapter, ISubscriptionProvider>();
                ((IProviderAdapter)mockSubscriptionProvider).ProviderName.Returns("pagarme");
                ((ISubscriptionProvider)mockSubscriptionProvider).CreateSubscriptionAsync(Arg.Any<SubscriptionRequest>())
                    .Returns(Task.FromResult(new SubscriptionResult
                    {
                        Success = true,
                        ProviderSubscriptionId = providerSubscriptionId,
                        Status = "active",
                        NextBillingDate = DateTime.UtcNow.AddMonths(1)
                    }));

                var mockProviderFactory = Substitute.For<IProviderFactory>();
                mockProviderFactory.GetProvider("pagarme")
                    .Returns((IProviderAdapter)mockSubscriptionProvider);

                var mockAuditService = Substitute.For<IAuditService>();
                mockAuditService.LogAsync(Arg.Any<AuditEntry>())
                    .Returns(Task.CompletedTask);

                var service = new SubscriptionService(dbContext, mockProviderFactory, _encryptionService, mockAuditService);

                var result = service.CreateSubscriptionAsync(request, merchantId).Result;

                ((ISubscriptionProvider)mockSubscriptionProvider).Received(1).CreateSubscriptionAsync(Arg.Any<SubscriptionRequest>());

                return result.SubscriptionId != Guid.Empty &&
                       result.ProviderSubscriptionId == providerSubscriptionId &&
                       result.Status == SubscriptionStatus.Active;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void Subscription_Payment_Linkage_Should_Link_Subscription_To_Customer()
    {
        Prop.ForAll(
            GenerateValidSubscriptionRequest(),
            request =>
            {
                var options = new DbContextOptionsBuilder<FluxPayDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;
                using var dbContext = new FluxPayDbContext(options);

                var merchantId = Guid.NewGuid();
                var merchant = new Merchant
                {
                    Id = merchantId,
                    Name = "Test Merchant",
                    Email = "merchant@test.com",
                    ProviderConfigEncrypted = "{}",
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.Merchants.Add(merchant);
                dbContext.SaveChanges();

                var providerSubscriptionId = $"sub_{Guid.NewGuid()}";
                var mockSubscriptionProvider = Substitute.For<IProviderAdapter, ISubscriptionProvider>();
                ((IProviderAdapter)mockSubscriptionProvider).ProviderName.Returns("pagarme");
                ((ISubscriptionProvider)mockSubscriptionProvider).CreateSubscriptionAsync(Arg.Any<SubscriptionRequest>())
                    .Returns(Task.FromResult(new SubscriptionResult
                    {
                        Success = true,
                        ProviderSubscriptionId = providerSubscriptionId,
                        Status = "active",
                        NextBillingDate = DateTime.UtcNow.AddMonths(1)
                    }));

                var mockProviderFactory = Substitute.For<IProviderFactory>();
                mockProviderFactory.GetProvider("pagarme")
                    .Returns((IProviderAdapter)mockSubscriptionProvider);

                var mockAuditService = Substitute.For<IAuditService>();
                mockAuditService.LogAsync(Arg.Any<AuditEntry>())
                    .Returns(Task.CompletedTask);

                var service = new SubscriptionService(dbContext, mockProviderFactory, _encryptionService, mockAuditService);

                var result = service.CreateSubscriptionAsync(request, merchantId).Result;

                var subscription = dbContext.Subscriptions
                    .Include(s => s.Customer)
                    .AsNoTracking()
                    .FirstOrDefault(s => s.Id == result.SubscriptionId);

                return subscription != null &&
                       subscription.CustomerId != Guid.Empty &&
                       subscription.Customer != null &&
                       subscription.MerchantId == merchantId;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void Subscription_Cancellation_State_Transition_Should_Cancel_With_PagarMe_And_Update_Status()
    {
        Prop.ForAll(
            GenerateValidSubscriptionRequest(),
            request =>
            {
                var options = new DbContextOptionsBuilder<FluxPayDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;
                using var dbContext = new FluxPayDbContext(options);

                var merchantId = Guid.NewGuid();
                var merchant = new Merchant
                {
                    Id = merchantId,
                    Name = "Test Merchant",
                    Email = "merchant@test.com",
                    ProviderConfigEncrypted = "{}",
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.Merchants.Add(merchant);
                dbContext.SaveChanges();

                var providerSubscriptionId = $"sub_{Guid.NewGuid()}";
                var mockSubscriptionProvider = Substitute.For<IProviderAdapter, ISubscriptionProvider>();
                ((IProviderAdapter)mockSubscriptionProvider).ProviderName.Returns("pagarme");
                ((ISubscriptionProvider)mockSubscriptionProvider).CreateSubscriptionAsync(Arg.Any<SubscriptionRequest>())
                    .Returns(Task.FromResult(new SubscriptionResult
                    {
                        Success = true,
                        ProviderSubscriptionId = providerSubscriptionId,
                        Status = "active",
                        NextBillingDate = DateTime.UtcNow.AddMonths(1)
                    }));
                ((ISubscriptionProvider)mockSubscriptionProvider).CancelSubscriptionAsync(Arg.Any<string>())
                    .Returns(Task.FromResult(new SubscriptionCancellationResult
                    {
                        Success = true,
                        ProviderSubscriptionId = providerSubscriptionId,
                        Status = "cancelled",
                        CancelledAt = DateTime.UtcNow
                    }));

                var mockProviderFactory = Substitute.For<IProviderFactory>();
                mockProviderFactory.GetProvider("pagarme")
                    .Returns((IProviderAdapter)mockSubscriptionProvider);

                var mockAuditService = Substitute.For<IAuditService>();
                mockAuditService.LogAsync(Arg.Any<AuditEntry>())
                    .Returns(Task.CompletedTask);

                var service = new SubscriptionService(dbContext, mockProviderFactory, _encryptionService, mockAuditService);

                var createResult = service.CreateSubscriptionAsync(request, merchantId).Result;

                var cancelResult = service.CancelSubscriptionAsync(createResult.SubscriptionId, merchantId).Result;

                ((ISubscriptionProvider)mockSubscriptionProvider).Received(1).CancelSubscriptionAsync(providerSubscriptionId);

                var subscription = dbContext.Subscriptions
                    .AsNoTracking()
                    .FirstOrDefault(s => s.Id == createResult.SubscriptionId);

                return cancelResult.Status == SubscriptionStatus.Cancelled &&
                       cancelResult.CancelledAt != null &&
                       subscription != null &&
                       subscription.Status == SubscriptionStatus.Cancelled &&
                       subscription.CancelledAt != null;
            }
        ).QuickCheckThrowOnFailure();
    }

    private static Arbitrary<CreateSubscriptionRequest> GenerateValidSubscriptionRequest()
    {
        var gen = from amountCents in Gen.Choose(1000, 100000)
                  from interval in Gen.Elements("month", "year", "week")
                  from name in Gen.Elements("João Silva", "Maria Santos", "Carlos Oliveira", "Ana Costa")
                  from email in Gen.Elements("joao@example.com", "maria@example.com", "carlos@example.com", "ana@example.com")
                  from document in Gen.Elements("12345678900", "98765432100", "11122233344", "55566677788")
                  from cardToken in Gen.Elements("card_tok_abc123", "card_tok_xyz789", "card_tok_def456")
                  select new CreateSubscriptionRequest
                  {
                      AmountCents = amountCents,
                      Interval = interval,
                      CardToken = cardToken,
                      Customer = new CustomerInfo
                      {
                          Name = name,
                          Email = email,
                          Document = document
                      },
                      Metadata = new Dictionary<string, string>
                      {
                          { "plan_id", $"PLAN-{Guid.NewGuid()}" }
                      }
                  };

        return Arb.From(gen);
    }
}
