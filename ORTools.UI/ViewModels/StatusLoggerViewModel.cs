using CommunityToolkit.Mvvm.ComponentModel;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ORTools.UI.ViewModels;

public partial class StatusLoggerViewModel : ObservableObject
{
    private readonly WorkerService _worker;
    
    [ObservableProperty] private bool _logToFile;
    [ObservableProperty] private int _logFrequency;
    [ObservableProperty] private string _logFileName = "";
    
    [ObservableProperty] private bool _logName;
    [ObservableProperty] private bool _logLevel;
    [ObservableProperty] private bool _logJobLevel;
    [ObservableProperty] private bool _logExp;
    [ObservableProperty] private bool _logHp;
    [ObservableProperty] private bool _logMaxHp;
    [ObservableProperty] private bool _logSp;
    [ObservableProperty] private bool _logMaxSp;
    [ObservableProperty] private bool _logWeight;
    [ObservableProperty] private bool _logMaxWeight;
    [ObservableProperty] private bool _logMap;
    [ObservableProperty] private bool _logStatuses;

    public record LogFrequencyOption(int Seconds, string Label);
    public LogFrequencyOption[] LogFrequencies { get; } = 
    [
        new(1, "1 second"),
        new(2, "2 seconds"),
        new(5, "5 seconds"),
        new(10, "10 seconds"),
        new(30, "30 seconds"),
        new(60, "1 minute"),
        new(300, "5 minutes"),
        new(600, "10 minutes")
    ];

    private DateTime _lastLogTime = DateTime.MinValue;
    private bool _isApplicationOn = false;

    // Latest state
    private uint _currentHp;
    private uint _maxHp;
    private uint _currentSp;
    private uint _maxSp;
    private bool _suppressUpdates;

    // Cached translated headers — built on the UI thread so background logging never
    // touches Application.Current.Resources (Gotcha #6).
    private volatile List<string> _cachedHeaders;

