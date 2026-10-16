using Hr.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hr.Api.Http;

/// <summary>Zamienia wynik serwisu na odpowiedź HTTP (ADR-24, ADR-25): 404, 403, 422, 409 jako ProblemDetails z <c>code</c> i <c>ruleId</c>.</summary>
public static class ServiceResultExtensions
{
    public static IActionResult ToActionResult<T, TDto>(
        this ControllerBase controller, ServiceResult<T> result, Func<T, TDto> map, int successStatus = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return new ObjectResult(map(result.Value!)) { StatusCode = successStatus };
        }

        var error = result.Error!;
        var (status, title) = error.Code switch
        {
            ServiceErrorCode.NotFound => (StatusCodes.Status404NotFound, "Nie znaleziono zasobu."),
            ServiceErrorCode.Forbidden => (StatusCodes.Status403Forbidden, "Brak uprawnień do tej operacji."),
            ServiceErrorCode.Validation => (StatusCodes.Status422UnprocessableEntity, "Naruszenie reguł."),
            _ => (StatusCodes.Status409Conflict, "Konflikt stanu zasobu."),
        };

        var problem = new ProblemDetails { Status = status, Title = title, Detail = error.Message };
        problem.Extensions["code"] = error.Code.ToString();
        if (error.RuleId is not null)
        {
            problem.Extensions["ruleId"] = error.RuleId;
        }

        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
