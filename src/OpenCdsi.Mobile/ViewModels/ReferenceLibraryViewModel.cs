/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCdsi.Mobile.Models;
using OpenCdsi.Mobile.Services;

namespace OpenCdsi.Mobile.ViewModels;

// Browsable, searchable list of every curated ClinicalReference chapter, reachable without a
// patient loaded - see PatientsPage's "Reference" toolbar item. No autoJump: a chapter opened from
// here has no forecast status behind it, so ChapterDetailViewModel just opens at the top.
public partial class ReferenceLibraryViewModel : ObservableObject
{
    private readonly ClinicalReferenceStore _clinicalReferenceStore;
    private List<AntigenChapterListItem> _allChapters = new();

    public ReferenceLibraryViewModel(ClinicalReferenceStore clinicalReferenceStore)
    {
        _clinicalReferenceStore = clinicalReferenceStore;
    }

    [ObservableProperty]
    private ObservableCollection<AntigenChapterListItem> chapters = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var repository = await _clinicalReferenceStore.LoadAsync();

        _allChapters = repository.ChaptersByAntigen.Values
            .Select(c => new AntigenChapterListItem(c.AntigenKey, c.DiseaseName))
            .OrderBy(c => c.DiseaseName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ApplyFilter();
    }

    [RelayCommand]
    private static async Task OpenChapterAsync(AntigenChapterListItem? item)
    {
        if (item is null) return;
        await Shell.Current.GoToAsync($"chapterdetail?antigenKey={Uri.EscapeDataString(item.AntigenKey)}");
    }

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allChapters
            : _allChapters.Where(c => c.DiseaseName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        Chapters = new ObservableCollection<AntigenChapterListItem>(filtered);
    }
}
