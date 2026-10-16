using System.Security.Claims;
using Hr.Api.Dtos;
using Hr.Api.Security;
using Hr.Core.Domain;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Hr.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IAntiforgery antiforgery) : ControllerBase
{
    /// <summary>Wystawia ciasteczka antyforgery; wymagane przed logowaniem.</summary>
    [AllowAnonymous]
    [HttpGet("antiforgery", Name = "Auth_Antiforgery")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult GetAntiforgery()
    {
        AntiforgeryCookie.Issue(HttpContext, antiforgery);
        return NoContent();
    }

    /// <summary>Loguje lokalnym kontem; zły login i złe hasło dają tę samą odpowiedź 401, blokada konta 423.</summary>
    [AllowAnonymous]
    [HttpPost("login", Name = "Auth_Login")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked, "application/problem+json")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await signInManager.PasswordSignInAsync(
            request.UserName ?? string.Empty, request.Password ?? string.Empty, isPersistent: false, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return ProblemWithCode(StatusCodes.Status423Locked, "LockedOut", "Konto jest czasowo zablokowane.", "Zbyt wiele nieudanych prób logowania. Spróbuj później.");
        }

        if (!result.Succeeded)
        {
            return ProblemWithCode(StatusCodes.Status401Unauthorized, "InvalidCredentials", "Nieprawidłowe dane logowania.", "Login lub hasło są nieprawidłowe.");
        }

        var user = (await userManager.FindByNameAsync(request.UserName ?? string.Empty))!;
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        HttpContext.User = principal;
        AntiforgeryCookie.Issue(HttpContext, antiforgery);
        return Ok(ToDto(principal));
    }

    [HttpPost("logout", Name = "Auth_Logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        // Zmiana stempla bezpieczeństwa unieważnia też skopiowane ciasteczko sesji (samo SignOut usuwa je tylko u klienta).
        // Skutek uboczny: wylogowanie kończy sesje tego użytkownika na wszystkich urządzeniach (ADR-25).
        if (await userManager.GetUserAsync(User) is { } user)
        {
            await userManager.UpdateSecurityStampAsync(user);
        }

        await signInManager.SignOutAsync();
        HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        AntiforgeryCookie.Issue(HttpContext, antiforgery);
        return NoContent();
    }

    [HttpGet("me", Name = "Auth_Me")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    public ActionResult<CurrentUserDto> Me() => Ok(ToDto(User));

    private static CurrentUserDto ToDto(ClaimsPrincipal principal)
    {
        string userName = principal.Identity?.Name ?? string.Empty;
        return new CurrentUserDto(
            Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!),
            Guid.TryParse(principal.FindFirstValue(HttpContextCurrentUser.EmployeeIdClaim), out var employeeId) ? employeeId : null,
            userName,
            principal.FindFirstValue(HrClaimsPrincipalFactory.DisplayNameClaim) ?? userName,
            principal.FindAll(ClaimTypes.Role).Select(c => c.Value).OrderBy(r => r).ToList());
    }

    private ObjectResult ProblemWithCode(int status, string code, string title, string detail)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, status, title, detail: detail);
        problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
