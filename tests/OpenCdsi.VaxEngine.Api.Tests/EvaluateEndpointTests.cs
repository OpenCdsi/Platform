/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OpenCdsi.VaxEngine.Api.Tests;

/// <summary>
/// Real, end-to-end HTTP tests for POST /api/v3/evaluate - the §4.4/§6 evaluation surface, the
/// companion to ForecastEndpointTests. Same WebApplicationFactory&lt;Program&gt; setup: the real
/// Program.cs startup, real data loading, real GeneratePatientForecast pipeline reached through
/// the real HTTP/JSON boundary, nothing mocked.
/// </summary>
public class EvaluateEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EvaluateEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Evaluate_RealNewbornZeroDoses_ReturnsNoEvaluatedDoses()
    {
        var request = new
        {
            patientId = "test-patient-1",
            dateOfBirth = "2024-01-01",
            assessmentDate = "2024-01-01",
            administeredDoses = Array.Empty<object>()
        };

        var response = await _client.PostAsJsonAsync("/api/v3/evaluate", request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        Assert.Equal("test-patient-1", body.GetProperty("patientId").GetString());
        Assert.Equal(0, body.GetProperty("evaluatedDoses").GetArrayLength());
    }

    [Fact]
    public async Task Evaluate_RealHepBValidThenTooSoon_GradesFirstValidSecondNotValid()
    {
        // Same fixture as EvaluateSeriesHistoryTests.DoseGivenTooYoung_FailsTargetDose1: Dose 1
        // at DOB satisfies target dose 1; Dose 2 only 5 days later fails Age ("Too young"), and
        // target dose 2 stays outstanding. HepB is single-antigen, so no per-antigen conflict.
        var request = new
        {
            patientId = "test-patient-2",
            dateOfBirth = "2020-01-01",
            assessmentDate = "2020-09-01",
            administeredDoses = new[]
            {
                new { doseId = "d1", cvx = "08", dateAdministered = "2020-01-01" },
                new { doseId = "d2", cvx = "08", dateAdministered = "2020-01-06" }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v3/evaluate", request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var doses = body.GetProperty("evaluatedDoses").EnumerateArray().ToArray();
        Assert.Equal(2, doses.Length);

        var d1 = doses.Single(d => d.GetProperty("doseId").GetString() == "d1");
        Assert.Equal("Valid", d1.GetProperty("status").GetString());
        Assert.False(d1.GetProperty("hasAntigenConflict").GetBoolean());
        var d1Antigens = d1.GetProperty("perAntigen").EnumerateArray().ToArray();
        Assert.Equal("HepB", Assert.Single(d1Antigens).GetProperty("antigen").GetString());

        var d2 = doses.Single(d => d.GetProperty("doseId").GetString() == "d2");
        Assert.Equal("NotValid", d2.GetProperty("status").GetString());
        Assert.False(d2.GetProperty("hasAntigenConflict").GetBoolean());
        Assert.Equal("Too young", d2.GetProperty("perAntigen").EnumerateArray().Single().GetProperty("reason").GetString());

        var hepB = body.GetProperty("antigenSummaries").EnumerateArray()
            .Single(s => s.GetProperty("antigen").GetString() == "HepB");
        Assert.False(hepB.GetProperty("seriesComplete").GetBoolean());
        Assert.Equal(2, hepB.GetProperty("currentTargetDoseNumber").GetInt32());
    }

    [Fact]
    public async Task Evaluate_DoseForAntigenWithNoRelevantSeries_ReportsNotEvaluated()
    {
        // CVX 998 ("no vaccine administered") associates to no antigen, so the engine produces
        // no evaluation records for it. The dose still keeps a row - status "NotEvaluated", empty
        // per-antigen breakdown - rather than silently vanishing from the response.
        var request = new
        {
            patientId = "test-patient-3",
            dateOfBirth = "2020-01-01",
            assessmentDate = "2020-06-01",
            administeredDoses = new[]
            {
                new { doseId = "only", cvx = "998", dateAdministered = "2020-02-01" } // 998 = "no vaccine administered"
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v3/evaluate", request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var dose = Assert.Single(body.GetProperty("evaluatedDoses").EnumerateArray().ToArray());
        Assert.Equal("only", dose.GetProperty("doseId").GetString());
        Assert.Equal("NotEvaluated", dose.GetProperty("status").GetString());
        Assert.Equal(0, dose.GetProperty("perAntigen").GetArrayLength());
    }

    [Fact]
    public async Task Evaluate_InvalidGender_Returns400WithClearMessage()
    {
        var request = new
        {
            patientId = "test-patient-4",
            dateOfBirth = "2020-01-01",
            gender = "not-a-real-gender"
        };

        var response = await _client.PostAsJsonAsync("/api/v3/evaluate", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("gender", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluate_MissingRequiredDateOfBirth_Returns400()
    {
        var request = new { patientId = "test-patient-5" };

        var response = await _client.PostAsJsonAsync("/api/v3/evaluate", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_DefaultsAssessmentDateToToday_WhenOmitted()
    {
        var request = new
        {
            patientId = "test-patient-6",
            dateOfBirth = "2024-01-01"
        };

        var response = await _client.PostAsJsonAsync("/api/v3/evaluate", request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var assessmentDate = DateOnly.Parse(body.GetProperty("assessmentDate").GetString()!);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), assessmentDate);
    }
}
