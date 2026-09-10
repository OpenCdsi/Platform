/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using OpenCdsi.VaxEngine.Core.Evaluation;
using OpenCdsi.VaxEngine.Core.Models;
using OpenCdsi.VaxEngine.Core.Pipeline;
using OpenCdsi.VaxEngine.Core.ReferenceData;

// Loads the FULL real CDC catalog (all 30 antigens + schedule) and runs a few sample patients
// through GeneratePatientForecast end to end - the whole pipeline, real data, nothing mocked.
// §6.2's "Completed Series" condition is resolved internally via two evaluation passes -
// GeneratePatientForecast handles this on its own, no caller-supplied resolver needed anymore.
//
// Uses ExecuteWithDoseDetail so it can print BOTH halves of the process: the §7-§9 forecast
// (what's due next, per vaccine group) AND the §4.4/§6 evaluation (how each already-administered
// dose graded out, per antigen). This is deliberately the raw per-antigen view, not the
// collapsed-by-dose shape the /api/v3/evaluate endpoint returns - the antigen explosion (one
// combination shot -> one row per contained antigen) is the more instructive thing to see here.

var dataRoot = FindDataDirectory();
Console.WriteLine($"Loading full CDC catalog from: {dataRoot}");
var repo = ReferenceDataRepository.Load(Path.Combine(dataRoot, "antigens"), Path.Combine(dataRoot, "schedule", "ScheduleSupportingData.xml"));
Console.WriteLine($"Loaded {repo.AllSeries.Count} series across {repo.AllSeries.Select(s => s.Antigen).Distinct().Count()} antigens, {repo.VaccineGroups.Count} vaccine groups.");
Console.WriteLine();

// Chosen deliberately, not arbitrarily: real on-file seasonal windows for RSV (2025-10-01 to
// 2026-03-31) and Influenza (2025-07-01 to 2026-06-30) overlap here. An earlier run with an
// August assessment date correctly, but confusingly, showed both as "NotRecommended" (off
// season per real seasonal data - not a bug, see README) - mid-January avoids that so the demo's
// own output is easy to read at a glance instead of needing a season-staleness footnote.
var today = new DateOnly(2026, 1, 15);

RunPatient("Newborn, no doses yet", today, Array.Empty<VaccineDoseAdministered>());

RunPatient("2-month-old, birth-dose HepB only", today.AddMonths(-2), new[]
{
    new VaccineDoseAdministered { DoseId = "d1", Cvx = "08", DateAdministered = today.AddMonths(-2) }
});

var fifteenMonthOldDob = today.AddMonths(-15);
RunPatient("15-month-old, partway through routine schedule", fifteenMonthOldDob, new[]
{
    new VaccineDoseAdministered { DoseId = "d1", Cvx = "08", DateAdministered = fifteenMonthOldDob },                    // HepB birth dose
    new VaccineDoseAdministered { DoseId = "d2", Cvx = "08", DateAdministered = fifteenMonthOldDob.AddMonths(2) },       // HepB dose 2
    new VaccineDoseAdministered { DoseId = "d3", Cvx = "110", DateAdministered = fifteenMonthOldDob.AddMonths(2) },      // DTaP-HepB-IPV dose 1
    new VaccineDoseAdministered { DoseId = "d4", Cvx = "110", DateAdministered = fifteenMonthOldDob.AddMonths(4) },      // DTaP-HepB-IPV dose 2
});

// Chosen to make the evaluation output actually show something other than "Valid": DTaP dose 2
// given only ~2 weeks after dose 1 fails the minimum interval. CVX 20 is plain DTaP (Diphtheria +
// Tetanus + Pertussis), so one bad dose produces a "Not Valid" row for all three antigens - the
// antigen explosion working on a failure, not just a pass.
var toddlerDob = today.AddMonths(-18);
RunPatient("18-month-old, DTaP dose 2 given too soon after dose 1", toddlerDob, new[]
{
    new VaccineDoseAdministered { DoseId = "d1", Cvx = "20", DateAdministered = toddlerDob.AddMonths(2) },              // DTaP dose 1
    new VaccineDoseAdministered { DoseId = "d2", Cvx = "20", DateAdministered = toddlerDob.AddMonths(2).AddDays(14) },  // DTaP dose 2 - too soon
});

