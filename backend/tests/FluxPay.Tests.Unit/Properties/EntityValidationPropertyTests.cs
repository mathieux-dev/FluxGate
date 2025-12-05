using FsCheck;
using FsCheck.Xunit;
using FluxPay.Core.Entities;
using FluxPay.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace FluxPay.Tests.Unit.Properties;

public class EntityValidationPropertyTests
{
    private static FluxPayDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<FluxPayDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        return new FluxPayDbContext(options);
    }

    private static bool ContainsPAN(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        
        var panPattern = @"\b\d{13,19}\b";
        var matches = Regex.Matches(text, panPattern);
        
        foreach (Match match in matches)
        {
            var digits = match.Value;
            if (digits.Length >= 13 && digits.Length <= 19)
            {
                return true;
            }
        }
        
        return false;
    }

    private static bool ContainsCVV(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        
        var cvvKeywords = new[] { "cvv", "cvc", "security_code", "securityCode" };
        var textLower = text.ToLower();
        
        foreach (var keyword in cvvKeywords)
        {
            if (textLower.Contains(keyword))
            {
                var cvvPattern = @"\b\d{3,4}\b";
                if (Regex.IsMatch(text, cvvPattern))
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    private static bool ContainsSensitiveCardData(Payment payment)
    {
        var fieldsToCheck = new[]
        {
            payment.Provider,
            payment.ProviderPaymentId,
            payment.ProviderPayload,
            payment.Metadata
        };

        foreach (var field in fieldsToCheck)
        {
            if (ContainsPAN(field) || ContainsCVV(field))
                return true;
        }

        return false;
    }

    [Property(MaxTest = 10)]
    public void Payment_Should_Never_Store_PAN_Or_CVV(Guid merchantId, int amountCents, PaymentMethod method, PaymentStatus status)
    {
        Prop.ForAll(
            PaymentGenerator(merchantId, amountCents, method, status),
            payment =>
            {
                using var context = CreateInMemoryContext();
                
                var merchant = new Merchant
                {
                    Id = payment.MerchantId,
                    Name = "Test Merchant",
                    Email = "test@merchant.com",
                    ProviderConfigEncrypted = "encrypted_config",
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                context.Merchants.Add(merchant);
                context.Payments.Add(payment);
                context.SaveChanges();
                
                var storedPayment = context.Payments
                    .AsNoTracking()
                    .FirstOrDefault(p => p.Id == payment.Id);
                
                return storedPayment != null && !ContainsSensitiveCardData(storedPayment);
            }
        ).QuickCheckThrowOnFailure();
    }

    private static Arbitrary<Payment> PaymentGenerator(Guid merchantId, int amountCents, PaymentMethod method, PaymentStatus status)
    {
        return Arb.From(
            from provider in Gen.Elements("pagarme", "gerencianet")
            from providerPaymentId in Gen.Elements(
                "tok_abc123",
                "card_tok_xyz789",
                "pix_qr_def456",
                "boleto_ghi789",
                null
            )
            from providerPayload in Gen.Elements(
                "{\"token\":\"tok_abc123\",\"last4\":\"1234\"}",
                "{\"qr_code\":\"00020126...\"}",
                "{\"barcode\":\"34191.79001...\"}",
                null
            )
            from metadata in Gen.Elements(
                "{\"order_id\":\"ORD-12345\"}",
                "{\"customer_ref\":\"CUST-67890\"}",
                null
            )
            select new Payment
            {
                Id = Guid.NewGuid(),
                MerchantId = merchantId,
                CustomerId = null,
                AmountCents = Math.Abs(amountCents % 1000000) + 100,
                Method = method,
                Status = status,
                Provider = provider,
                ProviderPaymentId = providerPaymentId,
                ProviderPayload = providerPayload,
                Metadata = metadata,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );
    }
}
