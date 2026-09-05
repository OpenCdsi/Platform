using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using OpenCdsi.Mobile.Services;

namespace OpenCdsi.Mobile.Converters;

// Resolves a stored ImmunizationEvent.CvxCode back to its human-readable name via
// CvxLookupService, the same reference data the vaccine pickers search against - so a dose
// recorded as CVX 08 shows as "Hep B, adolescent or pediatric" instead of a bare code. Falls
// back to the raw code for anything not in the schedule data (should not happen in practice,
// since codes only get stored via a picker backed by this same lookup).
public class CvxCodeToDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var code = value as string;
        if (string.IsNullOrEmpty(code)) return string.Empty;

        var lookup = IPlatformApplication.Current!.Services.GetRequiredService<CvxLookupService>();
        return lookup.FindByCode(code)?.DisplayName ?? $"CVX {code}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
