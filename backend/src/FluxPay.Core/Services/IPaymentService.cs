using FluxPay.Core.Entities;

namespace FluxPay.Core.Services;

public interface IPaymentService
{
    Task<PaymentResult> CreatePaymentAsync(CreatePaymentRequest request, Guid merchantId);
    Task<Payment> GetPaymentAsync(Guid paymentId, Guid merchantId);
    Task<PaymentRefundResult> RefundPaymentAsync(Guid paymentId, RefundRequest request, Guid merchantId);
}

public class CreatePaymentRequest
{
    public long AmountCents { get; set; }
    public PaymentMethod Method { get; set; }
    public string? CardToken { get; set; }
    public CustomerInfo Customer { get; set; } = null!;
    public Dictionary<string, string>? Metadata { get; set; }
}

public class CustomerInfo
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Document { get; set; } = string.Empty;
}

public class PaymentResult
{
    public Guid PaymentId { get; set; }
    public PaymentStatus Status { get; set; }
    public long AmountCents { get; set; }
    public PaymentMethod Method { get; set; }
    public DateTime CreatedAt { get; set; }
    public PixData? Pix { get; set; }
    public BoletoData? Boleto { get; set; }
}

public class PixData
{
    public string QrCode { get; set; } = string.Empty;
    public string? QrCodeUrl { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class BoletoData
{
    public string Barcode { get; set; } = string.Empty;
    public string? DigitableLine { get; set; }
    public string? PdfUrl { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class RefundRequest
{
    public long AmountCents { get; set; }
    public string? Reason { get; set; }
}

public class PaymentRefundResult
{
    public Guid RefundId { get; set; }
    public Guid PaymentId { get; set; }
    public long AmountCents { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
