using Microsoft.AspNetCore.Antiforgery;

namespace Hr.Api.Security;

/// <summary>Wystawia token antyforgery w ciasteczku <c>XSRF-TOKEN</c> czytelnym dla JavaScript (Angular odsyła go w <c>X-XSRF-TOKEN</c>).</summary>
public static class AntiforgeryCookie
{
    public const string CookieName = "XSRF-TOKEN";
    public const string HeaderName = "X-XSRF-TOKEN";

    public static void Issue(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append(CookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps,
            Path = "/",
            IsEssential = true,
        });
    }
}
