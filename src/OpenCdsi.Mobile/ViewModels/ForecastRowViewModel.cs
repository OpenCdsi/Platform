/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using CommunityToolkit.Mvvm.ComponentModel;
using OpenCdsi.Mobile.Models;

namespace OpenCdsi.Mobile.ViewModels;

// Wraps a ForecastEntry with this page's own info-glyph state, computed once in
// ForecastResultViewModel.LoadAsync. Kept as a page-scoped wrapper rather than a change to
// ForecastEntry itself, since QuickForecastResultPage also binds that shared record directly and
// has no need for this concern.
//
// Also owns the Disabled-glyph tooltip's open/closed state and auto-dismiss timer. There's no
// floating tooltip control available here (see the disabled-glyph interaction design note on
// ForecastResultPage.xaml) - the tooltip is an inline collapsible region inside this same row, so
// its visibility is just another observable property on the row itself, not a separate popup.
public sealed partial class ForecastRowViewModel : ObservableObject
{
    private readonly ForecastEntry _entry;
    private CancellationTokenSource? _tooltipAutoCloseCts;

    public ForecastRowViewModel(ForecastEntry entry, ReferenceGlyphState glyphState, string? chapterAntigenKey)
    {
        _entry = entry;
        GlyphState = glyphState;
        ChapterAntigenKey = chapterAntigenKey;
    }

    public string AntigenName => _entry.AntigenName;
    public ForecastStatus Status => _entry.Status;
    public string ReasonText => _entry.ReasonText;
    public ReferenceGlyphState GlyphState { get; }

    // The AntigenChapter key to navigate to when GlyphState is Enabled - not always AntigenName
    // itself, since a few vaccine groups (DTaP/Tdap/Td, MMR) span several chapters at once. Null
    // whenever GlyphState isn't Enabled.
    public string? ChapterAntigenKey { get; }

    [ObservableProperty]
    private bool isTooltipVisible;

    // Tapping a Disabled glyph again while its tooltip is open closes it early; tapping it while
    // closed opens it and starts the 3s auto-dismiss clock, restarting that clock if tapped again
    // before it fires.
    public void ToggleTooltip()
    {
        _tooltipAutoCloseCts?.Cancel();

        if (IsTooltipVisible)
        {
            IsTooltipVisible = false;
            return;
        }

        IsTooltipVisible = true;
        var cts = new CancellationTokenSource();
        _tooltipAutoCloseCts = cts;
        _ = AutoCloseTooltipAsync(cts.Token);
    }

    public void CloseTooltip()
    {
        _tooltipAutoCloseCts?.Cancel();
        IsTooltipVisible = false;
    }

    private async Task AutoCloseTooltipAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
            IsTooltipVisible = false;
        }
        catch (TaskCanceledException)
        {
            // Cancelled by CloseTooltip/ToggleTooltip - the tooltip's already in the right state.
        }
    }
}
