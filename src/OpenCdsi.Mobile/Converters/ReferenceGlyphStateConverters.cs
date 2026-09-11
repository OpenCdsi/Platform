/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Globalization;
using OpenCdsi.Mobile.Models;

namespace OpenCdsi.Mobile.Converters;

public class ReferenceGlyphStateToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ReferenceGlyphState.Hidden ? false : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// Looks colors up from Resources/Styles/Colors.xaml by key rather than duplicating hex values here,
// same convention as ForecastStatusToColorConverter. Reuses the existing brand/neutral text colors
// rather than adding dedicated glyph-only keys - Enabled reads as an ordinary tappable link,
// Disabled reads as an ordinary muted/inactive control.
public class ReferenceGlyphStateToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            ReferenceGlyphState.Enabled => "BrandSecondary",
            _ => "TextMuted"
        };

        return Application.Current!.Resources[key];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
