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
using System.Text.RegularExpressions;

namespace FluxPay.Tests.Unit.Properties;

public class PaymentServicePropertyTests : IDisposable
{
    private readonly string _originalEncryptionKey;
    private readonly EncryptionService _encryptionService;

    public PaymentServicePropertyTests()
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

    [Property(MaxTest = 10)]
    public void Card_Payment_Creation_Invariant_Should_Create_Payment_Record_And_Call_Provider()
    {
        Prop.ForAll(
            GenerateValidCardPaymentRequest(),
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

                var mockProviderAdapter = Substitute.For<IProviderAdapter>();
                mockProviderAdapter.ProviderName.Returns("pagarme");
                mockProviderAdapter.AuthorizeAsync(Arg.Any<AuthorizationRequest>())
                    .Returns(Task.FromResult(new AuthorizationResult
                    {
                        Success = true,
                        ProviderTransactionId = Guid.NewGuid().ToString(),
                        ProviderPaymentId = Guid.NewGuid().ToString(),
                        Status = "authorized",
                        CardLastFourDigits = "1234",
                        CardBrand = "visa"
                    }));

                var mockProviderFactory = Substitute.For<IProviderFactory>();
                mockProviderFactory.GetProviderForPaymentMethod(Arg.Any<PaymentMethod>())
                    .Returns(mockProviderAdapter);

                var mockAuditService = Substitute.For<IAuditService>();
                mockAuditService.LogAsync(Arg.Any<AuditEntry>())
                    .Returns(Task.CompletedTask);

                var service = new PaymentService(dbContext, mockProviderFactory, _encryptionService, mockAuditService);

                var result = service.CreatePaymentAsync(request, merchantId).Result;

                var paymentExists = dbContext.Payments.Any(p => p.Id == result.PaymentId);
                mockProviderAdapter.Received(1).AuthorizeAsync(Arg.Any<AuthorizationRequest>());

                return paymentExists && result.PaymentId != Guid.Empty;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Authorized_Payment_Data_Constraints_Should_Not_Store_PAN_Or_CVV()
    {
        Prop.ForAll(
            GenerateValidCardPaymentRequest(),
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

                var mockProviderAdapter = Substitute.For<IProviderAdapter>();
                mockProviderAdapter.ProviderName.Returns("pagarme");
                mockProviderAdapter.AuthorizeAsync(Arg.Any<AuthorizationRequest>())
                    .Returns(Task.FromResult(new AuthorizationResult
                    {
                        Success = true,
                        ProviderTransactionId = Guid.NewGuid().ToString(),
                        ProviderPaymentId = Guid.NewGuid().ToString(),
                        Status = "authorized",
                        CardLastFourDigits = "1234",
                        CardBrand = "visa"
                    }));

                var mockProviderFactory = Substitute.For<IProviderFactory>();
                mockProviderFactory.GetProviderForPaymentMethod(Arg.Any<PaymentMethod>())
                    .Returns(mockProviderAdapter);

                var mockAuditService = Substitute.For<IAuditService>();
                mockAuditService.LogAsync(Arg.Any<AuditEntry>())
                    .Returns(Task.CompletedTask);

                var service = new PaymentService(dbContext, mockProviderFactory, _encryptionService, mockAuditService);

                var result = service.CreatePaymentAsync(request, merchantId).Result;

                var payment = dbContext.Payments
                    .AsNoTracking()
                    .FirstOrDefault(p => p.Id == result.PaymentId);

                if (payment == null)
                    return false;

                var panPattern = new Regex(@"\b\d{13,19}\b");
                var cvvPattern = new Regex(@"\b\d{3,4}\b");

                var fieldsToCheck = new[]
                {
                    payment.ProviderPaymentId ?? "",
                    payment.ProviderPayload ?? "",
                    payment.Metadata ?? ""
                };

                var combinedData = string.Join(" ", fieldsToCheck);
                var containsPAN = panPattern.IsMatch(combinedData);
                var containsCVV = cvvPattern.IsMatch(combinedData) && combinedData.Contains("cvv", StringComparison.OrdinalIgnoreCase);

                return !containsPAN && !containsCVV;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Payment_Validation_Ordering_Should_Validate_Before_Provider_Call()
    {
        Prop.ForAll(
            GenerateInvalidPaymentRequest(),
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

                var mockProviderAdapter = Substitute.For<IProviderAdapter>();
                mockProviderAdapter.ProviderName.Returns("pagarme");
                mockProviderAdapter.AuthorizeAsync(Arg.Any<AuthorizationRequest>())
                    .Returns(Task.FromResult(new AuthorizationResult
                    {
                        Success = true,
                        ProviderTransactionId = Guid.NewGuid().ToString(),
                        ProviderPaymentId = Guid.NewGuid().ToString(),
                        Status = "authorized"
                    }));

                var mockProviderFactory = Substitute.For<IProviderFactory>();
                mockProviderFactory.GetProviderForPaymentMethod(Arg.Any<PaymentMethod>())
                    .Returns(mockProviderAdapter);

                var mockAuditService = Substitute.For<IAuditService>();
                mockAuditService.LogAsync(Arg.Any<AuditEntry>())
                    .Returns(Task.CompletedTask);

                var service = new PaymentService(dbContext, mockProviderFactory, _encryptionService, mockAuditService);

                try
                {
                    var result = service.CreatePaymentAsync(request, merchantId).Result;
                    return false;
                }
                catch (AggregateException ex) when (ex.InnerException is ArgumentException)
                {
                    mockProviderAdapter.DidNotReceive().AuthorizeAsync(Arg.Any<AuthorizationRequest>());
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        ).QuickCheckThrowOnFailure();
    }

    private static Arbitrary<CreatePaymentRequest> GenerateValidCardPaymentRequest()
    {
        var gen = from amountCents in Gen.Choose(100, 1000000)
                  from name in Gen.Elements("João Silva", "Maria Santos", "Carlos Oliveira", "Ana Costa")
                  from email in Gen.Elements("joao@example.com", "maria@example.com", "carlos@example.com", "ana@example.com")
                  from document in Gen.Elements("12345678900", "98765432100", "11122233344", "55566677788")
                  from cardToken in Gen.Elements("card_tok_abc123", "card_tok_xyz789", "card_tok_def456")
                  select new CreatePaymentRequest
                  {
                      AmountCents = amountCents,
                      Method = PaymentMethod.CreditCard,
                      CardToken = cardToken,
                      Customer = new CustomerInfo
                      {
                          Name = name,
                          Email = email,
                          Document = document
                      },
                      Metadata = new Dictionary<string, string>
                      {
                          { "order_id", $"ORD-{Guid.NewGuid()}" }
                      }
                  };

        return Arb.From(gen);
    }

    private static Arbitrary<CreatePaymentRequest> GenerateInvalidPaymentRequest()
    {
        var gen = Gen.OneOf(
            Gen.Constant(new CreatePaymentRequest
            {
                AmountCents = -100,
                Method = PaymentMethod.CreditCard,
                CardToken = "card_tok_abc123",
                Customer = new CustomerInfo
                {
                    Name = "Test User",
                    Email = "test@example.com",
                    Document = "12345678900"
                }
            }),
            Gen.Constant(new CreatePaymentRequest
            {
                AmountCents = 0,
                Method = PaymentMethod.CreditCard,
                CardToken = "card_tok_abc123",
                Customer = new CustomerInfo
                {
                    Name = "Test User",
                    Email = "test@example.com",
                    Document = "12345678900"
                }
            }),
            Gen.Constant(new CreatePaymentRequest
            {
                AmountCents = 1000,
                Method = PaymentMethod.CreditCard,
                CardToken = "card_tok_abc123",
                Customer = new CustomerInfo
                {
                    Name = "",
                    Email = "test@example.com",
                    Document = "12345678900"
                }
            }),
            Gen.Constant(new CreatePaymentRequest
            {
                AmountCents = 1000,
                Method = PaymentMethod.CreditCard,
                CardToken = "card_tok_abc123",
                Customer = new CustomerInfo
                {
                    Name = "Test User",
                    Email = "",
                    Document = "12345678900"
                }
            }),
            Gen.Constant(new CreatePaymentRequest
            {
                AmountCents = 1000,
                Method = PaymentMethod.CreditCard,
                CardToken = "",
                Customer = new CustomerInfo
                {
                    Name = "Test User",
                    Email = "test@example.com",
                    Document = "12345678900"
                }
            })
        );

        return Arb.From(gen);
    }

    [Property(MaxTest = 10)]
    public void PIX_Payment_Response_Completeness_Should_Include_QR_Code_And_Payment_ID()
    {
        Prop.ForAll(
            GenerateValidPixPaymentRequest(),
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

                var mockPixProvider = Substitute.For<IPixProvider>();
                mockPixProvider.CreatePixPaymentAsync(Arg.Any<PixRequest>())
                    .Returns(Task.FromResult(new PixResult
                    {
                        Success = true,
                        ProviderPaymentId = Guid.NewGuid().ToString(),
                        QrCode = "00020126580014br.gov.bcb.pix...",
                        QrCodeUrl = "https://api.gerencianet.com.br/qr/abc123.png",
                        ExpiresAt = DateTime.UtcNow.AddHours(1)
                    }));

                var mockProviderFactory = Substitute.For<IProviderFactory>();
                mockProviderFactory.GetPixProvider()
                    .Returns(mockPixProvider);

                var mockAuditService = Substitute.For<IAuditService>();
                mockAuditService.LogAsync(Arg.Any<AuditEntry>())
                    .Returns(Task.CompletedTask);

                var service = new PaymentService(dbContext, mockProviderFactory, _encryptionService, mockAuditService);

                var result = service.CreatePaymentAsync(request, merchantId).Result;

                return result.PaymentId != Guid.Empty &&
                       result.Pix != null &&
                       !string.IsNullOrEmpty(result.Pix.QrCode);
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void PIX_Initial_State_Invariant_Should_Have_Pending_Status()
    {
        Prop.ForAll(
            GenerateValidPixPaymentRequest(),
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

                var providerPaymentId = Guid.NewGuid().ToString();
                var mockPixProvider = Substitute.For<IPixProvider>();
                mockPixProvider.CreatePixPaymentAsync(Arg.Any<PixRequest>())
                    .Returns(Task.FromResult(new PixResult
                    {
                        Success = true,
                        ProviderPaymentId = providerPaymentId,
                        QrCode = "00020126580014br.gov.bcb.pix...",
                        QrCodeUrl = "https://api.gerencianet.com.br/qr/abc123.png",
                        ExpiresAt = DateTime.UtcNow.AddHours(1)
                    }));

                var mockProviderFactory = Substitute.For<IProviderFactory>();
                mockProviderFactory.GetPixProvider()
                    .Returns(mockPixProvider);

                var mockAuditService = Substitute.For<IAuditService>();
                mockAuditService.LogAsync(Arg.Any<AuditEntry>())
                    .Returns(Task.CompletedTask);

                var service = new PaymentService(dbContext, mockProviderFactory, _encryptionService, mockAuditService);

                var result = service.CreatePaymentAsync(request, merchantId).Result;

                var payment = dbContext.Payments
                    .AsNoTracking()
                    .FirstOrDefault(p => p.Id == result.PaymentId);

                return payment != null &&
                       payment.Status == PaymentStatus.Pending &&
                       payment.Provider == "gerencianet" &&
                       payment.ProviderPaymentId == providerPaymentId;
            }
        ).QuickCheckThrowOnFailure();
    }

    private static Arbitrary<CreatePaymentRequest> GenerateValidPixPaymentRequest()
    {
        var gen = from amountCents in Gen.Choose(100, 1000000)
                  from name in Gen.Elements("João Silva", "Maria Santos", "Carlos Oliveira", "Ana Costa")
                  from email in Gen.Elements("joao@example.com", "maria@example.com", "carlos@example.com", "ana@example.com")
                  from document in Gen.Elements("12345678900", "98765432100", "11122233344", "55566677788")
                  select new CreatePaymentRequest
                  {
                      AmountCents = amountCents,
                      Method = PaymentMethod.Pix,
                      Customer = new CustomerInfo
                      {
                          Name = name,
                          Email = email,
                          Document = document
                      },
                      Metadata = new Dictionary<string, string>
                      {
                          { "order_id", $"ORD-{Guid.NewGuid()}" }
                      }
                  };

        return Arb.From(gen);
    }

    [Property(MaxTest = 10)]
    public void Refund_Transaction_Linkage_Should_Create_Linked_Transaction_Record()
    {
        Prop.ForAll(
            GenerateValidCardPaymentRequest(),
            GenerateValidRefundRequest(),
            (paymentRequest, refundRequest) =>
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

                var mockProviderAdapter = Substitute.For<IProviderAdapter>();
                mockProviderAdapter.ProviderName.Returns("pagarme");
                mockProviderAdapter.AuthorizeAsync(Arg.Any<AuthorizationRequest>())
                    .Returns(Task.FromResult(new AuthorizationResult
                    {
                        Success = true,
                        ProviderTransactionId = Guid.NewGuid().ToString(),
                        ProviderPaymentId = Guid.NewGuid().ToString(),
                        Status = "authorized",
                        CardLastFourDigits = "1234",
                        CardBrand = "visa"
                    }));
                mockProviderAdapter.RefundAsync(Arg.Any<string>(), Arg.Any<long>())
                    .Returns(Task.FromResult(new RefundResult
                    {
                        Success = true,
                        ProviderRefundId = Guid.NewGuid().ToString(),
                        Status = "refunded"
                    }));

                var mockProviderFactory = Substitute.For<IProviderFactory>();
                mockProviderFactory.GetProviderForPaymentMethod(Arg.Any<PaymentMethod>())
                    .Returns(mockProviderAdapter);
                mockProviderFactory.GetProvider(Arg.Any<string>())
                    .Returns(mockProviderAdapter);

                var mockAuditService = Substitute.For<IAuditService>();
                mockAuditService.LogAsync(Arg.Any<AuditEntry>())
                    .Returns(Task.CompletedTask);

                var service = new PaymentService(dbContext, mockProviderFactory, _encryptionService, mockAuditService);

                var paymentResult = service.CreatePaymentAsync(paymentRequest, merchantId).Result;

                var payment = dbContext.Payments.First(p => p.Id == paymentResult.PaymentId);
                payment.Status = PaymentStatus.Paid;
                dbContext.SaveChanges();

                var refundAmount = Math.Min(refundRequest.AmountCents, payment.AmountCents);
                var adjustedRefundRequest = new RefundRequest
                {
                    AmountCents = refundAmount,
                    Reason = refundRequest.Reason
                };

                var refundResult = service.RefundPaymentAsync(paymentResult.PaymentId, adjustedRefundRequest, merchantId).Result;

                var refundTransaction = dbContext.Transactions
                    .FirstOrDefault(t => t.Id == refundResult.RefundId);

                return refundTransaction != null &&
                       refundTransaction.PaymentId == paymentResult.PaymentId &&
                       refundTransaction.Type == TransactionType.Refund &&
                       refundTransaction.AmountCents == refundAmount;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Refund_Completion_Flow_Should_Update_Status_And_Trigger_Audit()
    {
        Prop.ForAll(
            GenerateValidCardPaymentRequest(),
            paymentRequest =>
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

                var mockProviderAdapter = Substitute.For<IProviderAdapter>();
                mockProviderAdapter.ProviderName.Returns("pagarme");
                mockProviderAdapter.AuthorizeAsync(Arg.Any<AuthorizationRequest>())
                    .Returns(Task.FromResult(new AuthorizationResult
                    {
                        Success = true,
                        ProviderTransactionId = Guid.NewGuid().ToString(),
                        ProviderPaymentId = Guid.NewGuid().ToString(),
                        Status = "authorized",
                        CardLastFourDigits = "1234",
                        CardBrand = "visa"
                    }));
                mockProviderAdapter.RefundAsync(Arg.Any<string>(), Arg.Any<long>())
                    .Returns(Task.FromResult(new RefundResult
                    {
                        Success = true,
                        ProviderRefundId = Guid.NewGuid().ToString(),
                        Status = "refunded"
                    }));

                var mockProviderFactory = Substitute.For<IProviderFactory>();
                mockProviderFactory.GetProviderForPaymentMethod(Arg.Any<PaymentMethod>())
                    .Returns(mockProviderAdapter);
                mockProviderFactory.GetProvider(Arg.Any<string>())
                    .Returns(mockProviderAdapter);

                var mockAuditService = Substitute.For<IAuditService>();
                mockAuditService.LogAsync(Arg.Any<AuditEntry>())
                    .Returns(Task.CompletedTask);

                var service = new PaymentService(dbContext, mockProviderFactory, _encryptionService, mockAuditService);

                var paymentResult = service.CreatePaymentAsync(paymentRequest, merchantId).Result;

                var payment = dbContext.Payments.First(p => p.Id == paymentResult.PaymentId);
                payment.Status = PaymentStatus.Paid;
                dbContext.SaveChanges();

                var refundRequest = new RefundRequest
                {
                    AmountCents = payment.AmountCents,
                    Reason = "Customer requested refund"
                };

                var refundResult = service.RefundPaymentAsync(paymentResult.PaymentId, refundRequest, merchantId).Result;

                var updatedPayment = dbContext.Payments
                    .AsNoTracking()
                    .First(p => p.Id == paymentResult.PaymentId);

                mockAuditService.Received(1).LogAsync(Arg.Is<AuditEntry>(e =>
                    e.Action == "payment.refunded" &&
                    e.ResourceType == "Payment" &&
                    e.ResourceId == paymentResult.PaymentId
                ));

                return updatedPayment.Status == PaymentStatus.Refunded &&
                       refundResult.Status == "refunded";
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Partial_Refund_Arithmetic_Should_Calculate_Remaining_Balance_Correctly()
    {
        Prop.ForAll(
            GenerateValidCardPaymentRequest(),
            GeneratePartialRefundRequest(),
            (paymentRequest, refundRequest) =>
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

                var mockProviderAdapter = Substitute.For<IProviderAdapter>();
                mockProviderAdapter.ProviderName.Returns("pagarme");
                mockProviderAdapter.AuthorizeAsync(Arg.Any<AuthorizationRequest>())
                    .Returns(Task.FromResult(new AuthorizationResult
                    {
                        Success = true,
                        ProviderTransactionId = Guid.NewGuid().ToString(),
                        ProviderPaymentId = Guid.NewGuid().ToString(),
                        Status = "authorized",
                        CardLastFourDigits = "1234",
                        CardBrand = "visa"
                    }));
                mockProviderAdapter.RefundAsync(Arg.Any<string>(), Arg.Any<long>())
                    .Returns(Task.FromResult(new RefundResult
                    {
                        Success = true,
                        ProviderRefundId = Guid.NewGuid().ToString(),
                        Status = "refunded"
                    }));

                var mockProviderFactory = Substitute.For<IProviderFactory>();
                mockProviderFactory.GetProviderForPaymentMethod(Arg.Any<PaymentMethod>())
                    .Returns(mockProviderAdapter);
                mockProviderFactory.GetProvider(Arg.Any<string>())
                    .Returns(mockProviderAdapter);

                var mockAuditService = Substitute.For<IAuditService>();
                mockAuditService.LogAsync(Arg.Any<AuditEntry>())
                    .Returns(Task.CompletedTask);

                var service = new PaymentService(dbContext, mockProviderFactory, _encryptionService, mockAuditService);

                var paymentResult = service.CreatePaymentAsync(paymentRequest, merchantId).Result;

                var payment = dbContext.Payments.First(p => p.Id == paymentResult.PaymentId);
                payment.Status = PaymentStatus.Paid;
                dbContext.SaveChanges();

                var originalAmount = payment.AmountCents;
                var refundAmount = (long)(originalAmount * refundRequest.RefundPercentage);
                refundAmount = Math.Max(1, Math.Min(refundAmount, originalAmount - 1));

                var adjustedRefundRequest = new RefundRequest
                {
                    AmountCents = refundAmount,
                    Reason = "Partial refund"
                };

                var refundResult = service.RefundPaymentAsync(paymentResult.PaymentId, adjustedRefundRequest, merchantId).Result;

                var totalRefunded = dbContext.Transactions
                    .Where(t => t.PaymentId == paymentResult.PaymentId &&
                               t.Type == TransactionType.Refund &&
                               t.Status == TransactionStatus.Success)
                    .Sum(t => t.AmountCents);

                var expectedRemainingBalance = originalAmount - totalRefunded;

                var updatedPayment = dbContext.Payments
                    .AsNoTracking()
                    .First(p => p.Id == paymentResult.PaymentId);

                return totalRefunded == refundAmount &&
                       expectedRemainingBalance == (originalAmount - refundAmount) &&
                       updatedPayment.Status != PaymentStatus.Refunded;
            }
        ).QuickCheckThrowOnFailure();
    }

    private static Arbitrary<RefundRequest> GenerateValidRefundRequest()
    {
        var gen = from amountCents in Gen.Choose(100, 100000)
                  from reason in Gen.Elements("Customer requested", "Duplicate charge", "Product not delivered", "Quality issue")
                  select new RefundRequest
                  {
                      AmountCents = amountCents,
                      Reason = reason
                  };

        return Arb.From(gen);
    }

    private static Arbitrary<PartialRefundRequest> GeneratePartialRefundRequest()
    {
        var gen = from percentage in Gen.Choose(10, 90)
                  select new PartialRefundRequest
                  {
                      RefundPercentage = percentage / 100.0
                  };

        return Arb.From(gen);
    }
}

public class PartialRefundRequest
{
    public double RefundPercentage { get; set; }
}
