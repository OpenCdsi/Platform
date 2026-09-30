/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

namespace OpenCdsi.VaxEngine.Core.ReferenceData;

/// <summary>Convenience loader: reads every AntigenSupportingData-*.xml in a directory plus the Schedule file, and holds the combined in-memory catalog. This is the object your API's startup/DI wiring should build once (per your "easy updates" priority: rebuilding this from a mounted data volume, not a code change, is how a new CDC data drop gets picked up).</summary>
public sealed class ReferenceDataRepository
{
    public required IReadOnlyList<AntigenSeries> AllSeries { get; init; }
    public required ScheduleSupportingData Schedule { get; init; }

    /// <summary>Keyed by antigen name (matches AntigenSeries.Antigen) - everything GeneratePatientForecast needs to actually run the full pipeline, not just enumerate series.</summary>
    public required IReadOnlyDictionary<string, AntigenImmunityData> ImmunityByAntigen { get; init; }
    public required IReadOnlyDictionary<string, AntigenContraindicationData> ContraindicationsByAntigen { get; init; }
    public required IReadOnlyList<VaccineGroupInfo> VaccineGroups { get; init; }

    public static ReferenceDataRepository Load(string antigensDirectory, string scheduleFilePath)
    {
        // Edge: read every antigen file once, in ordinal filename order, and parse it
        // immediately (series, then immunity, then contraindications, as before) so a bad file
        // fails at the same point it always did.
        var files = Directory
            .EnumerateFiles(antigensDirectory, "AntigenSupportingData-*.xml")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(file =>
            {
                var root = AntigenSupportingDataLoader.LoadRoot(file);
                return (
                    Series: AntigenSupportingDataLoader.ParseSeriesList(root, file),
                    Immunity: AntigenSupportingDataLoader.ParseImmunityData(root),
                    Contraindications: AntigenSupportingDataLoader.ParseContraindicationData(root));
            })
            .ToArray();

        // Every real file defines exactly one antigen in practice, but key by whatever distinct
        // antigen names actually appear rather than assuming it, in case that ever isn't true for
        // some future data drop.
        var perAntigen = files
            .SelectMany(f => f.Series.Select(s => s.Antigen).Distinct()
                .Select(antigen => (Antigen: antigen, f.Immunity, f.Contraindications)))
            .GroupBy(x => x.Antigen)
            .ToArray();

        var schedule = ScheduleSupportingDataLoader.LoadFile(scheduleFilePath);
        var vaccineGroups = ScheduleSupportingDataLoader.LoadVaccineGroups(scheduleFilePath);

        return new ReferenceDataRepository
        {
            // ToList/ToDictionary keep the runtime types identical to before the refactor.
            AllSeries = files.SelectMany(f => f.Series).ToList(),
            Schedule = schedule,
            // Last() matches the old indexer's last-write-wins if an antigen spans several files;
            // GroupBy keeps its first-appearance position, as the indexer did.
            ImmunityByAntigen = perAntigen.ToDictionary(g => g.Key, g => g.Last().Immunity),
            ContraindicationsByAntigen = perAntigen.ToDictionary(g => g.Key, g => g.Last().Contraindications),
            VaccineGroups = vaccineGroups
        };
    }
}
