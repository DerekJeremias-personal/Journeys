namespace CampaignContextAudit.Budget;

public sealed record StableSizes(int PersonaChars, int SharedChars, int CoreChars, int PhaseChars, string PhaseFile)
{
    public int TotalStableChars => PersonaChars + SharedChars + CoreChars + PhaseChars;
}

public sealed class SegmentSizer
{
    private readonly string _governanceDir;
    private readonly bool _dataWarehouseEnabled;
    private readonly Dictionary<string, int> _fileCharCache = new(StringComparer.OrdinalIgnoreCase);

    public SegmentSizer(string governanceDir, bool dataWarehouseEnabled = true)
    {
        _governanceDir = governanceDir;
        _dataWarehouseEnabled = dataWarehouseEnabled;
    }

    public StableSizes StableSizesForPhase(string? phase)
    {
        var phaseFile = GovernancePhaseMap.GetPhaseFileName(phase, _dataWarehouseEnabled);
        return new StableSizes(
            FileChars(GovernancePhaseMap.PersonaFileName),
            FileChars(GovernancePhaseMap.SharedFileName),
            FileChars(GovernancePhaseMap.CoreFileName),
            FileChars(phaseFile),
            phaseFile);
    }

    private int FileChars(string fileName)
    {
        if (_fileCharCache.TryGetValue(fileName, out var cached)) return cached;
        var path = Path.Combine(_governanceDir, fileName);
        var chars = File.Exists(path) ? File.ReadAllText(path).Trim().Length : 0;
        _fileCharCache[fileName] = chars;
        return chars;
    }
}
