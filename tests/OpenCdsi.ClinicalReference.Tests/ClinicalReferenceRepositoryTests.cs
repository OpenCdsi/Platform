/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Linq;
using OpenCdsi.ClinicalReference;
using Xunit;

namespace OpenCdsi.ClinicalReference.Tests;

public class ClinicalReferenceRepositoryTests
{
    private static string ChaptersDirectory => Path.Combine(AppContext.BaseDirectory, "TestData", "pinkbook");

    [Fact]
    public void Load_ReadsEveryChapterFile_KeyedByAntigen()
    {
        var repo = ClinicalReferenceRepository.Load(ChaptersDirectory);

        var diphtheria = repo.TryGetByAntigen("Diphtheria");

        Assert.NotNull(diphtheria);
        Assert.Equal("Diphtheria", diphtheria!.AntigenKey);
        Assert.StartsWith("Corynebacterium diphtheriae", diphtheria.Organism);
        Assert.NotEmpty(diphtheria.KeyPoints);
        Assert.StartsWith("Epidemiology and Prevention", diphtheria.Source.Edition);
        Assert.Equal(new DateOnly(2021, 8, 1), diphtheria.Source.PublishedDate);
    }

    [Fact]
    public void TryGetByAntigen_ReturnsNull_WhenNoCuratedChapterExists()
    {
        var repo = ClinicalReferenceRepository.Load(ChaptersDirectory);

        // Covered by the CDSi antigen catalog but not yet curated into a Pink Book chapter -
        // e.g. a travel vaccine this edition doesn't include. Missing content is expected, not an error.
        Assert.Null(repo.TryGetByAntigen("Yellow Fever"));
    }

    [Theory]
    [InlineData("Tetanus", "Clostridium tetani")]
    [InlineData("Pertussis", "Bordetella pertussis")]
    [InlineData("Measles", "Measles virus")]
    [InlineData("Mumps", "Mumps virus")]
    [InlineData("Rubella", "Rubella virus")]
    [InlineData("Varicella", "Varicella-zoster virus")]
    [InlineData("Zoster", "Herpes zoster")]
    [InlineData("HepA", "Hepatitis A virus")]
    [InlineData("HepB", "Hepatitis B virus")]
    [InlineData("Hib", "Haemophilus influenzae")]
    [InlineData("HPV", "Human papillomavirus")]
    [InlineData("Influenza", "Influenza is a single-stranded RNA virus")]
    [InlineData("Meningococcal", "Neisseria meningitidis")]
    [InlineData("Meningococcal B", "Neisseria meningitidis")]
    [InlineData("Pneumococcal", "Streptococcus pneumoniae")]
    [InlineData("Polio", "Poliovirus")]
    [InlineData("Rotavirus", "Rotavirus, a double-stranded RNA virus")]
    public void Load_ReadsEveryChapterFile_ForEachCuratedAntigen(string antigenKey, string expectedOrganismPrefix)
    {
        var repo = ClinicalReferenceRepository.Load(ChaptersDirectory);

        var chapter = repo.TryGetByAntigen(antigenKey);

        Assert.NotNull(chapter);
        Assert.Equal(antigenKey, chapter!.AntigenKey);
        Assert.StartsWith(expectedOrganismPrefix, chapter.Organism);
        Assert.NotEmpty(chapter.KeyPoints);
        Assert.Equal(new DateOnly(2021, 8, 1), chapter.Source.PublishedDate);
    }

    [Fact]
    public void Load_ReadsChapterCount_MatchingCuratedFiles()
    {
        var repo = ClinicalReferenceRepository.Load(ChaptersDirectory);

        // The 17 Pink Book (14th ed.) antigen chapters, 18 antigen keys - the Meningococcal
        // Disease chapter covers two CDSi antigens (Meningococcal and Meningococcal B) in one
        // chapter, so it's curated as two separate files/keys - plus RSV, Mpox, and COVID-19,
        // each curated from a source outside the static 14th edition book (see
        // Load_ReadsRsvChapter_FromTheWebOnDemandSeriesSource and
        // Load_ReadsMpoxAndCovidChapters_FromDirectMmwrSources). Bump this as more chapters are
        // curated.
        Assert.Equal(21, repo.ChaptersByAntigen.Count);
    }

    [Fact]
    public void Load_ReadsRsvChapter_FromTheWebOnDemandSeriesSource()
    {
        var repo = ClinicalReferenceRepository.Load(ChaptersDirectory);

        var rsv = repo.TryGetByAntigen("RSV");

        Assert.NotNull(rsv);
        Assert.StartsWith("Respiratory syncytial virus", rsv!.Organism);
        Assert.NotEmpty(rsv.KeyPoints);

        // RSV isn't in the 14th edition (Aug 2021) static book at all - the CDC didn't have
        // maternal/infant/older-adult RSV immunization products yet in 2021. This chapter was
        // curated from CDC's separately published, more frequently updated "Pink Book
        // Web-on-Demand Series" instead (see CONTRIBUTING.md), so its source doesn't match the
        // 2021 date every other chapter in this test file asserts.
        Assert.Contains("Web-on-Demand", rsv.Source.Edition);
        Assert.Equal(new DateOnly(2025, 12, 12), rsv.Source.PublishedDate);
        Assert.Contains(rsv.SupplementalTopics, t => t.Title == "Storage and Handling by Product");
    }

