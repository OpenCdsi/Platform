/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OpenCdsi.VaxEngine.Api.Tests;

/// <summary>Real, end-to-end HTTP tests against /antigens/{name}/ref and /reference/{name}, same WebApplicationFactory approach as ForecastEndpointTests - real Pink Book data loaded from the real repo data/pinkbook directory, not mocked.</summary>
public class ChapterEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ChapterEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/v3/antigens/Diphtheria/ref")]
    [InlineData("/api/v3/reference/Diphtheria")]
    public async Task RealAntigenWithChapter_ReturnsChapter(string path)
    {
        var response = await _client.GetAsync(path);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Diphtheria", body.GetProperty("antigenKey").GetString());
        Assert.True(body.GetProperty("keyPoints").GetArrayLength() > 0);
    }

    [Theory]
    [InlineData("/api/v3/antigens/diphtheria/ref")]
    [InlineData("/api/v3/reference/diphtheria")]
    public async Task AntigenNameLookup_IsCaseInsensitive(string path)
    {
        var response = await _client.GetAsync(path);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Diphtheria", body.GetProperty("antigenKey").GetString());
    }

    [Theory]
    [InlineData("/api/v3/antigens/NotARealAntigen/ref")]
    [InlineData("/api/v3/reference/NotARealAntigen")]
    public async Task UnknownAntigen_Returns404(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
