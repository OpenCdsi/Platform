/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using OpenCdsi.ClinicalReference;
using OpenCdsi.ClinicalReference.Models;
using OpenCdsi.VaxEngine.Contracts.ReferenceData;

namespace OpenCdsi.VaxEngine.Api;

/// <summary>
/// GET /api/v3/antigens/{name}/ref and GET /api/v3/reference/{name} - both resolve to the same
/// curated Pink Book chapter (OpenCdsi.ClinicalReference), just reachable from two discovery
/// paths: nested under the existing antigen resource, and as its own standalone "Reference"
/// collection. Antigen name lookups are case-insensitive, matching every other name-keyed lookup
/// in this API (see AntigenEndpoints). A missing chapter is expected for antigens the current
/// Pink Book edition doesn't cover - see ClinicalReferenceRepository.TryGetByAntigen - so that's a
/// 404, not an error.
/// </summary>
public static class ChapterEndpoints
{
    public static void MapChapterEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/antigens/{name}/ref", (string name, ClinicalReferenceRepository refData) =>
            FindChapter(refData, name) is { } chapter ? Results.Ok(ReferenceDataMapping.ToDto(chapter)) : Results.NotFound())
            .WithName("GetAntigenReference")
            .WithTags("Supporting Data");

        group.MapGet("/reference/{name}", (string name, ClinicalReferenceRepository refData) =>
            FindChapter(refData, name) is { } chapter ? Results.Ok(ReferenceDataMapping.ToDto(chapter)) : Results.NotFound())
            .WithName("GetReferenceByAntigen")
            .WithTags("Reference");
    }

    private static AntigenChapter? FindChapter(ClinicalReferenceRepository refData, string antigenName) =>
        refData.ChaptersByAntigen.Values.FirstOrDefault(c => string.Equals(c.AntigenKey, antigenName, StringComparison.OrdinalIgnoreCase));
}
