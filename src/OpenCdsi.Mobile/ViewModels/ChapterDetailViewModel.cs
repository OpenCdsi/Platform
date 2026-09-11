/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCdsi.ClinicalReference.Models;
using OpenCdsi.Mobile.Services;

namespace OpenCdsi.Mobile.ViewModels;

[QueryProperty(nameof(AntigenKey), "antigenKey")]
[QueryProperty(nameof(AutoJumpFlag), "autoJump")]
public partial class ChapterDetailViewModel : ObservableObject
{
    private readonly ClinicalReferenceStore _clinicalReferenceStore;

    public ChapterDetailViewModel(ClinicalReferenceStore clinicalReferenceStore)
    {
        _clinicalReferenceStore = clinicalReferenceStore;
    }

    // Raised once, right after Sections is populated, when a Contraindicated/NotRecommended
    // forecast row sent us here with autoJump=true and the chapter actually has a Contraindications
    // section to jump to. The event argument is that section's index in Sections.
    // ChapterDetailPage.xaml.cs subscribes to scroll the accordion there - MVVM has no bindable
    // CollectionView.ScrollTo, so this is a deliberate, narrow code-behind exception.
    public event EventHandler<int>? ScrollToSectionRequested;

    [ObservableProperty]
    private string antigenKey = string.Empty;

    partial void OnAntigenKeyChanged(string value) => _ = LoadCommand.ExecuteAsync(null);

    [ObservableProperty]
    private string autoJumpFlag = string.Empty;

    [ObservableProperty]
    private string diseaseName = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<SupplementalTopic> supplementalTopics = Array.Empty<SupplementalTopic>();

    [ObservableProperty]
    private ChapterSource? source;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool chapterNotFound;

    public ObservableCollection<AccordionSectionViewModel> Sections { get; } = new();

    [RelayCommand]
    private static async Task OpenSourceAsync(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        await Launcher.Default.OpenAsync(new Uri(url));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(AntigenKey)) return;

        IsBusy = true;
        try
        {
            var repository = await _clinicalReferenceStore.LoadAsync();
            var chapter = repository.TryGetByAntigen(AntigenKey);

            ChapterNotFound = chapter is null;
            if (chapter is null) return;

            DiseaseName = chapter.DiseaseName;
            SupplementalTopics = chapter.SupplementalTopics;
            Source = chapter.Source;

            Sections.Clear();
            Sections.Add(new AccordionSectionViewModel("Key points", "• " + string.Join("\n• ", chapter.KeyPoints)));
            AddIfPresent("Overview", chapter.Organism);
            AddIfPresent("Clinical features", chapter.ClinicalFeaturesSummary);
            AddIfPresent("Epidemiology", chapter.EpidemiologySummary);
            AddIfPresent("Trends", chapter.SecularTrendsSummary);
            AddIfPresent("About the vaccine", chapter.VaccineDescription);
            AddIfPresent("Vaccination schedule", chapter.VaccinationScheduleSummary);
            AddIfPresent("Vaccine efficacy", chapter.VaccineEfficacySummary);
            AddIfPresent("Evidence of immunity", chapter.EvidenceOfImmunitySummary);
            var contraindicationsSection = AddIfPresent("Contraindications & precautions", chapter.ContraindicationsSummary);
            AddIfPresent("Vaccine safety", chapter.VaccineSafetySummary);
            AddIfPresent("Storage", chapter.VaccineStorageSummary);
            AddIfPresent("Surveillance", chapter.SurveillanceSummary);

            if (AutoJumpFlag == "true" && contraindicationsSection is not null)
            {
                contraindicationsSection.IsExpanded = true;
                ScrollToSectionRequested?.Invoke(this, Sections.IndexOf(contraindicationsSection));
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Chapter fields are all optional (not every Pink Book chapter has every section - see
    // AntigenChapter's own doc comments) - a section simply doesn't appear when its backing field
    // is null, rather than rendering an empty row. Returns the added section (or null) so the
    // Contraindications case above can grab its own reference without a second lookup.
    private AccordionSectionViewModel? AddIfPresent(string title, string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var section = new AccordionSectionViewModel(title, content);
        Sections.Add(section);
        return section;
    }
}
