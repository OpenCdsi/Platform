/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using OpenCdsi.VaxEngine.Core.Models;
using OpenCdsi.VaxEngine.Core.ReferenceData;

namespace OpenCdsi.VaxEngine.Core.Evaluation;

/// <summary>
/// §7.5 FORECASTGUIDANCE-1: administrative guidance text for a forecast, aggregated from three
/// sources - the series' own regimen guidance (always included), indication guidance (only for
/// indications the patient actually has an active observation for), and contraindication
/// guidance (only for contraindications the patient actually has an active observation for).
///
/// Note the rule's exact wording is "active patient observation" - not "active observation OR
/// adverse reaction." Unlike EvaluateContraindications' applicability check (which deliberately
/// checks both buckets, since the underlying data can't distinguish which a given code
/// represents), FORECASTGUIDANCE-1 is specific enough to implement literally: only
/// Patient.ActiveObservations is checked here, not AdverseReactions.
/// </summary>
public static class GenerateForecastGuidance
{
    public static IReadOnlyList<string> Execute(
        AntigenSeries series,
        Patient patient,
        IReadOnlyList<AntigenContraindication> antigenContraindications,
        IReadOnlyList<VaccineContraindication> vaccineContraindications)
    {
        bool HasActiveObservation(string? code) => patient.ActiveObservations.Any(o => o.Code == code);

        // Regimen guidance for the series being forecast - always included.
        var regimen = series.SeriesAdminGuidance;

        // Indication guidance, only where the patient has a matching active observation. A null
        // observation code never matches (checked explicitly, as the original did).
        var indications = series.Indications
            .Where(i => i.Guidance is not null && i.ObservationCode is not null && HasActiveObservation(i.ObservationCode))
            .Select(i => i.Guidance!);

        // Contraindication guidance, only where the patient has a matching active observation.
        var antigenLevel = antigenContraindications
            .Where(c => c.ContraindicationGuidance is not null && HasActiveObservation(c.ObservationCode))
            .Select(c => c.ContraindicationGuidance!);
        var vaccineLevel = vaccineContraindications
            .Where(c => c.ContraindicationGuidance is not null && HasActiveObservation(c.ObservationCode))
            .Select(c => c.ContraindicationGuidance!);

        // Order and duplicates are part of the output: callers get this exact sequence. ToList
        // (not ToArray) keeps the returned runtime type identical to before the refactor.
        return regimen.Concat(indications).Concat(antigenLevel).Concat(vaccineLevel).ToList();
    }
}
