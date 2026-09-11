/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using OpenCdsi.ClinicalReference.Models;

namespace OpenCdsi.ClinicalReference;

/// <summary>
/// Convenience loader: reads every chapter JSON file in a directory and holds the combined
/// in-memory catalog, keyed by antigen name - same shape as OpenCdsi.VaxEngine.Core's
/// ReferenceDataRepository.Load, and for the same reason: rebuilding this from a mounted data
/// directory (not a code change) is how a content refresh - a new Pink Book edition, or one more
/// curated chapter - gets picked up.
/// </summary>
public sealed class ClinicalReferenceRepository
{
    public required IReadOnlyDictionary<string, AntigenChapter> ChaptersByAntigen { get; init; }

    /// <summary>Not every antigen in the CDSi catalog has a curated chapter yet (this Pink Book edition doesn't cover several newer or travel-only antigens) - callers should treat a missing chapter as "no reference content available" rather than an error.</summary>
    public AntigenChapter? TryGetByAntigen(string antigenKey) => ChaptersByAntigen.GetValueOrDefault(antigenKey);

    public static ClinicalReferenceRepository Load(string chaptersDirectory)
    {
        var chaptersByAntigen = new Dictionary<string, AntigenChapter>();

        foreach (var file in Directory.EnumerateFiles(chaptersDirectory, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            var chapter = ClinicalReferenceLoader.LoadFile(file);
            chaptersByAntigen[chapter.AntigenKey] = chapter;
        }

        return new ClinicalReferenceRepository { ChaptersByAntigen = chaptersByAntigen };
    }
}
