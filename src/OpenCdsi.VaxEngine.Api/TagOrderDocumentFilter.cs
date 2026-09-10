/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OpenCdsi.VaxEngine.Api;

/// <summary>
/// Pins the order of the tag sections in Swagger UI. Swagger UI (no <c>tagsSorter</c> configured,
/// which is Swashbuckle's default) renders tag groups in the order they appear in the document's
/// root <c>tags</c> array, falling back to first-seen order for anything not listed there.
/// Swashbuckle emits operations ordered by route, so without this the alphabetically-earlier
/// "/api/v3/..." reference-data paths make "Supporting Data" the first tag encountered and
/// "VaxEngine" (health/forecast/evaluate) sorts below it. This writes an explicit ordered root
/// <c>tags</c> array so "VaxEngine" comes first.
/// </summary>
public sealed class TagOrderDocumentFilter : IDocumentFilter
{
    private static readonly string[] Order = ["VaxEngine", "Supporting Data"];

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        int Rank(string? name)
        {
            var index = Array.IndexOf(Order, name);
            return index < 0 ? int.MaxValue : index;
        }

        // Every tag actually referenced by an operation, in the order Order lists them, with any
        // unlisted tag kept (ordinal-sorted) after the known ones rather than silently dropped.
        var usedTagNames = swaggerDoc.Paths.Values
            .SelectMany(path => path.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            .SelectMany(operation => operation.Tags ?? Enumerable.Empty<OpenApiTagReference>())
            .Select(tag => tag.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct()
            .OrderBy(Rank)
            .ThenBy(name => name, StringComparer.Ordinal);

        var orderedTags = new SortedSet<OpenApiTag>(Comparer<OpenApiTag>.Create((a, b) =>
        {
            var byRank = Rank(a.Name).CompareTo(Rank(b.Name));
            return byRank != 0 ? byRank : string.CompareOrdinal(a.Name, b.Name);
        }));

        foreach (var name in usedTagNames)
        {
            orderedTags.Add(new OpenApiTag { Name = name });
        }

        swaggerDoc.Tags = orderedTags;
    }
}
