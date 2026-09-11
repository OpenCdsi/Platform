/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Text.Json;
using OpenCdsi.ClinicalReference.Models;

namespace OpenCdsi.ClinicalReference;

/// <summary>Loads one antigen chapter JSON file (see data/pinkbook/*.json) into an AntigenChapter.</summary>
public static class ClinicalReferenceLoader
{
    // Property names in the JSON files are camelCase (hand-authored content, not generated) -
    // case-insensitive matching avoids needing a [JsonPropertyName] on every AntigenChapter property.
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static AntigenChapter LoadFile(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<AntigenChapter>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"'{path}' deserialized to null.");
    }
}
