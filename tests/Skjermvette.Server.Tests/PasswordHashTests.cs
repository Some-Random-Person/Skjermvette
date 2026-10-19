using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Skjermvette.Server.Auth;
using Skjermvette.Server.Contracts;
using Skjermvette.Server.Data.Entities;
using static Skjermvette.Server.Tests.ServerFactory;

namespace Skjermvette.Server.Tests;

public class PasswordHashTests
{
    private const string Email = "ada@example.com";
    private const string Password = "correct-horse-battery";

    [Fact]
    public async Task NewPasswords_AreHashedWithTheConfiguredIterations()
    {
        using var factory = new ServerFactory();
        await factory.CreateParentAsync(Email);

        Assert.Equal(ParentSession.PasswordHashIterations, Iterations(await StoredHashAsync(factory)));
    }

    [Fact]
    public async Task AnOlderHash_IsUpgradedAtTheNextSignIn()
    {
        using var factory = new ServerFactory();
        await factory.CreateParentAsync(Email);
        // Replace the hash with one made with Identity's old default of 100,000 iterations.
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ParentUser>>();
            var user = (await users.FindByEmailAsync(Email))!;
            user.PasswordHash = new PasswordHasher<ParentUser>(Options.Create(new PasswordHasherOptions { IterationCount = 100_000 }))
                .HashPassword(user, Password);
            await users.UpdateAsync(user);
        }
        Assert.Equal(100_000, Iterations(await StoredHashAsync(factory)));

        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(Email, Password), Json);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(ParentSession.PasswordHashIterations, Iterations(await StoredHashAsync(factory)));
    }

    private static async Task<string> StoredHashAsync(ServerFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ParentUser>>();
        return (await users.FindByEmailAsync(Email))!.PasswordHash!;
    }

    /// <summary>Reads the iteration count from an Identity V3 hash (big-endian, after the format byte and PRF).</summary>
    private static int Iterations(string hash)
    {
        var bytes = Convert.FromBase64String(hash);
        Assert.Equal(0x01, bytes[0]);
        return bytes[5] << 24 | bytes[6] << 16 | bytes[7] << 8 | bytes[8];
    }
}
