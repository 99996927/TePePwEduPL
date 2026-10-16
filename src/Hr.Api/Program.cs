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
using Microsoft.OpenApi.Models;
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
builder.Services.AddScoped<AuditService>();

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
// Stempel bezpieczeństwa sprawdzany przy każdym żądaniu, aby wylogowanie działało natychmiast (ADR-10, ADR-25).
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
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

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Asystent kadrowo-płacowy API";
        document.Info.Version = "v1";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["cookieAuth"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = "hr.session",
            Description = "Cookie sesyjne po zalogowaniu (POST /api/auth/login). Metody zapisu wymagają też nagłówka X-XSRF-TOKEN z wartością ciasteczka XSRF-TOKEN.",
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, _) =>
    {
        bool anonymous = context.Description.ActionDescriptor.EndpointMetadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any();
        if (!anonymous)
        {
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "cookieAuth" } }] = new List<string>(),
            });
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Brak sesji: zaloguj się (bez ciała odpowiedzi)." });
        }

        if (context.Description.HttpMethod is not ("GET" or "HEAD" or "OPTIONS"))
        {
            operation.Responses.TryAdd("400", new OpenApiResponse
            {
                Description = "Brak lub niepoprawny token antyforgery (code = AntiforgeryTokenInvalid) albo niepoprawne żądanie.",
                Content = { ["application/problem+json"] = new OpenApiMediaType { Schema = new OpenApiSchema { Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = "ProblemDetails" } } } },
            });
        }

        return Task.CompletedTask;
    });
});

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
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapFallbackToFile("/{**path:regex(^(?!api/|test/|openapi/).*$)}", "index.html").AllowAnonymous();

app.Run();

public partial class Program
{
}