    [Fact]
    public void Load_ReadsMpoxAndCovidChapters_FromDirectMmwrSources()
    {
        var repo = ClinicalReferenceRepository.Load(ChaptersDirectory);

        var mpox = repo.TryGetByAntigen("Orthopoxvirus");
        var covid = repo.TryGetByAntigen("COVID-19");

        Assert.NotNull(mpox);
        Assert.NotNull(covid);
        Assert.StartsWith("Monkeypox virus", mpox!.Organism);
        Assert.StartsWith("Severe acute respiratory syndrome coronavirus 2", covid!.Organism);
        Assert.NotEmpty(mpox.KeyPoints);
        Assert.NotEmpty(covid.KeyPoints);

        // Neither Mpox nor COVID-19 has a Pink Book Web-on-Demand Series module as of this
        // curation (checked against CDC's "You Call the Shots" training listing, which omits
        // both) or a static 14th-edition chapter (both postdate it), so - like RSV - their
        // Source.PublishedDate is the real date of the direct MMWR source used instead, not the
        // 2021 date every static-book chapter in Load_ReadsEveryChapterFile_ForEachCuratedAntigen
        // asserts.
        Assert.Equal(new DateOnly(2025, 6, 19), mpox.Source.PublishedDate);
        Assert.Equal(new DateOnly(2024, 9, 13), covid.Source.PublishedDate);

        // COVID-19 is the one chapter in this library curated under live, unresolved litigation
        // over the underlying recommendation itself (see its own SupplementalTopic) - a stronger
        // and more explicit caveat than any Pattern C case, since even the "currently operative"
        // baseline could change on short notice. This assertion locks in that the caveat exists,
        // not that any particular schedule detail is "the" answer.
        Assert.Contains(covid.SupplementalTopics, t => t.Title.Contains("Active Litigation"));
    }

    [Theory]
    [InlineData("Pneumococcal", "PCV21")]
    [InlineData("Meningococcal", "Penmenvy")]
    [InlineData("Meningococcal B", "Penmenvy")]
    [InlineData("Polio", "cVDPV2")]
    [InlineData("Zoster", "immunocompromised")]
    [InlineData("Influenza", "FluMist")]
    [InlineData("Hib", "Vaxelis")]
    public void Load_ReflectsPostLicensureAmendments_ViaTargetedMmwrCitations(string antigenKey, string expectedNewFact)
    {
        var repo = ClinicalReferenceRepository.Load(ChaptersDirectory);

        var chapter = repo.TryGetByAntigen(antigenKey);

        Assert.NotNull(chapter);

        // Unlike RSV (an entirely different source - see
        // Load_ReadsRsvChapter_FromTheWebOnDemandSeriesSource), these three chapters were amended
        // IN PLACE: the base content is still the 14th edition (Source.PublishedDate stays
        // 2021-08-01), but specific fields and SupplementalTopics were updated using a targeted,
        // cited MMWR report covering a real post-2021 ACIP change (PCV15/20/21 and the October
        // 2024 age-50 expansion for Pneumococcal; GSK's pentavalent Penmenvy for both Meningococcal
        // chapters). Source.Edition names the amending report(s) rather than claiming a new
        // official Pink Book edition exists - CDC has not published one.
        Assert.Equal(new DateOnly(2021, 8, 1), chapter!.Source.PublishedDate);
        Assert.Contains("amended using CDC MMWR", chapter.Source.Edition);
        Assert.Contains(expectedNewFact, chapter.VaccineDescription + " " + string.Join(" ", chapter.SupplementalTopics.Select(t => t.Summary)));
    }

    [Fact]
    public void Load_KeepsPublishedScheduleAsPrimary_WhenAContestedAnnouncementConflictsWithIt()
    {
        var repo = ClinicalReferenceRepository.Load(ChaptersDirectory);

        var hpv = repo.TryGetByAntigen("HPV");

        Assert.NotNull(hpv);

        // A third amendment pattern, distinct from both RSV (wholesale new source) and
        // Pneumococcal/Meningococcal (amend in place using an accepted ACIP MMWR): in January
        // 2026 HHS announced a single-dose HPV schedule, but that announcement bypassed ACIP's
        // normal evidence-review vote, and CDC's own published Child and Adolescent Immunization
        // Schedule still showed the original 2-dose/3-dose schedule as of this chapter's last
        // check. Per an explicit user decision, VaccinationScheduleSummary keeps following CDC's
        // actually-published schedule rather than the disputed announcement - the announcement is
        // documented in its own SupplementalTopic, not treated as settled fact. A future curator
        // re-checking this chapter should confirm which schedule CDC is currently publishing
        // before changing either field.
        Assert.Contains("2-dose series", hpv!.VaccinationScheduleSummary);
        Assert.DoesNotContain("single dose", hpv.VaccinationScheduleSummary);
        Assert.Contains(hpv.SupplementalTopics, t => t.Title.Contains("Single-Dose Announcement"));
    }

