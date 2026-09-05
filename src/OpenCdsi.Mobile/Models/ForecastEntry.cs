namespace OpenCdsi.Mobile.Models;

// One row of a forecast result: an antigen, its status, and why. This is the app's own display
// model — VaxEngineForecastService maps OpenCdsi.VaxEngine.Core's real forecast output into this
// shape, so the UI never depends on the engine's own types directly.
public record ForecastEntry(string AntigenName, ForecastStatus Status, string ReasonText);
