using OpenCdsi.VaxEngine.Mobile.Models;

namespace OpenCdsi.VaxEngine.Mobile.Services;

// This interface is a seam, not a guess at vaxengine.core's real API — I
// don't have that library's actual public surface in front of me in this
// session, so I'm not fabricating class/method names for it. Implement
// VaxEngineForecastService (real) against whatever vaxengine.core actually
// exposes, mapping its output into ForecastEntry here. Everything upstream
// (the ViewModel, the page) only depends on this interface, so swapping the
// placeholder for the real adapter shouldn't touch anything else.
public interface IForecastEngineAdapter
{
    Task<IReadOnlyList<ForecastEntry>> ForecastAsync(Patient patient, CancellationToken ct = default);
}
