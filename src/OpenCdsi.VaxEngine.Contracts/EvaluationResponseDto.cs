/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

namespace OpenCdsi.VaxEngine.Contracts;

/// <summary>
/// The full response body for POST /api/v3/evaluate (and the Functions "Evaluate" surface) - the
/// §4.4/§6 EVALUATION half of the CDSi process: how each already-administered dose graded out,
/// rather than the §7-§9 FORECAST half /forecast exposes. Same request shape (ForecastRequestDto)
/// as /forecast - see EvaluationResponseMapping.
/// </summary>
public sealed class EvaluationResponseDto
{
    public required string PatientId { get; init; }
    public required DateOnly AssessmentDate { get; init; }

    /// <summary>One entry per physical administered dose (collapsed by DoseId across antigens), in request order.</summary>
    public required IReadOnlyList<EvaluatedDoseDto> EvaluatedDoses { get; init; }

    /// <summary>
    /// Per-antigen series progress, for the antigens this patient's administered doses were
    /// actually evaluated against (not a full cross-antigen standing - that's /forecast). The
    /// natural bridge from "how did these doses grade" to "where does each of those series now
    /// stand". Ordered by antigen name.
    /// </summary>
    public required IReadOnlyList<AntigenEvaluationSummaryDto> AntigenSummaries { get; init; }
}

/// <summary>
/// One physical administered dose's evaluation outcome. A single CVX can associate to several
/// antigens (e.g. DTaP -> Diphtheria, Tetanus, Pertussis), each evaluated independently; this
/// collapses that to one headline the way a clinician reads an immunization record - one shot,
/// one status - with the per-antigen detail kept in <see cref="PerAntigen"/> and any disagreement
/// spelled out in <see cref="ConflictText"/>.
/// </summary>
public sealed class EvaluatedDoseDto
{
    public required string DoseId { get; init; }
    public required string Cvx { get; init; }
    public required DateOnly DateAdministered { get; init; }

    /// <summary>
    /// "Valid", "NotValid", "Extraneous", "SubStandard", or "NotEvaluated". When the dose's
    /// antigens disagree, this is the MOST PERMISSIVE graded status among them
    /// (Valid &gt; SubStandard &gt; NotValid &gt; Extraneous) and <see cref="HasAntigenConflict"/> is
    /// true. "NotEvaluated" means no antigen produced a grade at all - the dose only ever hit
    /// target doses that were Skipped, or its CVX associates only to antigens with no series
    /// relevant to this patient.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>True when the dose's antigens didn't all reach the same graded status.</summary>
    public required bool HasAntigenConflict { get; init; }

    /// <summary>Human-readable note naming the antigens whose status differs from the headline, e.g. "Pertussis: Not Valid (Too young)". Null when there's no conflict.</summary>
    public string? ConflictText { get; init; }

    /// <summary>The raw per-antigen breakdown this dose collapsed from. Includes Skipped entries (which don't affect the headline or count as a conflict).</summary>
    public required IReadOnlyList<AntigenDoseEvaluationDto> PerAntigen { get; init; }
}

/// <summary>One antigen's view of one administered dose - what <see cref="EvaluatedDoseDto"/> collapses from.</summary>
public sealed class AntigenDoseEvaluationDto
{
    public required string Antigen { get; init; }

    /// <summary>"Valid", "NotValid", "Extraneous", "SubStandard", or "Skipped" (the target dose didn't need this dose - not a failure).</summary>
    public required string Status { get; init; }

    /// <summary>Which target dose number in the series this administered dose was evaluated against. Null for a Skipped or series-already-complete (Extraneous) outcome.</summary>
    public int? TargetDoseNumber { get; init; }

    /// <summary>The engine's own evaluation-reason text where one exists (e.g. "Too young", "Grace period"). Reason-text humanization for patient communications is out of scope here - it's a separate project.</summary>
    public string? Reason { get; init; }
}

/// <summary>Per-antigen series progress after evaluating the patient's history - mirrors SeriesHistoryResult.</summary>
public sealed class AntigenEvaluationSummaryDto
{
    public required string Antigen { get; init; }
    public required bool SeriesComplete { get; init; }

    /// <summary>The next target dose number still needing to be satisfied. Null when the series is complete.</summary>
    public int? CurrentTargetDoseNumber { get; init; }
}
