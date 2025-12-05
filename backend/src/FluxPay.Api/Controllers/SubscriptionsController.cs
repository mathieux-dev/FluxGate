using FluxPay.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace FluxPay.Api.Controllers;

[ApiController]
[Route("v1/[controller]")]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(ISubscriptionService subscriptionService, ILogger<SubscriptionsController> logger)
    {
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateSubscription([FromBody] CreateSubscriptionRequest request)
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

            var result = await _subscriptionService.CreateSubscriptionAsync(request, merchantId.Value);

            return StatusCode(201, new
            {
                subscription_id = result.SubscriptionId,
                provider_subscription_id = result.ProviderSubscriptionId,
                status = result.Status.ToString().ToLowerInvariant(),
                amount_cents = result.AmountCents,
                interval = result.Interval,
                next_billing_date = result.NextBillingDate,
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
                    code = "SUBSCRIPTION_FAILED",
                    message = ex.Message
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating subscription");
            return StatusCode(500, new
            {
                error = new
                {
                    code = "INTERNAL_ERROR",
                    message = "An error occurred while creating the subscription"
                }
            });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetSubscription(Guid id)
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

            var subscription = await _subscriptionService.GetSubscriptionAsync(id, merchantId.Value);

            return Ok(new
            {
                subscription_id = subscription.Id,
                provider_subscription_id = subscription.ProviderSubscriptionId,
                status = subscription.Status.ToString().ToLowerInvariant(),
                amount_cents = subscription.AmountCents,
                interval = subscription.Interval,
                next_billing_date = subscription.NextBillingDate,
                customer = subscription.Customer != null ? new
                {
                    name = subscription.Customer.Name
                } : null,
                created_at = subscription.CreatedAt,
                cancelled_at = subscription.CancelledAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new
            {
                error = new
                {
                    code = "SUBSCRIPTION_NOT_FOUND",
                    message = ex.Message
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving subscription {SubscriptionId}", id);
            return StatusCode(500, new
            {
                error = new
                {
                    code = "INTERNAL_ERROR",
                    message = "An error occurred while retrieving the subscription"
                }
            });
        }
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelSubscription(Guid id)
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

            var result = await _subscriptionService.CancelSubscriptionAsync(id, merchantId.Value);

            return Ok(new
            {
                subscription_id = result.SubscriptionId,
                status = result.Status.ToString().ToLowerInvariant(),
                cancelled_at = result.CancelledAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new
            {
                error = new
                {
                    code = "CANCELLATION_FAILED",
                    message = ex.Message
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling subscription {SubscriptionId}", id);
            return StatusCode(500, new
            {
                error = new
                {
                    code = "INTERNAL_ERROR",
                    message = "An error occurred while cancelling the subscription"
                }
            });
        }
    }
}
