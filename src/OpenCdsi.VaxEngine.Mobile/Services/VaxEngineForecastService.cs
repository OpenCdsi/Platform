using Engine = OpenCdsi.VaxEngine.Core.Models;
using OpenCdsi.VaxEngine.Core.Evaluation;
using OpenCdsi.VaxEngine.Core.Pipeline;
using OpenCdsi.VaxEngine.Mobile.Models;

namespace OpenCdsi.VaxEngine.Mobile.Services;

// The real IForecastEngineAdapter, calling OpenCdsi.VaxEngine.Core's actual forecasting pipeline.
// Reference data comes from the shared ReferenceDataStore (loaded once at app startup), not
// reloaded per forecast.
public class VaxEngineForecastService : IForecastEngineAdapter
{
    private readonly ReferenceDataStore _referenceData;

    public VaxEngineForecastService(ReferenceDataStore referenceData)
    {
        _referenceData = referenceData;
    }

    public async Task<IReadOnlyList<ForecastEntry>> ForecastAsync(Patient patient, CancellationToken ct = default)
    {
        // Awaits the shared background load (kicked off at startup in MauiProgram.cs) instead of
        // assuming it's already finished - by the time a user reaches this screen it almost
        // certainly is, but this stays correct even if they get here unusually fast.
        var referenceData = await _referenceData.LoadAsync();
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

            case PatientSeriesStatus.Immune:
                return ForecastStatus.Immune;

            case PatientSeriesStatus.Contraindicated:
                return ForecastStatus.Contraindicated;

            case PatientSeriesStatus.AgedOut:
                return ForecastStatus.AgedOut;

            case PatientSeriesStatus.NotRecommended:
                return ForecastStatus.NotRecommended;

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
                throw new ArgumentOutOfRangeException(
                    nameof(result), result.Status, "Unrecognized PatientSeriesStatus.");
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
