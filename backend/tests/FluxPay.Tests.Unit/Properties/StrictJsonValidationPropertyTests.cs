using FsCheck;
using FsCheck.Xunit;
using FluxPay.Api.Controllers;
using FluxPay.Api.Filters;
using FluxPay.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text;
using System.Text.Json;

namespace FluxPay.Tests.Unit.Properties;

public class StrictJsonValidationPropertyTests
{
    [Property(MaxTest = 10)]
    public void Request_With_Unexpected_Fields_Should_Be_Rejected(NonEmptyString unexpectedField)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(unexpectedField.Get)),
            fieldName =>
            {
                if (string.IsNullOrWhiteSpace(fieldName) || 
                    fieldName.ToLowerInvariant() == "amountcents" ||
                    fieldName.ToLowerInvariant() == "method" ||
                    fieldName.ToLowerInvariant() == "cardtoken" ||
                    fieldName.ToLowerInvariant() == "customer" ||
                    fieldName.ToLowerInvariant() == "metadata")
                {
                    return true;
                }

                var paymentService = Substitute.For<IPaymentService>();
                var logger = Substitute.For<ILogger<PaymentsController>>();
                var controller = new PaymentsController(paymentService, logger);

                var httpContext = new DefaultHttpContext();
                var requestBody = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["amount_cents"] = 10000,
                    ["method"] = "credit_card",
                    ["card_token"] = "tok_test123",
                    ["customer"] = new Dictionary<string, string>
                    {
                        ["name"] = "Test User",
                        ["email"] = "test@example.com",
                        ["document"] = "12345678900"
                    },
                    [fieldName] = "unexpected_value"
                });

                var bodyBytes = Encoding.UTF8.GetBytes(requestBody);
                httpContext.Request.Body = new MemoryStream(bodyBytes);
                httpContext.Request.ContentType = "application/json";
                httpContext.Request.ContentLength = bodyBytes.Length;

                var actionContext = new ActionContext(
                    httpContext,
                    new RouteData(),
                    new ActionDescriptor()
                    {
                        Parameters = new List<ParameterDescriptor>
                        {
                            new ParameterDescriptor
                            {
                                Name = "request",
                                ParameterType = typeof(CreatePaymentRequest)
                            }
                        }
                    }
                );

                var actionExecutingContext = new ActionExecutingContext(
                    actionContext,
                    new List<IFilterMetadata>(),
                    new Dictionary<string, object?>
                    {
                        ["request"] = JsonSerializer.Deserialize<CreatePaymentRequest>(requestBody)
                    },
                    controller
                );

                var filter = new StrictJsonValidationFilter();
                filter.OnActionExecuting(actionExecutingContext);

                return actionExecutingContext.Result is BadRequestObjectResult;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Request_With_Only_Expected_Fields_Should_Pass_Validation(PositiveInt amountCents)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(amountCents.Get)),
            amount =>
            {
                if (amount <= 0) return true;

                var paymentService = Substitute.For<IPaymentService>();
                var logger = Substitute.For<ILogger<PaymentsController>>();
                var controller = new PaymentsController(paymentService, logger);

                var httpContext = new DefaultHttpContext();
                var requestBody = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["amount_cents"] = amount,
                    ["method"] = "credit_card",
                    ["card_token"] = "tok_test123",
                    ["customer"] = new Dictionary<string, string>
                    {
                        ["name"] = "Test User",
                        ["email"] = "test@example.com",
                        ["document"] = "12345678900"
                    }
                });

                var bodyBytes = Encoding.UTF8.GetBytes(requestBody);
                httpContext.Request.Body = new MemoryStream(bodyBytes);
                httpContext.Request.ContentType = "application/json";
                httpContext.Request.ContentLength = bodyBytes.Length;

                var actionContext = new ActionContext(
                    httpContext,
                    new RouteData(),
                    new ActionDescriptor()
                    {
                        Parameters = new List<ParameterDescriptor>
                        {
                            new ParameterDescriptor
                            {
                                Name = "request",
                                ParameterType = typeof(CreatePaymentRequest)
                            }
                        }
                    }
                );

                var actionExecutingContext = new ActionExecutingContext(
                    actionContext,
                    new List<IFilterMetadata>(),
                    new Dictionary<string, object?>
                    {
                        ["request"] = JsonSerializer.Deserialize<CreatePaymentRequest>(requestBody)
                    },
                    controller
                );

                var filter = new StrictJsonValidationFilter();
                filter.OnActionExecuting(actionExecutingContext);

                return actionExecutingContext.Result == null;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Refund_Request_With_Unexpected_Fields_Should_Be_Rejected(NonEmptyString unexpectedField)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(unexpectedField.Get)),
            fieldName =>
            {
                if (string.IsNullOrWhiteSpace(fieldName) || 
                    fieldName.ToLowerInvariant() == "amountcents" ||
                    fieldName.ToLowerInvariant() == "reason")
                {
                    return true;
                }

                var paymentService = Substitute.For<IPaymentService>();
                var logger = Substitute.For<ILogger<PaymentsController>>();
                var controller = new PaymentsController(paymentService, logger);

                var httpContext = new DefaultHttpContext();
                var requestBody = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["amount_cents"] = 5000,
                    ["reason"] = "Customer request",
                    [fieldName] = "unexpected_value"
                });

                var bodyBytes = Encoding.UTF8.GetBytes(requestBody);
                httpContext.Request.Body = new MemoryStream(bodyBytes);
                httpContext.Request.ContentType = "application/json";
                httpContext.Request.ContentLength = bodyBytes.Length;

                var actionContext = new ActionContext(
                    httpContext,
                    new RouteData(),
                    new ActionDescriptor()
                    {
                        Parameters = new List<ParameterDescriptor>
                        {
                            new ParameterDescriptor
                            {
                                Name = "request",
                                ParameterType = typeof(RefundRequest)
                            }
                        }
                    }
                );

                var actionExecutingContext = new ActionExecutingContext(
                    actionContext,
                    new List<IFilterMetadata>(),
                    new Dictionary<string, object?>
                    {
                        ["request"] = JsonSerializer.Deserialize<RefundRequest>(requestBody)
                    },
                    controller
                );

                var filter = new StrictJsonValidationFilter();
                filter.OnActionExecuting(actionExecutingContext);

                return actionExecutingContext.Result is BadRequestObjectResult;
            }
        ).QuickCheckThrowOnFailure();
    }
}
