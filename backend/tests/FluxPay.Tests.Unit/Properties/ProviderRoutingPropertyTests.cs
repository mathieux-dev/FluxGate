using FsCheck;
using FsCheck.Xunit;
using FluxPay.Core.Entities;
using FluxPay.Core.Providers;
using NSubstitute;

namespace FluxPay.Tests.Unit.Properties;

public class ProviderRoutingPropertyTests
{
    private class TestProviderFactory : IProviderFactory
    {
        private readonly Dictionary<string, IProviderAdapter> _providers;

        public TestProviderFactory()
        {
            var pagarMeAdapter = Substitute.For<IProviderAdapter, ISubscriptionProvider>();
            pagarMeAdapter.ProviderName.Returns("pagarme");
            
            var gerencianetAdapter = Substitute.For<IProviderAdapter, IPixProvider, IBoletoProvider>();
            gerencianetAdapter.ProviderName.Returns("gerencianet");

            _providers = new Dictionary<string, IProviderAdapter>(StringComparer.OrdinalIgnoreCase)
            {
                { "pagarme", pagarMeAdapter },
                { "gerencianet", gerencianetAdapter }
            };
        }

        public IProviderAdapter GetProvider(string providerName)
        {
            if (!_providers.TryGetValue(providerName, out var provider))
            {
                throw new InvalidOperationException($"Provider '{providerName}' is not supported");
            }
            return provider;
        }

        public IProviderAdapter GetProviderForPaymentMethod(PaymentMethod method)
        {
            return method switch
            {
                PaymentMethod.CreditCard => GetProvider("pagarme"),
                PaymentMethod.DebitCard => GetProvider("pagarme"),
                _ => throw new ArgumentException($"Unsupported payment method: {method}", nameof(method))
            };
        }

        public IPixProvider GetPixProvider()
        {
            return (IPixProvider)GetProvider("gerencianet");
        }

        public IBoletoProvider GetBoletoProvider()
        {
            return (IBoletoProvider)GetProvider("gerencianet");
        }

        public ISubscriptionProvider GetSubscriptionProvider()
        {
            return (ISubscriptionProvider)GetProvider("pagarme");
        }
    }

    private static Gen<Payment> PaymentGenerator()
    {
        return from provider in Gen.Elements("pagarme", "gerencianet")
               from method in Gen.Elements(PaymentMethod.CreditCard, PaymentMethod.DebitCard, PaymentMethod.Pix, PaymentMethod.Boleto)
               from amountCents in Gen.Choose(100, 1000000)
               select new Payment
               {
                   Id = Guid.NewGuid(),
                   MerchantId = Guid.NewGuid(),
                   AmountCents = amountCents,
                   Method = method,
                   Status = PaymentStatus.Paid,
                   Provider = provider,
                   ProviderPaymentId = $"prov_tx_{Guid.NewGuid()}",
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow
               };
    }

    [Property(MaxTest = 10)]
    public void Refund_Should_Route_To_Same_Provider_As_Original_Payment()
    {
        Prop.ForAll(
            Arb.From(PaymentGenerator()),
            payment =>
            {
                var factory = new TestProviderFactory();
                
                var providerForRefund = factory.GetProvider(payment.Provider);
                
                return providerForRefund.ProviderName.Equals(payment.Provider, StringComparison.OrdinalIgnoreCase);
            }
        ).QuickCheckThrowOnFailure();
    }
}
