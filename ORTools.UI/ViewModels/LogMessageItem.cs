using System;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace ORTools.UI.ViewModels;

public class LogTextSegment
{
    public string Text { get; }
    public SolidColorBrush Foreground { get; }

    public LogTextSegment(string text, SolidColorBrush foreground)
    {
        Text = text;
        Foreground = foreground;
    }
}

public class LogMessageItem
{
    public string Timestamp { get; }
    public string Level { get; }
    public SolidColorBrush LevelBrush { get; }
    public ObservableCollection<LogTextSegment> Segments { get; } = new();

    public LogMessageItem(string timestamp, string level, SolidColorBrush levelBrush)
    {
        Timestamp = timestamp;
        Level = level;
        LevelBrush = levelBrush;
    }
}
