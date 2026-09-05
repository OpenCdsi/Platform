using OpenCdsi.VaxEngine.Core.ReferenceData;

namespace OpenCdsi.VaxEngine.Mobile.Services;

// Loads OpenCdsi.VaxEngine.Core's reference data once, at app startup (see MauiProgram.cs), and
// holds it for the app's lifetime. VaxEngineForecastService and CvxLookupService both need the
// same in-memory catalog - loading it once here instead of once each avoids parsing the same
// ~2.6MB of CDC XML twice.
public class ReferenceDataStore
{
    private ReferenceDataRepository? _repository;

    public ReferenceDataRepository Repository =>
        _repository ?? throw new InvalidOperationException(
            $"{nameof(ReferenceDataStore)}.{nameof(LoadAsync)} must complete before this is used " +
            "(MauiProgram.cs loads it synchronously during startup, before any page can resolve " +
            "a service that depends on it).");

    public async Task LoadAsync(CancellationToken ct = default)
        => _repository = await ReferenceDataProvisioner.LoadAsync(ct);
}
