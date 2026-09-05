using Engine = OpenCdsi.VaxEngine.Core.Models;
using OpenCdsi.VaxEngine.Core.Evaluation;
using OpenCdsi.VaxEngine.Core.Pipeline;
using OpenCdsi.VaxEngine.Core.ReferenceData;
using OpenCdsi.VaxEngine.Mobile.Models;

namespace OpenCdsi.VaxEngine.Mobile.Services;

// The real IForecastEngineAdapter, calling OpenCdsi.VaxEngine.Core's actual forecasting pipeline.
// Reference data is ~2.6MB of CDC XML across 30 antigen files - loaded once (via
// ReferenceDataProvisioner) and cached for the app's lifetime, not reloaded per forecast.
public class VaxEngineForecastService : IForecastEngineAdapter
{
    private readonly Lazy<Task<ReferenceDataRepository>> _referenceData =
        new(() => ReferenceDataProvisioner.LoadAsync());

    public async Task<IReadOnlyList<ForecastEntry>> ForecastAsync(Patient patient, CancellationToken ct = default)
    {
        var referenceData = await _referenceData.Value;
        var assessmentDate = DateOnly.FromDateTime(DateTime.Today);

        var enginePatient = new Engine.Patient
        {
            PatientId = patient.Id.ToString(),
            DateOfBirth = patient.DateOfBirth,
            Gender = ToEngineGender(patient.Gender)
        };

        var doses = patient.ImmunizationEvents
            .Select(e => new Engine.VaccineDoseAdministered
            {
                DoseId = e.Id.ToString(),
                Cvx = e.CvxCode,
                DateAdministered = e.DateAdministered
            })
            .ToList();

        var results = GeneratePatientForecast.Execute(
            enginePatient,
            doses,
            referenceData.AllSeries,
            referenceData.Schedule,
            referenceData.VaccineGroups,
            referenceData.ImmunityByAntigen,
            referenceData.ContraindicationsByAntigen,
            assessmentDate);

        return results.Select(r => ToForecastEntry(r, assessmentDate)).ToList();
    }

    private static Engine.Gender ToEngineGender(Gender gender) => gender switch
    {
        Gender.Male => Engine.Gender.Male,
        Gender.Female => Engine.Gender.Female,
        _ => Engine.Gender.Unknown
    };

    private static ForecastStatus ToForecastStatus(VaccineGroupForecastResult result, DateOnly assessmentDate)
    {
        switch (result.Status)
        {
            case PatientSeriesStatus.Complete:
                return ForecastStatus.Complete;

            case PatientSeriesStatus.NotComplete:
                // "Due now" once the recommended date has arrived (or there's no recommended
                // date to wait on); "not yet due" while still ahead of it. The engine doesn't
                // itself distinguish "due now" from "past due" - both land on DueNow here since
                // the app's UI only has the one status for "give it now."
                var recommended = result.AdjustedRecommendedDate ?? result.EarliestDate;
                return recommended is null || recommended <= assessmentDate
                    ? ForecastStatus.DueNow
                    : ForecastStatus.NotYetDue;

            default:
                // NotRecommended, Immune, Contraindicated, AgedOut - four clinically distinct
                // reasons the engine keeps separate, folded into this app's one "don't give this"
                // status because that's all its UI distinguishes today. Reasons (below) still
                // carries the real distinction through to ReasonText.
                return ForecastStatus.NotRecommended;
        }
    }

    private static ForecastEntry ToForecastEntry(VaccineGroupForecastResult result, DateOnly assessmentDate) =>
        new(
            AntigenName: result.VaccineGroupName,
            Status: ToForecastStatus(result, assessmentDate),
            ReasonText: result.Reasons.Count > 0
                ? string.Join(" ", result.Reasons)
                : "No additional detail available.");
}
