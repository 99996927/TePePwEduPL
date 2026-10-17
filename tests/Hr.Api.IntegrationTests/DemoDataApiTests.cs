using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hr.Core.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hr.Api.IntegrationTests;

/// <summary>Dane demo profilu <c>Full</c> widziane przez API (UC-29): konta, saldo, naliczenia, zapisane rozmowy i walidacja kotwicy.</summary>
[Collection("api")]
public class DemoDataApiTests(MsSqlFixture fixture)
{
    private static readonly string[] NamedLogins =
        ["anna.nowak", "jan.kowalski", "tomasz.lis", "ewa.wisniewska", "piotr.zielinski", "maria.kaminska"];

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<JsonElement> MeOf(ApiSession session) => await JsonOf(await session.GetAsync("/api/auth/me"));

    private Task<HrApiFactory> FullAsync() => HrApiFactory.CreateAsync(fixture, demoProfile: "Full");

    [Fact]
    public async Task UC29_Full_profile_serves_logins_for_named_and_generated_accounts()
    {
        await using var factory = await FullAsync();
        string generated;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            generated = await users.Users.Where(u => !NamedLogins.Contains(u.UserName!)).OrderBy(u => u.UserName).Select(u => u.UserName!).FirstAsync();
        }

        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var other = await ApiSession.LoginAsAsync(factory, generated);

        Assert.NotEqual(Guid.Empty, (await MeOf(anna)).GetProperty("employeeId").GetGuid());
        Assert.NotEqual(Guid.Empty, (await MeOf(other)).GetProperty("employeeId").GetGuid());
    }

    [Fact]
    public async Task UC05_Annas_balance_is_20_10_5_5()
    {
        await using var factory = await FullAsync();
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");

        var balance = await JsonOf(await anna.GetAsync("/api/leave/balance"));

        Assert.Equal(20, balance.GetProperty("entitlementDays").GetInt32());
        Assert.Equal(10, balance.GetProperty("approvedDays").GetInt32());
        Assert.Equal(5, balance.GetProperty("pendingDays").GetInt32());
        Assert.Equal(5, balance.GetProperty("remainingDays").GetInt32());
    }

    [Fact]
    public async Task UC17_Anna_sees_only_her_payroll_for_september_and_none_for_october()
    {
        await using var factory = await FullAsync();
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var jan = await ApiSession.LoginAsAsync(factory, "jan.kowalski");
        var annaId = (await MeOf(anna)).GetProperty("employeeId").GetGuid();
        var janId = (await MeOf(jan)).GetProperty("employeeId").GetGuid();

        var september = await anna.GetAsync($"/api/payroll/results?employeeId={annaId}&year=2026&month=9");
        var october = await anna.GetAsync($"/api/payroll/results?employeeId={annaId}&year=2026&month=10");
        var foreign = await anna.GetAsync($"/api/payroll/results?employeeId={janId}&year=2026&month=9");

        Assert.Equal(HttpStatusCode.OK, september.StatusCode);
        Assert.Equal(annaId, (await JsonOf(september)).GetProperty("employeeId").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, october.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task UC31_HR_can_open_Annas_seeded_conversation_and_read_is_audited()
    {
        await using var factory = await FullAsync();
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        var annaUserId = (await MeOf(anna)).GetProperty("userId").GetGuid();
        var piotrUserId = (await MeOf(piotr)).GetProperty("userId").GetGuid();

        var all = await JsonOf(await piotr.GetAsync("/api/conversations"));
        var id = all.EnumerateArray().Single(c => c.GetProperty("userId").GetGuid() == annaUserId).GetProperty("id").GetGuid();
        var detail = await piotr.GetAsync($"/api/conversations/{id}");
        var audit = await JsonOf(await piotr.GetAsync($"/api/audit?entityType=Conversation&entityId={id}"));

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.NotEmpty((await JsonOf(detail)).GetProperty("messages").EnumerateArray());
        Assert.Contains(audit.GetProperty("items").EnumerateArray(), a =>
            a.GetProperty("eventType").GetString() == "ConversationRead" && a.GetProperty("actorUserId").GetGuid() == piotrUserId);
    }

    [Fact]
    public async Task UC26_Piotr_sees_one_pending_correction_draft_for_Anna()
    {
        await using var factory = await FullAsync();
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        var annaId = (await MeOf(anna)).GetProperty("employeeId").GetGuid();

        var drafts = await JsonOf(await piotr.GetAsync("/api/correction-drafts"));

        var draft = Assert.Single(drafts.EnumerateArray());
        Assert.Equal("Pending", draft.GetProperty("status").GetString());
        Assert.Equal(annaId, draft.GetProperty("employeeId").GetGuid());
    }

    [Fact]
    public async Task Startup_fails_with_polish_message_for_non_working_day_anchor()
    {
        await using var factory = await FullAsync();
        await using var saturday = factory.WithWebHostBuilder(b => b.UseSetting("Hr:DemoAnchorDate", "2026-10-17"));

        var error = Record.Exception(() => saturday.Services);

        Assert.NotNull(error);
        var messages = Flatten(error).Select(e => e.Message).ToList();
        Assert.Contains(messages, m => m.Contains("Hr:DemoAnchorDate: 2026-10-17 musi być dniem roboczym", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Full_profile_startup_with_audit_interceptor_completes_in_under_60_seconds()
    {
        var connectionString = await fixture.NewDatabaseAsync();
        var stopwatch = Stopwatch.StartNew();
        await using var factory = new HrApiFactory(connectionString, demoProfile: "Full");

        await ApiSession.LoginAsAsync(factory, "anna.nowak");
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(60), $"Start z profilem Full trwał {stopwatch.Elapsed}.");
    }

    private static IEnumerable<Exception> Flatten(Exception error)
    {
        for (Exception? e = error; e is not null; e = e.InnerException)
        {
            yield return e;
            if (e is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions.SelectMany(Flatten))
                {
                    yield return inner;
                }
            }
        }
    }
}
