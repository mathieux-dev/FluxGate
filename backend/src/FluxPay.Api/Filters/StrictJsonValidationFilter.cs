using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FluxPay.Api.Filters;

public class StrictJsonValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.Request.ContentType?.Contains("application/json") == true &&
            context.HttpContext.Request.ContentLength > 0)
        {
            context.HttpContext.Request.EnableBuffering();
            context.HttpContext.Request.Body.Position = 0;

            using var reader = new StreamReader(context.HttpContext.Request.Body, leaveOpen: true);
            var body = reader.ReadToEnd();
            context.HttpContext.Request.Body.Position = 0;

            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    var jsonDoc = JsonDocument.Parse(body);
                    var actionParameters = context.ActionDescriptor.Parameters;

                    foreach (var parameter in actionParameters)
                    {
                        if (parameter.ParameterType.IsClass && 
                            parameter.ParameterType != typeof(string) &&
                            context.ActionArguments.TryGetValue(parameter.Name, out var value) &&
                            value != null)
                        {
                            var expectedProperties = parameter.ParameterType.GetProperties()
                                .Select(p => ToSnakeCase(p.Name).ToLowerInvariant())
                                .ToHashSet();

                            var actualProperties = jsonDoc.RootElement.EnumerateObject()
                                .Select(p => p.Name.ToLowerInvariant())
                                .ToHashSet();

                            var unexpectedFields = actualProperties.Except(expectedProperties).ToList();

                            if (unexpectedFields.Any())
                            {
                                context.Result = new BadRequestObjectResult(new
                                {
                                    error = new
                                    {
                                        code = "UNEXPECTED_FIELDS",
                                        message = "Request contains unexpected JSON fields",
                                        details = new
                                        {
                                            unexpected_fields = unexpectedFields
                                        }
                                    }
                                });
                                return;
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                }
            }
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    private static string ToSnakeCase(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return Regex.Replace(text, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
    }
}
