# OpenCdsi VaxEngine: agent instructions

## Hard rules
- Never commit directly to main. Work on a branch or worktree and merge via PR.
- Baseline first: before changing engine code, run `scripts/check-baseline.sh`
  on the unmodified checkout. If it doesn't match, stop and report; the
  baseline is stale and results can't be trusted.
- Behavior-preserving changes (refactors, cleanups): work in small steps and
  commit only when the baseline matches exactly (zero regressions). If a
  change alters any result, revert it and report; don't "fix forward".
- Intentional behavior changes (bug fixes, spec corrections): keep them in
  their own commits and PRs, never mixed with refactoring. Report every case
  whose result changes, and regenerate the baseline with `--update` in the
  same commit so the `engine-baseline.tsv` diff shows the change for review.
- Stop and ask before changing any public signature used by Api, Contracts,
  Demo, Mobile, or ClinicalReference.

## Target style
- No in-place mutation. Use `init` properties; use `record` and `with` for
  new internal state (e.g. fold accumulators). Keep existing model and result
  types (`AntigenSeries`, `SeriesHistoryResult`, etc.) as `sealed class`:
  stages rely on their reference identity (`==`, dictionary keys), and record
  value equality would change results.
- Stage functions take inputs and return new results; no shared mutable fields.
- Side effects (XML loading, files) only at the edges; the core sees plain data.
- Keep the existing exceptions (e.g. missing immunity/contraindication data,
  malformed reference XML); Api and Demo depend on them. New code may return
  result types for expected outcomes, but don't convert existing throws
  without asking.
- Keep nullable dates where "empty" is meaningful (Table 7-12). Don't
  substitute sentinel dates there.

## Build and test
- Never run a bare `dotnet build` (four .slnx files). Use `Engine.slnx` for the
  core and conformance tests, `Backend.slnx` for the API, `Platform.slnx` for everything.
- `dotnet test Engine.slnx` includes the conformance corpus.
- `scripts/check-baseline.sh` runs `Engine.slnx` and diffs every case against
  `tests/baseline/engine-baseline.tsv` (known failures included). This is the
  regression gate: commit only when it prints "Matches baseline". Use
  `--update` only for an intentional behavior change (see Hard rules), never
  to make an unexplained mismatch go away.
- Api.Tests needs `Platform.slnx` at the repo root to locate `data/`.

## Conventions
- Every new `.cs` file starts with the MPL 2.0 header used across the repo.
- Doc comments explain *why*, not *what*. Flag spec inferences in comments
  rather than silently guessing.
- Don't "correct" deliberate oddities documented in DEVLOG.md (e.g. the §8.6
  sign inversion, the recurring-dose targetIdx trick). Read the relevant
  DEVLOG section before touching a function.
- CDC data in `data/supportingdata` is external and not ours; never edit it.
- Keep it simple: no new features or scope creep beyond the task at hand.