    public StatusLoggerViewModel(WorkerService worker)
    {
        _worker = worker;
        _cachedHeaders = BuildHeaders();
        _worker.AppStateReceived += OnAppStateReceived;
        _worker.StatusLoggerConfigReceived += OnConfigReceived;
        _worker.CharacterReceived += OnCharacterStateReceived;
        _worker.HpSpReceived += OnHpSpReceived;
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>
    /// Rebuilds the cached headers when the language is switched.
    /// LanguageChanged fires on the UI thread, so LanguageService.Get() is safe here.
    /// </summary>
    private void OnLanguageChanged()
    {
        _cachedHeaders = BuildHeaders();
    }

    private void OnAppStateReceived(AppStateUpdate u)
    {
        _isApplicationOn = u.IsOn;
    }

    private void OnHpSpReceived(HpSpUpdate u)
    {
        _currentHp = u.CurrentHp;
        _maxHp = u.MaxHp;
        _currentSp = u.CurrentSp;
        _maxSp = u.MaxSp;
    }

    private void OnConfigReceived(StatusLoggerConfigUpdate u)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _suppressUpdates = true;
            LogToFile = u.LogToFile;
            LogFrequency = u.LogFrequency;
            LogFileName = u.LogFileName;
            
            LogName = u.LogName;
            LogLevel = u.LogLevel;
            LogJobLevel = u.LogJobLevel;
            LogExp = u.LogExp;
            LogHp = u.LogHp;
            LogMaxHp = u.LogMaxHp;
            LogSp = u.LogSp;
            LogMaxSp = u.LogMaxSp;
            LogWeight = u.LogWeight;
            LogMaxWeight = u.LogMaxWeight;
            LogMap = u.LogMap;
            LogStatuses = u.LogStatuses;
            _suppressUpdates = false;
        });
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        
        // Exclude properties that shouldn't trigger an immediate config update
        if (e.PropertyName == nameof(LogFileName)) return;

        SendConfigUpdate();
    }

    private void SendConfigUpdate()
    {
        if (_suppressUpdates) return;
        
        _worker.Send(new UpdateStatusLoggerConfigCommand(
            LogToFile: LogToFile,
            LogFrequency: LogFrequency,
            LogName: LogName,
            LogLevel: LogLevel,
            LogJobLevel: LogJobLevel,
            LogExp: LogExp,
            LogHp: LogHp,
            LogMaxHp: LogMaxHp,
            LogSp: LogSp,
            LogMaxSp: LogMaxSp,
            LogWeight: LogWeight,
            LogMaxWeight: LogMaxWeight,
            LogMap: LogMap,
            LogStatuses: LogStatuses
        ));
    }

    /// <summary>
    /// Builds the header list using LanguageService. Must be called on the UI thread.
    /// </summary>
    private static List<string> BuildHeaders()
    {
        return new List<string> 
        { 
            LanguageService.Get("S.StatusLogger.Time") ?? "Time", 
            LanguageService.Get("S.StatusLogger.Name") ?? "Character Name", 
            LanguageService.Get("S.StatusLogger.Level") ?? "Level", 
            LanguageService.Get("S.StatusLogger.JobLevel") ?? "Job Level", 
            LanguageService.Get("S.StatusLogger.Exp") ?? "Exp", 
            LanguageService.Get("S.StatusLogger.HP") ?? "HP", 
            LanguageService.Get("S.StatusLogger.MaxHP") ?? "Max HP", 
            LanguageService.Get("S.StatusLogger.SP") ?? "SP", 
            LanguageService.Get("S.StatusLogger.MaxSP") ?? "Max SP", 
            LanguageService.Get("S.StatusLogger.Weight") ?? "Weight", 
            LanguageService.Get("S.StatusLogger.MaxWeight") ?? "Max Weight", 
            LanguageService.Get("S.StatusLogger.Map") ?? "Map", 
            LanguageService.Get("S.StatusLogger.Statuses") ?? "Statuses" 
        };
    }

    private void OnCharacterStateReceived(CharacterUpdate u)
    {
        if (_isApplicationOn && LogToFile && !string.IsNullOrWhiteSpace(LogFileName))
        {
            if ((DateTime.Now - _lastLogTime).TotalSeconds >= LogFrequency)
            {
                _lastLogTime = DateTime.Now;

                // Read the pre-cached headers (safe from any thread)
                var headers = _cachedHeaders;
                uint currentHp = _currentHp;
                uint maxHp = _maxHp;
                uint currentSp = _currentSp;
                uint maxSp = _maxSp;

                _ = System.Threading.Tasks.Task.Run(() => LogExpToDisk(u, headers, currentHp, maxHp, currentSp, maxSp));
            }
        }
    }

    private void LogExpToDisk(CharacterUpdate u, List<string> headers, uint currentHp, uint maxHp, uint currentSp, uint maxSp)
    {
        try
        {
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogFileName);
            bool writeHeader = !System.IO.File.Exists(path) || new System.IO.FileInfo(path).Length == 0;
            using var sw = new System.IO.StreamWriter(path, append: true);

            if (writeHeader)
            {
                sw.WriteLine(string.Join(",", headers));
            }

            var row = new List<string> { $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}" };
            row.Add(LogName ? u.Name : "");
            row.Add(LogLevel ? u.Level.ToString() : "");
            row.Add(LogJobLevel ? u.JobLevel.ToString() : "");
            row.Add(LogExp ? u.Exp.ToString() : "");
            row.Add(LogHp ? currentHp.ToString() : "");
            row.Add(LogMaxHp ? maxHp.ToString() : "");
            row.Add(LogSp ? currentSp.ToString() : "");
            row.Add(LogMaxSp ? maxSp.ToString() : "");
            row.Add(LogWeight ? u.WeightCur.ToString() : "");
            row.Add(LogMaxWeight ? u.WeightMax.ToString() : "");
            row.Add(LogMap ? u.Map : "");
            
            if (LogStatuses) 
            {
                var sortedStatuses = string.Join(" ", u.ActiveStatuses.Split(' ', StringSplitOptions.RemoveEmptyEntries).OrderBy(s => s));
                row.Add(sortedStatuses);
            }
            else
            {
                row.Add("");
            }

            sw.WriteLine(string.Join(",", row));
        }
        catch
        {
            // Ignore I/O errors
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void OpenLogFile()
    {
        try
        {
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogFileName);
            if (!System.IO.File.Exists(path))
            {
                var headers = _cachedHeaders;
                System.IO.File.WriteAllText(path, string.Join(",", headers) + "\r\n");
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore if no associated app
        }
    }
}
