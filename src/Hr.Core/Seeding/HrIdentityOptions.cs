using Microsoft.AspNetCore.Identity;

namespace Hr.Core.Seeding;

/// <summary>Polityka haseł i blokady konta środowiska demo (ADR-06). Niedopuszczalna produkcyjnie.</summary>
public static class HrIdentityOptions
{
    public static void Apply(IdentityOptions options)
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredUniqueChars = 1;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

        options.User.RequireUniqueEmail = true;
    }
}
