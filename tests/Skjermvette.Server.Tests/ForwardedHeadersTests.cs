using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Skjermvette.Core.DeviceApi;
using Skjermvette.Server.Contracts;
using static Skjermvette.Server.Tests.ServerFactory;

namespace Skjermvette.Server.Tests;

/// <summary>Behind the Cloudflare Tunnel every request comes from the same address (#6).</summary>
public class ForwardedHeadersTests
{
    [Fact]
    public async Task PairingRateLimit_IsPerClient_FromCfConnectingIp()
    {
        using var factory = new ServerFactory();
        var client = factory.CreateClient();
        var request = new PairRequest("000000", "PC", DevicePlatformEnum.Windows);

        async Task<HttpStatusCode> PairFromAsync(string ip)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "/api/device/pair") { Content = JsonContent.Create(request, options: Json) };
            message.Headers.Add("CF-Connecting-IP", ip);
            return (await client.SendAsync(message)).StatusCode;
        }

        var responses = new List<HttpStatusCode>();
        for (var i = 0; i < 11; i++)
            responses.Add(await PairFromAsync("203.0.113.1"));
        var otherClient = await PairFromAsync("203.0.113.2");

        Assert.All(responses[..10], s => Assert.NotEqual(HttpStatusCode.TooManyRequests, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, responses[10]);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, otherClient);
    }

    [Fact]
    public async Task SignInCookies_AreHttpsOnly_EvenOverPlainHttp()
    {
        using var factory = new ServerFactory();
        await factory.CreateParentAsync();
        // Like cloudflared, which forwards plain HTTP.
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("parent@example.com", DefaultPassword), Json);

        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("skjermvette.2fa="));
        Assert.Contains("; secure", cookie, StringComparison.OrdinalIgnoreCase);
    }
}
