using OpenCdsi.VaxEngine.Mobile.Models;

namespace OpenCdsi.VaxEngine.Mobile.Services;

// TEMPORARY. Returns fixed sample data so ForecastResultPage has something
// to render and the navigation flow can be tested end to end. Delete this
// and register a real IForecastEngineAdapter implementation once
// vaxengine.core is wired in — the interface's job is to make that swap a
// one-line change in MauiProgram.cs, not a rewrite of this page.
public class PlaceholderForecastEngineAdapter : IForecastEngineAdapter
{
    public Task<IReadOnlyList<ForecastEntry>> ForecastAsync(Patient patient, CancellationToken ct = default)
    {
        IReadOnlyList<ForecastEntry> sample = new List<ForecastEntry>
        {
            new("MMR", ForecastStatus.DueNow,
                "Next in the series, and the minimum interval since the last dose has passed."),
            new("Rotavirus", ForecastStatus.NotRecommended,
                "Given only 3 weeks after the previous dose — the minimum wait is 4 weeks."),
            new("Polio (OPV)", ForecastStatus.Complete,
                "All doses in the series have been given."),
            new("Pentavalent", ForecastStatus.NotYetDue,
                "Next dose is scheduled for age 14 weeks, based on date of birth."),
        };

        return Task.FromResult(sample);
    }
}
