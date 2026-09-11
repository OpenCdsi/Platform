/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OpenCdsi.Mobile.ViewModels;

// One collapsible row in ChapterDetailPage's accordion - see ChapterDetailViewModel for how these
// get built (one per non-null AntigenChapter summary field, plus a leading one for Key points).
public sealed partial class AccordionSectionViewModel : ObservableObject
{
    public AccordionSectionViewModel(string title, string content)
    {
        Title = title;
        Content = content;
    }

    public string Title { get; }
    public string Content { get; }

    [ObservableProperty]
    private bool isExpanded;

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;
}
