/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

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

        // All 17 Pink Book (14th ed.) antigen chapters, 18 antigen keys - the Meningococcal
        // Disease chapter covers two CDSi antigens (Meningococcal and Meningococcal B) in one
        // chapter, so it's curated as two separate files/keys. Bump this if a chapter from a
        // different source (a web-only supplement, a later edition) gets added.
        Assert.Equal(18, repo.ChaptersByAntigen.Count);
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
