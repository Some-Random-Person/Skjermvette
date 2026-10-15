using System.Net;
using System.Net.Http.Json;
using ScreenTime.Server.Contracts;
using static ScreenTime.Server.Tests.ServerFactory;

namespace ScreenTime.Server.Tests;

public class AuthTests
{
    [Fact]
    public async Task ConfirmedParent_IsSignedIn()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync("ada@example.com");

        var me = await parent.GetFromJsonAsync<MeResponse>("/api/auth/me", Json);

        Assert.Equal("ada@example.com", me!.Email);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Is401()
    {
        using var factory = new ServerFactory();
        await factory.CreateParentAsync("ada@example.com");

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("ada@example.com", "wrong-password!"), Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithRightPassword_AsksForTheSecondStep_BeforeSigningIn()
    {
        using var factory = new ServerFactory();
        await factory.CreateParentAsync("ada@example.com");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("ada@example.com", "correct-horse-battery"), Json);

        response.EnsureSuccessStatusCode();
        Assert.Equal(new LoginResponse(null, TwoFactorMethodEnum.Email), await response.Content.ReadFromJsonAsync<LoginResponse>(Json));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/children")).StatusCode);
    }

    [Fact]
    public async Task Register_WithShortPassword_IsValidationProblem()
    {
        using var factory = new ServerFactory();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest("ada@example.com", "short"), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ParentApi_WithoutSignIn_Is401()
    {
        using var factory = new ServerFactory();

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/children")).StatusCode);
    }

    [Fact]
    public async Task Families_CannotSeeEachOthersChildren()
    {
        using var factory = new ServerFactory();
        var ada = await factory.CreateParentAsync("ada@example.com");
        var bob = await factory.CreateParentAsync("bob@example.com");
        var child = await CreateChildAsync(ada);

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/children/{child.Id}")).StatusCode);
        Assert.Empty((await bob.GetFromJsonAsync<List<ChildResponse>>("/api/children", Json))!);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsync($"/api/children/{child.Id}/pairing-codes", null)).StatusCode);
    }

    [Fact]
    public async Task DeviceToken_CannotUseParentApi_AndParentCannotUseDeviceApi()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();
        var child = await CreateChildAsync(parent);
        var (device, _) = await factory.PairDeviceAsync(parent, child.Id);

        Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync("/api/children")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await parent.GetAsync("/api/device/state")).StatusCode);
    }
}
