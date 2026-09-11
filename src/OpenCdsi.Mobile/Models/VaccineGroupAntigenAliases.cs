/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

namespace OpenCdsi.Mobile.Models;

// A handful of forecast vaccine groups (VaccineGroupForecastResult.VaccineGroupName, surfaced here
// as ForecastEntry.AntigenName) combine several antigens the Pink Book still covers as separate
// chapters - "DTaP/Tdap/Td" spans Diphtheria, Tetanus, and Pertussis; "MMR" spans Measles, Mumps,
// and Rubella - so neither matches any single AntigenChapter key directly. This is a small,
// hand-maintained list of that handful of cases (verified against data/supportingdata/schedule's
// actual vaccineGroup names), not a generic mapping - every other vaccine group name already
// matches its AntigenChapter key exactly.
public static class VaccineGroupAntigenAliases
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Aliases =
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["DTaP/Tdap/Td"] = new[] { "Diphtheria", "Tetanus", "Pertussis" },
            ["MMR"] = new[] { "Measles", "Mumps", "Rubella" }
        };

    // The vaccine group name itself, when it isn't a known composite - the common case, where it
    // already matches its AntigenChapter key one-to-one.
    public static IReadOnlyList<string> AntigenKeysFor(string vaccineGroupName) =>
        Aliases.TryGetValue(vaccineGroupName, out var keys) ? keys : new[] { vaccineGroupName };
}
