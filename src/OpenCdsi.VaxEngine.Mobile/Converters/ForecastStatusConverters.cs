using System.Globalization;
using OpenCdsi.VaxEngine.Mobile.Models;

namespace OpenCdsi.VaxEngine.Mobile.Converters;

// Looks colors up from Resources/Styles/Colors.xaml by key rather than
// duplicating hex values here, so the two stay in sync automatically.
public class ForecastStatusToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            ForecastStatus.DueNow => "StatusDueNow",
            ForecastStatus.NotRecommended => "StatusWarning",
            ForecastStatus.Complete => "StatusComplete",
            ForecastStatus.NotYetDue => "StatusNeutral",
            _ => "StatusNeutral"
        };

        return Application.Current!.Resources[key];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class ForecastStatusToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            ForecastStatus.DueNow => "Due now",
            ForecastStatus.NotRecommended => "Not recommended",
            ForecastStatus.Complete => "Complete",
            ForecastStatus.NotYetDue => "Not yet due",
            _ => string.Empty
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
