namespace ORTools.Shared.Protocol;

/// <summary>Wire DTO for one autopot slot.</summary>
public sealed record AutopotSlotData(
    int Id,
    string Key,
    int Percent,
    bool Enabled);
