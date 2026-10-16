using System.Net.Http.Json;

namespace Hr.Api.IntegrationTests;

/// <summary>Klient HTTP z ciasteczkami i tokenem antyforgery: dokłada <c>X-XSRF-TOKEN</c> i odświeża token po każdej odpowiedzi, która go ustawia.</summary>
public sealed class ApiSession
{
    private string? _token;

    private ApiSession(HttpClient client) => Client = client;

    public HttpClient Client { get; }

    public static async Task<ApiSession> StartAsync(HrApiFactory factory)
    {
        var session = new ApiSession(factory.CreateApiClient());
        await session.RefreshTokenAsync();
        return session;
    }

    /// <summary>Hasło konta demo z docs/arc42/07-widok-wdrozenia.md: login bez kropki i „123”.</summary>
    public static string PasswordOf(string login) => login.Replace(".", string.Empty) + "123";

    public static async Task<ApiSession> LoginAsAsync(HrApiFactory factory, string login)
    {
        var session = await StartAsync(factory);
        var response = await session.PostAsync("/api/auth/login", new { userName = login, password = PasswordOf(login) });
        Assert.True(response.IsSuccessStatusCode, $"Logowanie {login} zwróciło {(int)response.StatusCode}");
        return session;
    }

    public async Task RefreshTokenAsync() => Capture(await Client.GetAsync("/api/auth/antiforgery"));

    public Task<HttpResponseMessage> GetAsync(string path) => SendAsync(HttpMethod.Get, path, null);

    public Task<HttpResponseMessage> PostAsync(string path, object? body = null) => SendAsync(HttpMethod.Post, path, body);

    public Task<HttpResponseMessage> PutAsync(string path, object? body = null) => SendAsync(HttpMethod.Put, path, body);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, bool withToken = true)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (withToken && _token is not null)
        {
            request.Headers.Add("X-XSRF-TOKEN", _token);
        }

        var response = await Client.SendAsync(request);
        Capture(response);
        return response;
    }

    private void Capture(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return;
        }

        foreach (var cookie in cookies)
        {
            const string prefix = "XSRF-TOKEN=";
            if (cookie.StartsWith(prefix, StringComparison.Ordinal))
            {
                _token = Uri.UnescapeDataString(cookie[prefix.Length..].Split(';')[0]);
            }
        }
    }
}
