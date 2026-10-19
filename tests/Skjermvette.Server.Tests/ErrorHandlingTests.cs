using System.Net;
using System.Text;

namespace Skjermvette.Server.Tests;

public class ErrorHandlingTests
{
    [Fact]
    public async Task MalformedJson_Is400WithProblemDetails()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();

        var response = await parent.PostAsync("/api/children", new StringContent("{\"name\": \"Al\rex\"}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnknownApiPath_Is404_NotTheDashboard()
    {
        using var factory = new ServerFactory();
        var parent = await factory.CreateParentAsync();

        var response = await parent.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
