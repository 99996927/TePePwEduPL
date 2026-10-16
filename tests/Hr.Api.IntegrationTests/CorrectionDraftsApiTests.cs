using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hr.Api.IntegrationTests;

[Collection("api")]
public class CorrectionDraftsApiTests(MsSqlFixture fixture)
{
    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<Guid> AnnaEntryAsync(ApiSession anna) =>
        (await JsonOf(await anna.PostAsync("/api/timesheet/entries", new { start = "2026-10-12T08:00:00", end = "2026-10-12T16:00:00", breakMinutes = 0 })))
        .GetProperty("id").GetGuid();

    private static Task<HttpResponseMessage> CreateDraft(ApiSession s, Guid entryId, string start = "2026-10-12T09:00:00", string end = "2026-10-12T17:00:00") =>
        s.PostAsync("/api/correction-drafts", new { workEntryId = entryId, start, end, breakMinutes = 30, reason = "Korekta godzin" });

    [Fact]
    public async Task UC26_HR_lists_and_creates_drafts()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        var entryId = await AnnaEntryAsync(anna);

        var created = await CreateDraft(piotr, entryId);
        var list = await JsonOf(await piotr.GetAsync("/api/correction-drafts?status=Pending"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await JsonOf(created);
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.Equal(30, body.GetProperty("proposedBreakMinutes").GetInt32());
        Assert.Equal(body.GetProperty("id").GetGuid(), list.EnumerateArray().Single().GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task UC26_Employee_gets_403_on_draft_endpoints()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var entryId = await AnnaEntryAsync(anna);

        Assert.Equal(HttpStatusCode.Forbidden, (await anna.GetAsync("/api/correction-drafts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateDraft(anna, entryId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await anna.PostAsync($"/api/correction-drafts/{Guid.NewGuid()}/decision", new { approve = true })).StatusCode);
    }

    [Fact]
    public async Task UC11_HR_approves_draft_via_api_and_entry_changes()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        var entryId = await AnnaEntryAsync(anna);
        var draftId = (await JsonOf(await CreateDraft(piotr, entryId))).GetProperty("id").GetGuid();

        var decided = await piotr.PostAsync($"/api/correction-drafts/{draftId}/decision", new { approve = true });
        var entries = await JsonOf(await anna.GetAsync("/api/timesheet/entries?from=2026-10-12&to=2026-10-12"));

        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
        Assert.Equal("Approved", (await JsonOf(decided)).GetProperty("status").GetString());
        var entry = entries.EnumerateArray().Single();
        Assert.Equal("2026-10-12T09:00:00", entry.GetProperty("start").GetString());
        Assert.Equal(30, entry.GetProperty("breakMinutes").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await piotr.PostAsync($"/api/correction-drafts/{draftId}/decision", new { approve = false, note = "Nie" })).StatusCode);
    }

    [Fact]
    public async Task UC26_Zoned_start_time_is_422()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var anna = await ApiSession.LoginAsAsync(factory, "anna.nowak");
        var piotr = await ApiSession.LoginAsAsync(factory, "piotr.zielinski");
        var entryId = await AnnaEntryAsync(anna);

        var response = await CreateDraft(piotr, entryId, "2026-10-12T09:00:00Z", "2026-10-12T17:00:00Z");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty((await JsonOf(await piotr.GetAsync("/api/correction-drafts"))).EnumerateArray());
    }

    [Fact]
    public async Task UC26_Unauthenticated_is_401()
    {
        await using var factory = await HrApiFactory.CreateAsync(fixture);
        var session = await ApiSession.StartAsync(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/correction-drafts")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.PostAsync("/api/correction-drafts", new { })).StatusCode);
    }
}
