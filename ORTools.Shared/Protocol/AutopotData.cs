namespace ORTools.Shared.Protocol;

/// <summary>Wire-format for a single Autopot slot (used by both HP and SP updates).</summary>
public record AutopotSlotData(int Id, string Key, int Percent, bool Enabled);