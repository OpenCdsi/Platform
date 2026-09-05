using OpenCdsi.VaxEngine.Mobile.Models;

namespace OpenCdsi.VaxEngine.Mobile.Services;

// A handful of hand-typed sample entries, not the real CVX code list or a
// curated alias table — enough to exercise AddDosePage's search UI, not to
// actually record real doses against. Replace with a real lookup (the full
// CVX table, ideally with the alias mapping already discussed) before this
// app is used on an actual patient.
public class CvxLookupService
{
    private static readonly IReadOnlyList<CvxOption> All = new List<CvxOption>
    {
        new("03", "MMR"),
        new("21", "Varicella"),
        new("10", "IPV"),
        new("20", "DTaP"),
        new("107", "DTaP, unspecified formulation", IsUnspecified: true),
        new("133", "Pneumococcal conjugate (PCV13)"),
        new("08", "Hepatitis B"),
        new("88", "Influenza, unspecified formulation", IsUnspecified: true),
        new("119", "Rotavirus, unspecified formulation", IsUnspecified: true),
        new("17", "Hib"),
    };

    public IReadOnlyList<CvxOption> Search(string query)
        => string.IsNullOrWhiteSpace(query)
            ? All
            : All.Where(o => o.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
}
