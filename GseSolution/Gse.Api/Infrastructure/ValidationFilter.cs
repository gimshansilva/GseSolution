using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Gse.Api.Infrastructure;

/// <summary>
/// Runs any registered FluentValidation validator for each action argument and returns an
/// RFC 7807 validation problem (400). Replaces the deprecated FluentValidation.AspNetCore package.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(arg.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(arg), context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                context.Result = new BadRequestObjectResult(
                    new ValidationProblemDetails(result.ToDictionary()) { Status = StatusCodes.Status400BadRequest });
                return;
            }
        }
        await next();
    }
}
