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

## Curating one chapter, step by step

1. Extract the PDF's text with layout preserved (poppler's `pdftotext` was
   used here):

   ```bash
   pdftotext -layout "<path to the Pink Book PDF>" full.txt
   ```

   Do this once per edition; work from the extracted text file, not the PDF
   directly — grep and sed are far faster on plain text.

2. Find the chapter. Chapter titles repeat as running page headers (e.g.
   "Diphtheria" printed at the top of every page in that chapter), and each
   chapter's first page has a byline plus a footer with its canonical CDC
   URL and revision date — both good grep anchors.

3. Read through the chapter's real section order — Organism → Pathogenesis →
   Clinical Features → Epidemiology → Secular Trends → Vaccine(s) →
   Vaccination Schedule and Use → Contraindications and Precautions →
   Vaccine Safety → Vaccine Storage and Handling → Surveillance and
   Reporting — noting the sidebar "quick facts" callouts as you go (they're
   `KeyPoints` candidates).

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
   a load/parse regression).

## Chapters this edition doesn't cover

The Pink Book's 14th edition (2021) only covers the 17 antigen chapters
curated here. Eleven antigens in `data/supportingdata/antigens/` have no
chapter and never will from this edition, because it predates them or
they're outside its scope entirely — COVID-19, RSV, and Orthopoxvirus (mpox)
were added to CDC's
Pink Book only as later web-only supplements, and Chikungunya, Cholera,
Dengue, Ebola, JE, Rabies, TBE, Typhoid, and YF are travel vaccines covered
by CDC's Yellow Book instead. `ClinicalReferenceRepository.TryGetByAntigen`
returns `null` for all of these by design — that's expected, not a bug, per
its own doc comment.

## Updating for a new Pink Book edition

There's no "regenerate" command — refreshing this data means re-running the
process above against the new edition's text, chapter by chapter:

1. Get the new PDF and re-run step 1 above to produce a fresh `full.txt`.
2. For each chapter that changed, diff the new prose against the existing
   `data/pinkbook/<antigen>.json` file. Numbers age fastest — secular trend
   stats, safety percentages, dose schedules — so check those first.
3. Watch for a new subsection that recurs across chapters; that's the signal
   to add another field to `AntigenChapter.cs` rather than stuffing it into
   `SupplementalTopics` chapter by chapter (see "The schema" above).
4. Update `source.edition` and `source.publishedDate` in every file you
   touch, and update `chapterAuthors`/`url` too if CDC changed them.
5. If CDC adds a chapter for an antigen already present in
   `data/supportingdata/antigens/` (their web-only COVID-19/RSV/mpox
   chapters, for instance), that's a good time to curate a new file rather
   than waiting — the antigen key is already known.
