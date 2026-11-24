using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ORTools.UI.Views;

public partial class ProfilesView : UserControl
{
    public ProfilesView()
    {
        InitializeComponent();
    }

    private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item && item.DataContext is ViewModels.ProfileItemViewModel profile)
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow?.DataContext is ViewModels.MainWindowViewModel mainVm)
            {
                mainVm.CurrentProfile = profile.Name;
            }
        }
    }

    private void ProfileHotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        Key realKey = e.Key == Key.System ? e.SystemKey : e.Key;
        if (realKey == Key.Tab) return;
        e.Handled = true;

        // Ignore standalone modifier presses
        if (realKey is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        if (DataContext is not ViewModels.ProfilesViewModel vm) return;

        if (realKey is Key.Back or Key.Escape)
        {
            vm.UpdateSelectedProfileHotkey("None");
            Keyboard.ClearFocus();
            return;
        }

        string keyName = realKey switch
        {
            Key.PageUp => "Prior",
            Key.PageDown => "Next",
            Key.Enter or Key.Return => "Return",
            Key.OemTilde => "Oemtilde",
            Key.OemPlus => "Oemplus",
            Key.OemMinus => "OemMinus",
            Key.OemPeriod => "OemPeriod",
            Key.OemComma => "Oemcomma",
            Key.OemOpenBrackets => "OemOpenBrackets",
            Key.OemCloseBrackets => "OemCloseBrackets",
            Key.OemQuotes => "OemQuotes",
            Key.OemSemicolon => "OemSemicolon",
            Key.OemQuestion => "OemQuestion",
            Key.OemPipe => "OemPipe",
            Key.OemBackslash => "OemBackslash",
            Key.OemClear => "OemClear",
            _ => realKey.ToString()
        };

        var mods = Keyboard.Modifiers;
        string prefix = "";
        if ((mods & ModifierKeys.Control) != 0) prefix += "Ctrl+";
        if ((mods & ModifierKeys.Alt) != 0) prefix += "Alt+";
        if ((mods & ModifierKeys.Shift) != 0) prefix += "Shift+";

        vm.UpdateSelectedProfileHotkey(prefix + keyName);
        Keyboard.ClearFocus();
    }

    private void ProfilesView_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files != null && Array.Exists(files, f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                return;
            }
        }
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void ProfilesView_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not ViewModels.ProfilesViewModel vm) return;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        if (files == null) return;

        var validFiles = files
            .Where(file => File.Exists(file) && file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (validFiles.Count > 0)
        {
            vm.ImportProfilesFromPaths(validFiles);
        }
    }
}
