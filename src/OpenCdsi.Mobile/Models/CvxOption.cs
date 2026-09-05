namespace OpenCdsi.Mobile.Models;

// A selectable row in AddDosePage's vaccine search. IsUnspecified flags CVX
// codes like "DTaP, unspecified formulation" — real codes providers do use
// when the specific product isn't known, not a placeholder/error state.
public record CvxOption(string Code, string DisplayName, bool IsUnspecified = false);
