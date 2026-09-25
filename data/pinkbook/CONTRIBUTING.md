# Contributing to data/pinkbook

This directory holds one JSON file per CDC Pink Book antigen chapter,
feeding the `OpenCdsi.ClinicalReference` library
(`src/OpenCdsi.ClinicalReference/`). Read `NOTICE` in this directory first
for licensing context.

## There is no curation script

Every file here was produced by hand: a contributor (working with Claude)
read the actual chapter text from the CDC's PDF and wrote a condensed
summary into the schema defined in `AntigenChapter.cs`. There is no program
that extracts, parses, or regenerates this data — the PDF's two-column
layout with interleaved sidebar callouts extracts too messily for that to be
worthwhile, and more fundamentally, deciding what's clinically load-bearing
versus boilerplate is a judgment call a script can't make reliably.

## The schema

`src/OpenCdsi.ClinicalReference/Models/AntigenChapter.cs` defines the shape
every chapter file follows: fixed fields for the sections nearly every
chapter has (`Organism`, `ClinicalFeaturesSummary`, `EpidemiologySummary`,
`VaccinationScheduleSummary`, `ContraindicationsSummary`,
`VaccineSafetySummary`, etc.), plus two escape hatches:

- `KeyPoints` — short standalone facts pulled from the chapter's own sidebar
  callout boxes.
- `SupplementalTopics` — titled, chapter-specific content that doesn't fit
  the fixed fields (a wound-management table, a pregnancy-specific note, an
  outbreak-response protocol).

The schema grew twice during the original curation pass, both times because
the same kind of content kept showing up in `SupplementalTopics` across
multiple chapters:

- `VaccineEfficacySummary` was promoted out of `SupplementalTopics` after
  Pertussis and Tetanus both turned out to have a distinct "Immunogenicity
  and Vaccine Efficacy" subsection.
- `EvidenceOfImmunitySummary` was promoted after Measles, Mumps, and Rubella
  all needed to express "who counts as presumptively immune without a
  vaccination record" (birth-year cutoffs, serology, lab-confirmed disease).

Follow the same pattern: when curating a new chapter, put content that
doesn't fit an existing field into `SupplementalTopics` first. Only add a
new first-class field once the same kind of content recurs across two or
three chapters — a one-off doesn't need a new field.

## Two source formats, not one

Most chapters here (17 of 18) come from the static 14th-edition (Aug 2021)
Pink Book PDF, which CDC has not meaningfully revised since — its own
Pneumococcal Disease chapter page even says outright "Vaccine-specific
recommendations may be outdated." For anything newer, CDC's actual current
source is the **Pink Book Web-on-Demand Series**
(`www2.cdc.gov/vaccines/ed/pinkbook/<year>/pb_<antigen>/PB_<Antigen>.pdf`,
also listed at `cdc.gov/immunization-training/hcp/pink-book-education-series/`)
— annually refreshed training-deck PDFs, one per chapter topic, that actually
track ACIP's latest votes. `rsv.json` was curated from that series (no
static-book RSV chapter exists at all — RSV immunization products postdate
the 2021 edition). If you're refreshing an *existing* chapter rather than
adding a new one, check whether its Web-on-Demand deck has diverged from the
static book before assuming the static PDF is still current — see
"Updating for a new Pink Book edition" below.

The two formats extract differently: the static book is prose with sidebar
callout boxes; the Web-on-Demand decks are slide bullets throughout, which
actually makes `KeyPoints` extraction easier but means the prose summary
fields need more synthesis (turning bullets into sentences) rather than
mostly condensing existing paragraphs.

## Curating one chapter, step by step

1. Extract the PDF's text with layout preserved (poppler's `pdftotext` was
   used here):

   ```bash
   pdftotext -layout "<path to the PDF>" full.txt
   ```

   Do this once per source document; work from the extracted text file, not
   the PDF directly — grep and sed are far faster on plain text.

2. Find the chapter. In the static book, chapter titles repeat as running
   page headers (e.g. "Diphtheria" printed at the top of every page in that
   chapter), and each chapter's first page has a byline plus a footer with
   its canonical CDC URL and revision date — both good grep anchors. A
   Web-on-Demand deck is already a single chapter's worth of PDF, so this
   step is a non-issue — just extract and read straight through.

