using OpenCdsi.VaxEngine.Mobile.Models;

namespace OpenCdsi.VaxEngine.Mobile.Services;

// The seam between the app and OpenCdsi.VaxEngine.Core. VaxEngineForecastService is the real
// implementation (registered as this interface in MauiProgram.cs), mapping the engine's output
// into ForecastEntry; every ViewModel/page only depends on this interface, never the engine
// package directly.
public interface IForecastEngineAdapter
{
    Task<IReadOnlyList<ForecastEntry>> ForecastAsync(Patient patient, CancellationToken ct = default);
}
