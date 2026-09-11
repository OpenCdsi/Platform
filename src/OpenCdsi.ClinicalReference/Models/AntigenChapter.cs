/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

namespace OpenCdsi.ClinicalReference.Models;

/// <summary>
/// A curated, per-antigen digest of a CDC Pink Book chapter - hand-authored summaries per
/// section (not the raw chapter text, which runs several thousand words across a two-column
/// layout with interleaved sidebar callouts) so it's short enough to surface in a mobile "learn
/// about this vaccine" panel. AntigenKey deliberately matches the same antigen name string used
/// by AntigenSeries.Antigen in OpenCdsi.VaxEngine.Core/OpenCdsi.VaxEngine.Contracts (e.g.
/// "Diphtheria"), so a forecast result's antigen name can look a chapter up directly with no
/// separate mapping table.
/// </summary>
public sealed class AntigenChapter
{
    public required string AntigenKey { get; init; }
    public required string DiseaseName { get; init; }
    public string? Organism { get; init; }
    public string? ClinicalFeaturesSummary { get; init; }
    public string? EpidemiologySummary { get; init; }
    public string? SecularTrendsSummary { get; init; }
    public string? VaccineDescription { get; init; }
    public string? VaccinationScheduleSummary { get; init; }

    /// <summary>Not every chapter has this as its own section - some (e.g. Diphtheria) fold efficacy into the schedule discussion, while others (e.g. Pertussis, Tetanus) give it a distinct "Immunogenicity and Vaccine Efficacy" subsection worth keeping separate.</summary>
    public string? VaccineEfficacySummary { get; init; }

    /// <summary>
    /// The chapter's own criteria for considering someone immune WITHOUT a vaccination record -
    /// birth year cutoffs, serologic evidence, or lab-confirmed disease. Only the vaccine-preventable
    /// diseases with a meaningful "presumptive immunity" concept have this (so far: Measles, Mumps,
    /// Rubella - each with its own criteria despite sharing MMR as the vaccine). Conceptually this is
    /// the human-readable counterpart to AntigenImmunityData/ImmunityBirthDateRule already modeled in
    /// OpenCdsi.VaxEngine.Core.ReferenceData - that's the machine-evaluable version of the same CDC rule.
    /// </summary>
    public string? EvidenceOfImmunitySummary { get; init; }
    public string? ContraindicationsSummary { get; init; }
    public string? VaccineSafetySummary { get; init; }
    public string? VaccineStorageSummary { get; init; }
    public string? SurveillanceSummary { get; init; }

    /// <summary>Short standalone facts, pulled from the chapter's own sidebar "quick facts" boxes - meant for a bulleted list, not prose.</summary>
    public required IReadOnlyList<string> KeyPoints { get; init; }

    /// <summary>
    /// Real chapter content that doesn't fit any of the fixed fields above - a special-population
    /// consideration (e.g. pregnancy), a wound-management decision table, an outbreak-response
    /// recommendation - things that are genuinely chapter-specific rather than missing curation.
    /// Titled rather than a single free-text blob so a consumer can render each as its own card
    /// instead of an undifferentiated wall of text. Expect this list to suggest new first-class
    /// fields once a topic recurs across enough chapters to be worth promoting (see
    /// VaccineEfficacySummary, itself promoted out of here after Pertussis and Tetanus both
    /// needed it).
    /// </summary>
    public required IReadOnlyList<SupplementalTopic> SupplementalTopics { get; init; }

    public required ChapterSource Source { get; init; }
}

/// <summary>One titled, chapter-specific topic that didn't warrant its own field on AntigenChapter - see SupplementalTopics.</summary>
public sealed class SupplementalTopic
{
    public required string Title { get; init; }
    public required string Summary { get; init; }
}

/// <summary>
/// Provenance for one AntigenChapter. Kept separate from the summaries themselves because CDC
/// content gets revised on its own schedule (a new Pink Book edition, or a web-only chapter
/// update) independent of this library's own release cadence - Url is each chapter's own
/// canonical CDC page (printed in the chapter's page footer in the source PDF), so a reader can
/// always jump to the current, authoritative text rather than trusting a possibly-stale summary.
/// </summary>
public sealed class ChapterSource
{
    public required string Edition { get; init; }
    public string? ChapterAuthors { get; init; }
    public required string Url { get; init; }
    public required DateOnly PublishedDate { get; init; }
}
