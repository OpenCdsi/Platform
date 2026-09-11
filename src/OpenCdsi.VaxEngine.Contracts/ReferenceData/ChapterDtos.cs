/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

namespace OpenCdsi.VaxEngine.Contracts.ReferenceData;

/// <summary>1:1 mirror of OpenCdsi.ClinicalReference.Models.AntigenChapter - see that type's own doc comment for what each field means and why it's optional.</summary>
public sealed class ChapterDto
{
    public required string AntigenKey { get; init; }
    public required string DiseaseName { get; init; }
    public string? Organism { get; init; }
    public string? ClinicalFeaturesSummary { get; init; }
    public string? EpidemiologySummary { get; init; }
    public string? SecularTrendsSummary { get; init; }
    public string? VaccineDescription { get; init; }
    public string? VaccinationScheduleSummary { get; init; }
    public string? VaccineEfficacySummary { get; init; }
    public string? EvidenceOfImmunitySummary { get; init; }
    public string? ContraindicationsSummary { get; init; }
    public string? VaccineSafetySummary { get; init; }
    public string? VaccineStorageSummary { get; init; }
    public string? SurveillanceSummary { get; init; }
    public required IReadOnlyList<string> KeyPoints { get; init; }
    public required IReadOnlyList<SupplementalTopicDto> SupplementalTopics { get; init; }
    public required ChapterSourceDto Source { get; init; }
}

public sealed class SupplementalTopicDto
{
    public required string Title { get; init; }
    public required string Summary { get; init; }
}

public sealed class ChapterSourceDto
{
    public required string Edition { get; init; }
    public string? ChapterAuthors { get; init; }
    public required string Url { get; init; }
    public required DateOnly PublishedDate { get; init; }
}
