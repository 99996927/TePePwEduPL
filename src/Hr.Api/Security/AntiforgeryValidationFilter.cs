using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Hr.Api.Security;

/// <summary>
/// Waliduje token antyforgery (nagłówek <c>X-XSRF-TOKEN</c> + ciasteczko) dla metod innych niż GET, HEAD, OPTIONS i TRACE.
/// Brak lub zły token daje 400 ProblemDetails z kodem <c>AntiforgeryTokenInvalid</c>.
/// </summary>
public sealed class AntiforgeryValidationFilter(IAntiforgery antiforgery, ProblemDetailsFactory problemFactory) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        string method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            var problem = problemFactory.CreateProblemDetails(
                context.HttpContext, StatusCodes.Status400BadRequest, "Nieprawidłowy token antyforgery.",
                detail: "Brak lub niepoprawny token antyforgery w nagłówku X-XSRF-TOKEN.");
            problem.Extensions["code"] = "AntiforgeryTokenInvalid";
            context.Result = new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { "application/problem+json" },
            };
        }
    }
}
