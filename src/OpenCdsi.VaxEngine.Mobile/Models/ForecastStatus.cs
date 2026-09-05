namespace OpenCdsi.VaxEngine.Mobile.Models;

// DueNow/NotYetDue are this app's own split of the engine's single NotComplete status (see
// VaxEngineForecastService) - everything else maps 1:1 onto OpenCdsi.VaxEngine.Core's
// PatientSeriesStatus. Immune, Contraindicated, and AgedOut are kept distinct rather than folded
// into NotRecommended: they're clinically different reasons a dose isn't given, and collapsing
// them would hide that difference from the person reading the forecast.
public enum ForecastStatus
{
    DueNow,
    NotYetDue,
    NotRecommended,
    Complete,
    Immune,
    Contraindicated,
    AgedOut
}
