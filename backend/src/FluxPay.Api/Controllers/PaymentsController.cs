using FluxPay.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace FluxPay.Api.Controllers;

[ApiController]
[Route("v1/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
    {
        try
        {
            var merchantId = HttpContext.Items["MerchantId"] as Guid?;
            if (!merchantId.HasValue)
            {
                return Unauthorized(new
                {
                    error = new
                    {
                        code = "UNAUTHORIZED",
                        message = "Merchant authentication required"
                    }
                });
            }

            var result = await _paymentService.CreatePaymentAsync(request, merchantId.Value);

            var statusCode = result.Method == Core.Entities.PaymentMethod.CreditCard || 
                           result.Method == Core.Entities.PaymentMethod.DebitCard ? 202 : 201;

            return StatusCode(statusCode, new
            {
                payment_id = result.PaymentId,
                status = result.Status.ToString().ToLowerInvariant(),
                amount_cents = result.AmountCents,
                method = result.Method.ToString().ToLowerInvariant(),
                pix = result.Pix != null ? new
                {
                    qr_code = result.Pix.QrCode,
                    qr_code_url = result.Pix.QrCodeUrl,
                    expires_at = result.Pix.ExpiresAt
                } : null,
                boleto = result.Boleto != null ? new
                {
                    barcode = result.Boleto.Barcode,
                    digitable_line = result.Boleto.DigitableLine,
                    pdf_url = result.Boleto.PdfUrl,
                    expires_at = result.Boleto.ExpiresAt
                } : null,
                created_at = result.CreatedAt
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                error = new
                {
                    code = "INVALID_REQUEST",
                    message = ex.Message
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new
            {
                error = new
                {
                    code = "PAYMENT_FAILED",
                    message = ex.Message
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment");
            return StatusCode(500, new
            {
                error = new
                {
                    code = "INTERNAL_ERROR",
                    message = "An error occurred while processing the payment"
                }
            });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPayment(Guid id)
    {
        try
        {
            var merchantId = HttpContext.Items["MerchantId"] as Guid?;
            if (!merchantId.HasValue)
            {
                return Unauthorized(new
                {
                    error = new
                    {
                        code = "UNAUTHORIZED",
                        message = "Merchant authentication required"
                    }
                });
            }

            var payment = await _paymentService.GetPaymentAsync(id, merchantId.Value);

            return Ok(new
            {
                payment_id = payment.Id,
                status = payment.Status.ToString().ToLowerInvariant(),
                amount_cents = payment.AmountCents,
                method = payment.Method.ToString().ToLowerInvariant(),
                customer = payment.Customer != null ? new
                {
                    name = payment.Customer.Name,
                    email = "***"
                } : null,
                transactions = payment.Transactions?.Select(t => new
                {
                    id = t.Id,
                    type = t.Type.ToString().ToLowerInvariant(),
                    status = t.Status.ToString().ToLowerInvariant(),
                    amount_cents = t.AmountCents,
                    created_at = t.CreatedAt
                }).ToList(),
                created_at = payment.CreatedAt,
                updated_at = payment.UpdatedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new
            {
                error = new
                {
                    code = "PAYMENT_NOT_FOUND",
                    message = ex.Message
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment {PaymentId}", id);
            return StatusCode(500, new
            {
                error = new
                {
                    code = "INTERNAL_ERROR",
                    message = "An error occurred while retrieving the payment"
                }
            });
        }
    }

    [HttpPost("{id}/refund")]
    public async Task<IActionResult> RefundPayment(Guid id, [FromBody] RefundRequest request)
    {
        try
        {
            var merchantId = HttpContext.Items["MerchantId"] as Guid?;
            if (!merchantId.HasValue)
            {
                return Unauthorized(new
                {
                    error = new
                    {
                        code = "UNAUTHORIZED",
                        message = "Merchant authentication required"
                    }
                });
            }

            var result = await _paymentService.RefundPaymentAsync(id, request, merchantId.Value);

            return Ok(new
            {
                refund_id = result.RefundId,
                payment_id = result.PaymentId,
                amount_cents = result.AmountCents,
                status = result.Status.ToLowerInvariant(),
                created_at = result.CreatedAt
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                error = new
                {
                    code = "INVALID_REQUEST",
                    message = ex.Message
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new
            {
                error = new
                {
                    code = "REFUND_FAILED",
                    message = ex.Message
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refunding payment {PaymentId}", id);
            return StatusCode(500, new
            {
                error = new
                {
                    code = "INTERNAL_ERROR",
                    message = "An error occurred while processing the refund"
                }
            });
        }
    }
}
