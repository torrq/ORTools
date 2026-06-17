using System.Windows;
using System.Windows.Input;
using ORTools.UI.ViewModels;

namespace ORTools.UI.Views;

public partial class MainWindow : Window
{
    private double _expandedHeight = 675;
    private double _expandedWidth = 731;
    private double _currentDebugHeight = 0;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += MainWindow_DataContextChanged;
    }

    private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainWindowViewModel oldVm)
        {
            oldVm.PropertyChanged -= Vm_PropertyChanged;
            if (oldVm.Settings != null) oldVm.Settings.PropertyChanged -= Settings_PropertyChanged;
        }
        if (e.NewValue is MainWindowViewModel newVm)
        {
            newVm.PropertyChanged += Vm_PropertyChanged;
            if (newVm.Settings != null) newVm.Settings.PropertyChanged += Settings_PropertyChanged;
        }
    }

    private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (e.PropertyName == nameof(SettingsViewModel.DebugView))
            {
                if (vm.Settings.DebugView)
                {
                    _currentDebugHeight = vm.Settings.DebugViewHeight;
                    this.Height += _currentDebugHeight;
                    _expandedHeight += _currentDebugHeight;
                }
                else
                {
                    this.Height -= _currentDebugHeight;
                    _expandedHeight -= _currentDebugHeight;
                    _currentDebugHeight = 0;
                }
            }
            else if (e.PropertyName == nameof(SettingsViewModel.DebugViewHeight))
            {
                if (vm.Settings.DebugView)
                {
                    double delta = vm.Settings.DebugViewHeight - _currentDebugHeight;
                    this.Height += delta;
                    _expandedHeight += delta;
                    _currentDebugHeight = vm.Settings.DebugViewHeight;
                }
            }
        }
    }

    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsMiniMode))
        {
            if (DataContext is MainWindowViewModel vm)
            {
                if (vm.IsMiniMode)
                {
                    _expandedHeight = this.Height;
                    _expandedWidth = this.Width;
                    this.MinHeight = 0;
                    this.MinWidth = 0;
                    this.Width = 584;
                    this.SizeToContent = SizeToContent.Height;
                }
                else
                {
                    this.SizeToContent = SizeToContent.Manual;
                    this.Height = _expandedHeight;
                    this.Width = _expandedWidth;
                    this.MinHeight = 500;
                    this.MinWidth = 600;
                }
            }
        }
    }

    private void ToggleKeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && sender is System.Windows.Controls.TextBox tb)
        {
            ORTools.UI.Helpers.InputHelper.HandleKeyInput(tb, e, newKey => vm.UpdateToggleKeyCommand.Execute(newKey), "ToggleKeyTextBox");
        }
    }

    private void DragRegion_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            this.DragMove();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized && DataContext is MainWindowViewModel vm)
        {
            if (vm.Settings.MinimizeToSystray && !vm.Settings.DisableSystray)
            {
                this.Hide();
            }
        }
        base.OnStateChanged(e);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && !vm.ForceExit)
        {
            if (vm.Settings.CloseToSystray && !vm.Settings.DisableSystray)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }
        }
        base.OnClosing(e);
    }

    private void ProcessList_DropDownOpened(object sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.RefreshProcessListCommand.Execute(null);
        }
    }
}
