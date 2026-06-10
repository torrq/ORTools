using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ORTools.UI.ViewModels;

namespace ORTools.UI.Views;

public partial class AutopotSPView : UserControl
{
    private Point _dragStartPoint;

    public AutopotSPView()
    {
        InitializeComponent();
    }

    private static bool IsIgnoredKey(Key key) =>
        key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt
            or Key.RightAlt or Key.LWin or Key.RWin or Key.Tab or Key.CapsLock or Key.NumLock
            or Key.Scroll or Key.System;

    private void Row_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void Row_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (sender is not Border border || border.DataContext is not AutopotSPSlotViewModel slot)
            return;

        DragDrop.DoDragDrop(border, slot.Id, DragDropEffects.Move);
    }

    private void Row_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(int)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void Row_Drop(object sender, DragEventArgs e)
    {
        if (sender is not Border targetBorder || targetBorder.DataContext is not AutopotSPSlotViewModel targetSlot)
            return;

        if (!e.Data.GetDataPresent(typeof(int)) || DataContext is not AutopotSPViewModel viewModel)
            return;

        if (e.Data.GetData(typeof(int)) is not int sourceId || sourceId == targetSlot.Id)
            return;

        MoveSlot(viewModel.Slots, sourceId, targetSlot.Id);
        viewModel.RequestSlotOrder(viewModel.Slots.Select(s => s.Id));
        e.Handled = true;
    }

    private static void MoveSlot(ObservableCollection<AutopotSPSlotViewModel> slots, int sourceId, int targetId)
    {
        var sourceIndex = slots.ToList().FindIndex(slot => slot.Id == sourceId);
        var targetIndex = slots.ToList().FindIndex(slot => slot.Id == targetId);
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
            return;

        slots.Move(sourceIndex, targetIndex);
    }

    private void KeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not AutopotSPSlotViewModel slot)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            slot.KeyText = "None";
            e.Handled = true;
            return;
        }

        if (IsIgnoredKey(key))
        {
            e.Handled = true;
            return;
        }

        slot.KeyText = key.ToString();
        e.Handled = true;
    }

    private void KeyBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
            textBox.BorderBrush = (System.Windows.Media.Brush)FindResource("AppSuccessBrush");
    }

    private void KeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
            textBox.BorderBrush = (System.Windows.Media.Brush)FindResource("AppInputBorderBrush");
    }

    private void NumericTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.OemPeriod or Key.OemComma)
            e.Handled = true;
    }

    private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(ch => !char.IsDigit(ch));
    }

    private void NumericTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        if (!int.TryParse(textBox.Text, out var value))
            return;

        if (textBox.DataContext is AutopotSPSlotViewModel slot)
            slot.Percent = value;
        else if (ReferenceEquals(textBox, DelayBox) && DataContext is AutopotSPViewModel viewModel)
            viewModel.Delay = value;
    }

    private void DelayBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && DataContext is AutopotSPViewModel viewModel
            && !int.TryParse(textBox.Text, out _))
        {
            textBox.Text = viewModel.Delay.ToString();
        }
    }

    private void PercentBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is AutopotSPSlotViewModel slot
            && !int.TryParse(textBox.Text, out _))
        {
            textBox.Text = slot.Percent.ToString();
        }
    }
}
