namespace ORTools.Shared.Protocol;

/// <summary>Marker interface for all IPC messages (both commands and updates).</summary>
public interface IIpcMessage
{
    string Type { get; }
}
