using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HealthTrack.IntegrationTests;

public sealed class HostTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public HostTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    [Fact]
    public async Task Public_workspace_renders_without_database_configuration()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var response = await client.GetAsync("/");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<main id=\"main\"", html);
        Assert.Contains("/js/app.js", html);
        Assert.Contains("aria-labelledby=\"dialog-title\"", html);
        Assert.DoesNotContain("client-fixture", html);
    }

    [Theory]
    [InlineData("/app.css", "text/css")]
    [InlineData("/js/app.js", "javascript")]
    [InlineData("/vendor/chart.umd.js", "javascript")]
    public async Task Workspace_assets_are_served_with_browser_compatible_content_types(string path, string contentType)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        Assert.Contains(contentType, response.Content.Headers.ContentType!.MediaType!);
    }

    [Fact]
    public async Task Host_starts_and_returns_not_found_for_unmapped_route()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/unmapped-foundation-probe");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