3. Read through the chapter's real section order. The static book follows:
   Organism → Pathogenesis → Clinical Features → Epidemiology → Secular
   Trends → Vaccine(s) → Vaccination Schedule and Use → Contraindications
   and Precautions → Vaccine Safety → Vaccine Storage and Handling →
   Surveillance and Reporting — noting the sidebar "quick facts" callouts as
   you go (they're `KeyPoints` candidates). A Web-on-Demand deck is
   organized by numbered sections instead (disease burden → product
   characteristics → schedule/recommendations → safety → storage →
   resources) — same underlying content, different order and format, so map
   each section to the schema field it actually matches rather than assuming
   the static book's order.

4. Write the JSON file. Match an existing file's structure and tone —
   condensed but specific (real numbers, not vague qualifiers), no marketing
   language, and curated/summarized rather than copy-pasted verbatim CDC
   paragraphs (see `NOTICE`). Confirm `antigenKey` matches the real
   `<targetDisease>` value in the corresponding
   `data/supportingdata/antigens/AntigenSupportingData-*.xml` file exactly:

   ```bash
   grep -m1 "<targetDisease>" data/supportingdata/antigens/AntigenSupportingData-_<Antigen>-508.xml
   ```

   This is what lets a forecast result's antigen name look a chapter up
   directly — a mismatch here fails silently (the lookup just returns
   `null`), so always check it.

5. Validate the JSON and run the tests:

   ```bash
   python3 -c "import json; json.load(open('data/pinkbook/<antigen>.json'))"
   dotnet test tests/OpenCdsi.ClinicalReference.Tests/OpenCdsi.ClinicalReference.Tests.csproj
   ```

6. Extend `ClinicalReferenceRepositoryTests.cs` — bump
   `Load_ReadsChapterCount_MatchingCuratedFiles`'s expected count, and add
   the new antigen to the `Load_ReadsEveryChapterFile_ForEachCuratedAntigen`
   theory (antigen key plus an expected `Organism` prefix is enough to catch
   a load/parse regression). That shared theory also asserts every chapter's
   `Source.PublishedDate` is the 14th edition's Aug 2021 date — if you're
   curating an entirely new chapter from a different source (a Web-on-Demand
   deck with no static-book counterpart, a later edition), don't force it
   into that theory; write a dedicated fact instead, the way
   `Load_ReadsRsvChapter_FromTheWebOnDemandSeriesSource` does, asserting the
   real source date and edition string instead.

## Chapters not yet covered

18 chapters are curated here, covering 19 of the 30 antigens in
`data/supportingdata/antigens/`: the Pink Book 14th edition's 17 static
chapters (18 antigen keys, since the Meningococcal Disease chapter covers
both `Meningococcal` and `Meningococcal B`), plus `RSV` from the
Web-on-Demand Series (see "Two source formats, not one" above). Eleven
antigens still have no chapter: `COVID-19` and `Orthopoxvirus` (mpox) have
Web-on-Demand Series sessions per CDC's Education Series listing, but their
live PDF URLs weren't confirmed as of RSV's curation (unlike RSV/HPV/Pneumo/
HepB/Varicella, whose `pb_<antigen>` URLs resolve directly — try the same
pattern, or the JS-rendered training page, before assuming they don't
exist); `Chikungunya`, `Cholera`, `Dengue`, `Ebola`, `JE`, `Rabies`, `TBE`,
`Typhoid`, and `YF` are travel vaccines CDC covers in the Yellow Book
instead, outside Pink Book's scope entirely. `ClinicalReferenceRepository.
TryGetByAntigen` returns `null` for all eleven by design — that's expected,
not a bug, per its own doc comment.

## Updating for a new Pink Book edition

There's no "regenerate" command — refreshing this data means re-running the
process above against whatever's actually current, chapter by chapter. Two
different update patterns have come up in practice; pick whichever matches
what you actually found:

**Pattern A — amend in place, using a targeted primary source.** This is
what `pneumococcal.json`, `meningococcal.json`, `meningococcal_b.json`,
`polio.json`, `zoster.json`, `influenza.json`, `hepb.json` (its adult 19-59
recommendation only — see Pattern C for its birth-dose situation), and
`hib.json` went through (Sept 2026). For Pneumococcal specifically, the CDC Pink Book
Web-on-Demand deck turned out to be unreachable (repeated 404s despite search
engines indexing the URL — CDC's ASP.NET site appears to have reorganized or
removed some deck paths since RSV's curation), so rather than block on
finding it, the actual **ACIP MMWR recommendation report** for the specific
change was used directly instead — a more authoritative primary source than
a training deck anyway, since the decks themselves summarize these same
reports; every later Pattern A amendment used an MMWR directly from the
start rather than trying the deck path first. Concretely: PCV15/PCV20/PCV21
and ACIP's October 2024 age-50+ expansion came from `mmwr_pcv50` (MMWR
2025;74:1-8); GSK's pentavalent Penmenvy came from `mmwr_penmenvy` (MMWR
2026;75:6-14); the Bexsero MenB-4C schedule change came from the October
2024 MMWR cited inside `meningococcal_b.json`'s own `source.edition`; the
2023 universal-adult IPV recommendation (prompted by a 2022 vaccine-derived
poliovirus case in Rockland County, NY) came from MMWR 2023;72:1327-30; the
2021 RZV-for-immunocompromised-adults recommendation came from MMWR
2022;71:80-84; FluMist's 2024 self/caregiver-administration approval and
Flublok's expanded age range came from the 2025-26 seasonal influenza MMWR
(2025;74:1-30); the 2022 universal adult (19-59) HepB recommendation came
from MMWR 2022;71:477-83; and the September 2024 preferential recommendation
adding the hexavalent Vaxelis vaccine (alongside monovalent PRP-OMP) for
American Indian/Alaska Native infants' primary Hib series came from MMWR
2024;73:799-802. In this pattern, only the specific fields and
`SupplementalTopics` entries affected by the real change get rewritten — the
chapter's `source.publishedDate` stays the *original* 14th-edition date
(these are still fundamentally 2021 chapters), while `source.edition` is
extended to name the amending report(s) inline, so nothing false is
asserted about a new official edition existing. Locked in by
`Load_ReflectsPostLicensureAmendments_ViaTargetedMmwrCitations`.

**Pattern B — curate a wholesale new source**, the way `rsv.json` was
(RSV has no static-book chapter to amend at all). This is the pattern
described in "Two source formats, not one" above.

**Pattern C — check, but explicitly don't adopt, a contested source.** This
is what `hpv.json` went through (Sept 2026). A January 2026 HHS announcement
claimed a move to single-dose HPV vaccination, but the claim didn't survive
scrutiny as a source: it bypassed ACIP's normal evidence-review vote (which
had been underway since 2024 but hadn't concluded), and CDC's own live
Child and Adolescent Immunization Schedule Notes page still showed the
original 2-dose/3-dose schedule when checked directly. Per an explicit user
decision (this is a judgment call, not something to resolve unilaterally —
see below), `VaccinationScheduleSummary` kept following CDC's actually-
published schedule; the contested announcement was written up in its own
`SupplementalTopic` instead of silently adopted or silently ignored. Locked
in by `Load_KeepsPublishedScheduleAsPrimary_WhenAContestedAnnouncementConflictsWithIt`.
If you hit a source that conflicts with what CDC's own live pages actually
show — especially for a fact this clinically load-bearing (the dosing
schedule) — that's a case to surface to whoever you're working with rather
than pick a side on your own; there's more than one reasonable way to
represent contested guidance, and it isn't a technical judgment call the way
"which field does this content belong in" is.

The same pattern recurred twice more in the same update pass (Sept 2026),
once the ACIP process itself became visibly contested (committee membership
and structure were overhauled under new HHS leadership in 2025, producing
several truncated or reordered meetings): `hepb.json`'s birth dose (a
December 5, 2025 ACIP vote would delay it to 2 months old for HBsAg-negative
mothers' infants, reversing a policy in place since 1991 — but the vote
isn't self-executing, and CDC's published schedule still showed the
universal birth dose as of this check, so `VaccinationScheduleSummary`
follows the published schedule, with the vote documented in a
`SupplementalTopic`; locked in by
`Load_KeepsPublishedBirthDoseScheduleAsPrimary_WhenAContestedAcipVoteConflictsWithIt`),
and the MMRV-vs-separate-MMR/varicella schedule shared identically across
`measles.json`, `mumps.json`, `rubella.json`, and `varicella.json` (September
18-19, 2025 ACIP votes narrowed MMRV's Vaccines for Children program
formulary for the first dose — a real, concrete access restriction for about
half of US children under 4 — but CDC's published schedule notes still used
the older "preferred separately, may use MMRV if caregivers prefer" language
rather than a stronger "do not use" framing as of this check, so the
schedule fields are unchanged and the VFC-specific restriction is documented
in a `SupplementalTopic` in all four files; locked in by
`Load_DocumentsMmrvVfcRestriction_WithoutStrengtheningThePublishedPreference`).
Unlike the original HPV case, these two didn't need a fresh
`AskUserQuestion` round-trip — the same "follow what CDC actually publishes,
document the rest" judgment call had already been made once and generalizes
to any chapter in the same situation, so treat that as the default rather
than re-asking for every new instance of it. Only escalate again if a future
case doesn't cleanly fit this pattern (e.g., CDC's live page itself becomes
ambiguous or starts flip-flopping) rather than just being another vote
awaiting adoption.

**Status as of this update sweep (Sept 2026):** every chapter present as of
the 14th edition has now been checked against post-2021 ACIP activity, not
just the ones that turned out to need changes. `diphtheria.json`,
`pertussis.json`, `tetanus.json`, `hepa.json`, and `rotavirus.json` were
checked and found to have no material change worth amending — their
underlying ACIP recommendations have stayed stable. One nuance surfaced for
the Diphtheria/Pertussis/Tetanus family but was deliberately left alone: a
2019 ACIP vote (MMWR 2020;69:77-83, predating the 14th edition itself)
allows Tdap to substitute for Td in situations previously calling for Td
only (decennial boosters, wound management, catch-up). That's a real gap in
what the original 2021 curation captured, but it isn't *staleness* — nothing
changed about it since 2021 — so fixing it is a different kind of task
(auditing the original curation against its own source) than this sweep's
target (finding what's changed since). Worth doing at some point, just not
conflated with Pattern A/B/C amendment work.

To find out which pattern a given chapter needs:

1. Check whether the chapter's Web-on-Demand deck
   (`www2.cdc.gov/vaccines/ed/pinkbook/<year>/pb_<antigen>/PB_<Antigen>.pdf`)
   is actually reachable and has diverged from what's curated here — try it
   before assuming Pattern A is needed; it worked cleanly for RSV. If it
   404s, search for the specific MMWR recommendation report covering the
   change instead (search `"<topic>" MMWR cdc.gov <year>`, or check
   `cdc.gov/mmwr/` directly) and use Pattern A.
2. If CDC publishes an actual new static-book edition (all chapters at
   once, a new edition number), get the new PDF and re-run step 1 under
   "Curating one chapter" to produce a fresh `full.txt`, then diff each
   chapter's new prose against the existing `data/pinkbook/<antigen>.json`
   file — Pattern B applied across the whole book at once. Numbers age
   fastest — secular trend stats, safety percentages, dose schedules — so
   check those first.
3. Watch for a new subsection that recurs across chapters; that's the signal
   to add another field to `AntigenChapter.cs` rather than stuffing it into
   `SupplementalTopics` chapter by chapter (see "The schema" above).
4. Update `source.edition` (Pattern A: append the amending citation; Pattern
   B: replace wholesale) and `source.publishedDate` (Pattern A: leave alone;
   Pattern B: use the new source's real date) in every file you touch, and
   update `chapterAuthors`/`url` too if they changed.
5. If CDC publishes a chapter for an antigen already present in
   `data/supportingdata/antigens/` (a confirmed COVID-19 or mpox
   Web-on-Demand deck, for instance), that's a good time to curate a new
   file rather than waiting — the antigen key is already known.
