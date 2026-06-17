using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ORTools.UI.ViewModels;

namespace ORTools.UI.Views;

public static class LogViewHelper
{
    // ── AutoScroll Attached Property ──────────────────────────────────────────

    public static readonly DependencyProperty AutoScrollProperty =
        DependencyProperty.RegisterAttached("AutoScroll", typeof(bool), typeof(LogViewHelper), new PropertyMetadata(false, AutoScrollPropertyChanged));

    public static void AutoScrollPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        if (obj is ScrollViewer scrollViewer)
        {
            if ((bool)args.NewValue)
                scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            else
                scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
        }
    }

    private static void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeightChange != 0 && sender is ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollToBottom();
        }
    }

    public static bool GetAutoScroll(DependencyObject obj) => (bool)obj.GetValue(AutoScrollProperty);
    public static void SetAutoScroll(DependencyObject obj, bool value) => obj.SetValue(AutoScrollProperty, value);

    // ── LogMessages Attached Property ──────────────────────────────────────────

    public static readonly DependencyProperty LogMessagesProperty =
        DependencyProperty.RegisterAttached(
            "LogMessages",
            typeof(ObservableCollection<LogMessageItem>),
            typeof(LogViewHelper),
            new PropertyMetadata(null, OnLogMessagesChanged));

    public static ObservableCollection<LogMessageItem> GetLogMessages(DependencyObject obj) =>
        (ObservableCollection<LogMessageItem>)obj.GetValue(LogMessagesProperty);

    public static void SetLogMessages(DependencyObject obj, ObservableCollection<LogMessageItem> value) =>
        obj.SetValue(LogMessagesProperty, value);

    private static void OnLogMessagesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RichTextBox rtb)
        {
            if (e.OldValue is ObservableCollection<LogMessageItem> oldCol)
            {
                oldCol.CollectionChanged -= (s, args) => CollectionChanged(rtb, args);
            }

            if (e.NewValue is ObservableCollection<LogMessageItem> newCol)
            {
                rtb.Document.Blocks.Clear();
                foreach (var item in newCol)
                {
                    AddMessageToRtb(rtb, item);
                }

                newCol.CollectionChanged += (s, args) => CollectionChanged(rtb, args);
            }
        }
    }

    private static void CollectionChanged(RichTextBox rtb, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (LogMessageItem item in e.NewItems)
            {
                AddMessageToRtb(rtb, item);
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            rtb.Document.Blocks.Clear();
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            for (int i = 0; i < e.OldItems.Count; i++)
            {
                if (rtb.Document.Blocks.Count > 0)
                {
                    rtb.Document.Blocks.Remove(rtb.Document.Blocks.FirstBlock);
                }
            }
        }
    }

    private static void AddMessageToRtb(RichTextBox rtb, LogMessageItem item)
    {
        var p = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
        foreach (var seg in item.Segments)
        {
            p.Inlines.Add(new Run(seg.Text) { Foreground = seg.Foreground });
        }
        rtb.Document.Blocks.Add(p);

        if (GetAutoScroll(rtb))
        {
            rtb.ScrollToEnd();
        }
    }
}