    [Fact]
    public void Load_KeepsPublishedBirthDoseScheduleAsPrimary_WhenAContestedAcipVoteConflictsWithIt()
    {
        var repo = ClinicalReferenceRepository.Load(ChaptersDirectory);

        var hepB = repo.TryGetByAntigen("HepB");

        Assert.NotNull(hepB);

        // Same Pattern C treatment as HPV's single-dose announcement (see
        // Load_KeepsPublishedScheduleAsPrimary_WhenAContestedAnnouncementConflictsWithIt): a
        // December 5, 2025 ACIP vote would delay the birth dose for HBsAg-negative mothers'
        // infants to age 2 months, reversing a universal birth-dose policy in place since 1991 -
        // but that vote is not self-executing and CDC's own published schedule still showed the
        // original universal birth-dose recommendation as of this chapter's last check, so
        // VaccinationScheduleSummary keeps following the published schedule rather than the vote.
        Assert.Contains("within 24 hours of birth", hepB!.VaccinationScheduleSummary);
        Assert.Contains(hepB.SupplementalTopics, t => t.Title.Contains("December 2025 ACIP Vote"));
    }

    [Theory]
    [InlineData("Measles")]
    [InlineData("Mumps")]
    [InlineData("Rubella")]
    [InlineData("Varicella")]
    public void Load_DocumentsMmrvVfcRestriction_WithoutStrengtheningThePublishedPreference(string antigenKey)
    {
        var repo = ClinicalReferenceRepository.Load(ChaptersDirectory);

        var chapter = repo.TryGetByAntigen(antigenKey);

        Assert.NotNull(chapter);

        // September 18-19, 2025 ACIP votes restricted MMRV's Vaccines for Children program
        // formulary for the first dose in children 12-47 months, affecting all four of the MMRV
        // component chapters identically. As of each chapter's last check, CDC's own published
        // schedule notes still used the older "preferred separately, MMRV may be used if
        // caregivers prefer" language rather than a stronger "do not use" framing, so
        // VaccinationScheduleSummary is unchanged - the VFC-specific restriction is documented in
        // a SupplementalTopic instead (see the HPV/HepB Pattern C precedent).
        Assert.Contains("generally preferred over MMRV", chapter!.VaccinationScheduleSummary);
        Assert.Contains(chapter.SupplementalTopics, t => t.Title.Contains("MMRV Vaccine Access"));
    }

    [Fact]
    public void Load_SplitsMeningococcalChapter_IntoTwoDistinctAntigenKeys()
    {
        var repo = ClinicalReferenceRepository.Load(ChaptersDirectory);

        var menACWY = repo.TryGetByAntigen("Meningococcal");
        var menB = repo.TryGetByAntigen("Meningococcal B");

        Assert.NotNull(menACWY);
        Assert.NotNull(menB);
        Assert.NotEqual(menACWY!.DiseaseName, menB!.DiseaseName);
        Assert.Contains("MenACWY", menACWY.VaccineDescription);
        Assert.Contains("MenB", menB.VaccineDescription);
    }

    [Fact]
    public void Load_ReadsSupplementalTopics_ForChapterContentThatDoesntFitFixedFields()
    {
        var repo = ClinicalReferenceRepository.Load(ChaptersDirectory);

        var rubella = repo.TryGetByAntigen("Rubella");

        Assert.NotNull(rubella);
        Assert.Contains(rubella!.SupplementalTopics, t => t.Title == "Congenital Rubella Syndrome (CRS)");
    }

    [Fact]
    public void Load_LeavesEvidenceOfImmunitySummaryNull_ForChaptersWithNoPresumptiveImmunityConcept()
    {
        var repo = ClinicalReferenceRepository.Load(ChaptersDirectory);

        // "Evidence of immunity" (birth-year cutoffs, serology in lieu of a vaccination record) is
        // a real concept for the MMR-family diseases and Varicella, but not for toxoid vaccines
        // like Tetanus - there's no such thing as presumptive tetanus immunity from having been
        // born early enough. Zoster is the interesting third case: its own vaccine (RZV) explicitly
        // requires NO varicella-history screening at all, even though the chapter discusses
        // varicella immunity criteria in its postexposure-prophylaxis discussion - that's real
        // content, but it isn't Zoster's OWN vaccination criterion, so it belongs in
        // SupplementalTopics rather than this field.
        var tetanus = repo.TryGetByAntigen("Tetanus");
        var rubella = repo.TryGetByAntigen("Rubella");
        var varicella = repo.TryGetByAntigen("Varicella");
        var zoster = repo.TryGetByAntigen("Zoster");

        Assert.NotNull(tetanus);
        Assert.NotNull(rubella);
        Assert.NotNull(varicella);
        Assert.NotNull(zoster);
        Assert.Null(tetanus!.EvidenceOfImmunitySummary);
        Assert.NotNull(rubella!.EvidenceOfImmunitySummary);
        Assert.NotNull(varicella!.EvidenceOfImmunitySummary);
        Assert.Null(zoster!.EvidenceOfImmunitySummary);
    }
}