void RunPatient(string label, DateOnly dob, IReadOnlyList<VaccineDoseAdministered> doses)
{
    Console.WriteLine($"=== {label} (DOB {dob:yyyy-MM-dd}, assessed {today:yyyy-MM-dd}) ===");
    Console.WriteLine($"Doses administered: {doses.Count}");

    var patient = new Patient { PatientId = label, DateOfBirth = dob };

    var result = GeneratePatientForecast.ExecuteWithDoseDetail(
        patient, doses, repo.AllSeries, repo.Schedule, repo.VaccineGroups,
        repo.ImmunityByAntigen, repo.ContraindicationsByAntigen, today);
    var results = result.VaccineGroupForecasts;

    // --- §4.4/§6 evaluation: how each administered dose graded out ---
    if (doses.Count > 0)
    {
        Console.WriteLine("  Dose evaluation (per antigen - one administered dose can grade against several):");
        foreach (var (antigen, history) in result.DoseDetailsByAntigen.OrderBy(kv => kv.Key))
        {
            if (history.DoseResults.Count == 0)
            {
                continue;
            }
            var seriesState = history.SeriesComplete
                ? "series complete"
                : $"next target dose #{history.CurrentTargetDoseNumber}";
            Console.WriteLine($"    {antigen} ({seriesState})");
            foreach (var r in history.DoseResults)
            {
                var status = r.Result.TargetDoseStatus == TargetDoseStatus.Skipped
                    ? "Skipped"
                    : r.Result.EvaluationStatus?.ToString() ?? "Skipped";
                var target = r.Result.TargetDoseStatus == TargetDoseStatus.Skipped || r.TargetDoseNumber is null
                    ? ""
                    : $" vs target dose #{r.TargetDoseNumber}";
                var reason = string.IsNullOrEmpty(r.Result.Reason) ? "" : $" - {r.Result.Reason}";
                Console.WriteLine($"      CVX {r.AdministeredDose.Cvx} on {r.AdministeredDose.DateAdministered:yyyy-MM-dd}: {status}{target}{reason}");
            }
        }
        Console.WriteLine();
    }

    Console.WriteLine($"Vaccine group forecasts produced: {results.Count}");
    Console.WriteLine();

    foreach (var vg in results.OrderBy(r => r.VaccineGroupName))
    {
        Console.WriteLine($"  {vg.VaccineGroupName} ({vg.Type})");
        Console.WriteLine($"    Status: {vg.Status}   ShouldForecast: {vg.ShouldForecast}");
        if (vg.ShouldForecast)
        {
            Console.WriteLine($"    Dose #{vg.ForecastDoseNumber}: earliest {vg.EarliestDate:yyyy-MM-dd} | recommended {vg.AdjustedRecommendedDate:yyyy-MM-dd} | past due {vg.AdjustedPastDueDate:yyyy-MM-dd} | latest {vg.LatestDate:yyyy-MM-dd}");
            if (vg.RecommendedVaccineCvxCodes.Count > 0)
            {
                Console.WriteLine($"    Recommended CVX codes: {string.Join(", ", vg.RecommendedVaccineCvxCodes)}");
            }
            if (vg.AllPreferableVaccineCvxCodes.Count > 0)
            {
                Console.WriteLine($"    All clinically valid CVX codes: {string.Join(", ", vg.AllPreferableVaccineCvxCodes)}");
            }
        }
        Console.WriteLine();
    }

    Console.WriteLine();
}

static string FindDataDirectory()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Platform.slnx")))
    {
        dir = dir.Parent;
    }
    if (dir is null)
    {
        throw new InvalidOperationException("Couldn't find the repo root (Platform.slnx) walking up from the executable's directory - run this from within the cdsi-engine checkout.");
    }
    return Path.Combine(dir.FullName, "data");
}
