# OpenCdsi VaxEngine: agent instructions

## Current task: functional-style refactor of the engine (C#)
Restructure `OpenCdsi.VaxEngine.Core` into explicit pipeline stages built from
pure functions over immutable data. **Behavior must not change.** This is a
structural refactor only.

## Hard rules
- Work only on the refactor branch/worktree. Never touch main.
- Baseline first: run the full conformance suite, record the pass count and
  per-case results before changing anything.
- One stage at a time. After each, run the conformance and unit tests.
  Commit only if results match the baseline exactly (zero regressions).
- If a change alters any result, revert it and report; don't "fix forward".
- Stop and ask before changing any public signature used by Api, Contracts,
  Demo, or Mobile.

## Target style
- `record` types with `init` properties and `with` expressions; no in-place mutation.
- Stage functions take inputs and return new results; no shared mutable fields.
- Side effects (XML loading, files) only at the edges; the core sees plain data.
- Return result types instead of throwing for expected outcomes.
- Keep nullable dates where "empty" is meaningful (Table 7-12). Don't
  substitute sentinel dates there.

## Build and test
- Never run a bare `dotnet build` (four .slnx files). Use `Engine.slnx` for the
  core and conformance tests, `Backend.slnx` for the API, `Platform.slnx` for everything.
- `dotnet test Engine.slnx` includes the conformance corpus.
- `scripts/check-baseline.sh` runs `Engine.slnx` and diffs every case against
  `tests/baseline/engine-baseline.tsv` (known failures included). This is the
  per-stage regression gate: commit only when it prints "Matches baseline".
  Never use `--update` during the refactor unless explicitly told to.
- Api.Tests needs `Platform.slnx` at the repo root to locate `data/`.

## Conventions
- Every new `.cs` file starts with the MPL 2.0 header used across the repo.
- Doc comments explain *why*, not *what*. Flag spec inferences in comments
  rather than silently guessing.
- Don't "correct" deliberate oddities documented in DEVLOG.md (e.g. the §8.6
  sign inversion, the recurring-dose targetIdx trick). Read the relevant
  DEVLOG section before touching a function.
- CDC data in `data/supportingdata` is external and not ours; never edit it.
- Keep it simple: no new features or scope creep during the refactor.
