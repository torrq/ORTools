using System.Text.Json;
using System.Text.Json.Serialization;

namespace ORTools.Shared.Protocol;

/// <summary>
/// Wire format: every message is a single newline-terminated JSON line.
/// {"t":"MessageType","p":{...typed payload...}}
///
/// Both PipeServer (Worker) and WorkerService (UI) read line-by-line and
/// parse through this class.
/// </summary>
public sealed class IpcEnvelope
{
    [JsonPropertyName("t")]
    public string Type { get; set; } = "";

    [JsonPropertyName("p")]
    public JsonElement? Payload { get; set; }

    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented               = false
    };

    /// <summary>Wrap a typed message into a single-line JSON string ready to send.</summary>
    public static string Wrap<T>(T message) where T : IIpcMessage
    {
        var envelope = new IpcEnvelope
        {
            Type    = message.Type,
            Payload = JsonSerializer.SerializeToElement(message, typeof(T), Options)
        };
        return JsonSerializer.Serialize(envelope, Options);
    }

    /// <summary>Parse one line from the pipe. Returns null on any parse failure.</summary>
    public static IpcEnvelope? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        try   { return JsonSerializer.Deserialize<IpcEnvelope>(line, Options); }
        catch { return null; }
    }

    /// <summary>Deserialize the payload as a specific message type.</summary>
    public T? As<T>() where T : class
    {
        if (Payload is null) return null;
        try   { return JsonSerializer.Deserialize<T>(Payload.Value.GetRawText(), Options); }
        catch { return null; }
    }
}
