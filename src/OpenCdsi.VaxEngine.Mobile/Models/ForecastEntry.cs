namespace OpenCdsi.VaxEngine.Mobile.Models;

// One row of a forecast result: an antigen, its status, and why. Maps onto
// whatever vaxengine.core actually returns once a real IForecastEngineAdapter
// replaces PlaceholderForecastEngineAdapter — this shape is the app's own
// display model, not vaxengine.core's.
public record ForecastEntry(string AntigenName, ForecastStatus Status, string ReasonText);
