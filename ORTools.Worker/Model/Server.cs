using System.IO;

namespace ORTools.Worker;

// ClientDTO is defined in Client.cs

internal static class Server
{
    private static HashSet<string>? _cityList;

    public static void Initialize()
    {
        try   { EnsureCitiesFileExists(); }
        catch (Exception ex) { DebugLogger.Error(ex, $"Failed to initialize {AppConfig.CitiesFile}"); }
    }

    private static void EnsureCitiesFileExists()
    {
        string path = AppConfig.CitiesFile;
        string dir  = System.IO.Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        if (!File.Exists(path) || string.IsNullOrWhiteSpace(File.ReadAllText(path)))
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(AppConfig.DefaultCities, Formatting.Indented));
            DebugLogger.Info($"Created {path} with defaults");
        }
    }

    public static List<ClientDTO> GetLocalClients()
    {
        try
        {
            var clients = new List<ClientDTO>();
            foreach (var s in AppConfig.DefaultServers)
                clients.Add(new ClientDTO(s.name, s.description,
                    s.hpAddress, s.nameAddress, s.mapAddress, s.jobAddress, s.onlineAddress));
            return clients;
        }
        catch (Exception ex)
        {
            DebugLogger.Error(ex, "GetLocalClients failed");
            return new List<ClientDTO>();
        }
    }

    public static HashSet<string> GetCityList()
    {
        if (_cityList == null || _cityList.Count == 0)
        {
            try
            {
                string raw = File.Exists(AppConfig.CitiesFile)
                    ? File.ReadAllText(AppConfig.CitiesFile) : "";
                if (string.IsNullOrWhiteSpace(raw)) return new HashSet<string>();
                var list = JsonConvert.DeserializeObject<List<string>>(raw)!;
                _cityList = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                DebugLogger.Error(ex, "GetCityList failed");
                return new HashSet<string>();
            }
        }
        return _cityList;
    }
}
