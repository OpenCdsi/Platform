/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Text.Json;
using OpenCdsi.VaxEngine.Contracts;
using OpenCdsi.VaxEngine.Core.Pipeline;
using OpenCdsi.VaxEngine.Core.ReferenceData;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace OpenCdsi.VaxEngine.Functions;

/// <summary>
/// The §4.4/§6 EVALUATION half of the CDSi process - how each already-administered dose graded
/// out - as a second API surface alongside OpenCdsi.VaxEngine.Api's own POST /api/v3/evaluate.
/// Kept as its own class rather than another method on ForecastFunction, mirroring the two
/// distinct process halves (evaluate vs forecast). Same request DTO, same
/// AuthorizationLevel.Function key requirement, and the same error-handling shape as
/// ForecastFunction - see that class's doc comment for the reasoning behind each.
/// </summary>
public class EvaluateFunction(ReferenceDataRepository data, ILogger<EvaluateFunction> logger)
{
    [Function("Evaluate")]
    public async Task<IActionResult> Evaluate(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "api/v1/evaluate")] HttpRequest req)
    {
        ForecastRequestDto? request;
        try
        {
            request = await req.ReadFromJsonAsync<ForecastRequestDto>();
        }
        catch (JsonException ex)
        {
            return new BadRequestObjectResult(new { title = "Invalid request", detail = ex.Message });
        }

        if (request is null)
        {
            return new BadRequestObjectResult(new { title = "Invalid request", detail = "Request body is required." });
        }

        try
        {
            var patient = RequestMapping.ToPatient(request);
            var doses = RequestMapping.ToAdministeredDoses(request);
            var assessmentDate = RequestMapping.ResolveAssessmentDate(request, DateOnly.FromDateTime(DateTime.UtcNow));

            var result = GeneratePatientForecast.ExecuteWithDoseDetail(
                patient, doses, data.AllSeries, data.Schedule, data.VaccineGroups,
                data.ImmunityByAntigen, data.ContraindicationsByAntigen, assessmentDate);

            return new OkObjectResult(EvaluationResponseMapping.ToResponse(request.PatientId, assessmentDate, doses, result));
        }
        catch (InvalidRequestException ex)
        {
            return new BadRequestObjectResult(new { title = "Invalid request", detail = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception processing Evaluate");
            return new ObjectResult(new { title = "Internal server error", detail = "An unexpected error occurred." })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
