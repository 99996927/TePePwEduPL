using System.Text.Json.Serialization;
using Hr.Api.Security;
using Hr.Core.Abstractions;
using Hr.Core.Auditing;
using Hr.Core.Domain;
using Hr.Core.Persistence;
using Hr.Core.Seeding;
using Hr.Core.Services;
using Hr.Rules.Calendar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Hr")
    ?? throw new InvalidOperationException("Brak konfiguracji ConnectionStrings:Hr.");

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<WorkCalendar>();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<HrDbContext>((sp, options) => options
    .UseSqlServer(connectionString)
    .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

builder.Services.AddScoped<EmployeeDirectoryService>();
builder.Services.AddScoped<LeaveService>();
builder.Services.AddScoped<TimesheetService>();
builder.Services.AddScoped<PayrollChangeService>();
builder.Services.AddScoped<PayrollService>();

builder.Services.AddIdentityCore<ApplicationUser>(HrIdentityOptions.Apply)
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<HrDbContext>()
    .AddSignInManager()
    .AddClaimsPrincipalFactory<HrClaimsPrincipalFactory>();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "hr.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = AntiforgeryCookie.HeaderName;
    options.Cookie.Name = "hr.antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddControllers(options =>
    {
        options.Filters.Add<AntiforgeryValidationFilter>();
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<HrDbContext>().Database.MigrateAsync();
}

if (app.Configuration.GetValue<bool>("Hr:SeedDemoData"))
{
    await DemoSeeder.SeedAsync(app.Services);
}

app.UseExceptionHandler(errors => errors.Run(context =>
    Results.Problem(
        statusCode: StatusCodes.Status500InternalServerError,
        title: "Wystąpił błąd serwera.",
        extensions: new Dictionary<string, object?> { ["code"] = "ServerError" }).ExecuteAsync(context)));
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("/{**path:regex(^(?!api/|test/).*$)}", "index.html").AllowAnonymous();

app.Run();

public partial class Program
{
}
