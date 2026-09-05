/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Globalization;
using OpenCdsi.Mobile.Models;

namespace OpenCdsi.Mobile.Converters;

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
            ForecastStatus.Immune => "StatusImmune",
            ForecastStatus.Contraindicated => "StatusContraindicated",
            ForecastStatus.AgedOut => "StatusAgedOut",
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
            ForecastStatus.Immune => "Immune",
            ForecastStatus.Contraindicated => "Contraindicated",
            ForecastStatus.AgedOut => "Aged out",
            _ => string.Empty
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
