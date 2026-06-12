using System.Windows.Controls;
using System.Windows.Input;
using ORTools.UI.Helpers;
using ORTools.UI.ViewModels;

namespace ORTools.UI.Views;

public partial class AtkDefView : UserControl
{
    public AtkDefView()
    {
        InitializeComponent();
    }

    private void SpammerKey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AtkDefRowViewModel vm)
        {
            InputHelper.HandleKeyInput(tb, e, newKey => vm.SpammerKey = newKey, vm);
        }
    }

    private void SpammerDelay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AtkDefRowViewModel vm)
        {
            InputHelper.HandleNumericUpDown(tb, e, 1, 10000, 10, val => vm.SpammerDelay = val);
        }
    }

    private void SwitchDelay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AtkDefRowViewModel vm)
        {
            InputHelper.HandleNumericUpDown(tb, e, 1, 10000, 10, val => vm.SwitchDelay = val);
        }
    }

    private void DefHead_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AtkDefRowViewModel vm)
            InputHelper.HandleKeyInput(tb, e, newKey => vm.DefHead = newKey, vm);
    }
    
    private void DefBody_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AtkDefRowViewModel vm)
            InputHelper.HandleKeyInput(tb, e, newKey => vm.DefBody = newKey, vm);
    }
    
    private void DefWeapon_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AtkDefRowViewModel vm)
            InputHelper.HandleKeyInput(tb, e, newKey => vm.DefWeapon = newKey, vm);
    }
    
    private void DefShield_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AtkDefRowViewModel vm)
            InputHelper.HandleKeyInput(tb, e, newKey => vm.DefShield = newKey, vm);
    }
    
    private void DefGarment_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AtkDefRowViewModel vm)
            InputHelper.HandleKeyInput(tb, e, newKey => vm.DefGarment = newKey, vm);
    }
    
    private void DefShoes_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AtkDefRowViewModel vm)
            InputHelper.HandleKeyInput(tb, e, newKey => vm.DefShoes = newKey, vm);
    }
    
    private void AtkHead_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AtkDefRowViewModel vm)
            InputHelper.HandleKeyInput(tb, e, newKey => vm.AtkHead = newKey, vm);
    }
    
    private void AtkBody_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AtkDefRowViewModel vm)
            InputHelper.HandleKeyInput(tb, e, newKey => vm.AtkBody = newKey, vm);
    }
    
    private void AtkWeapon_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AtkDefRowViewModel vm)
            InputHelper.HandleKeyInput(tb, e, newKey => vm.AtkWeapon = newKey, vm);
    }
    
    private void AtkShield_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AtkDefRowViewModel vm)
            InputHelper.HandleKeyInput(tb, e, newKey => vm.AtkShield = newKey, vm);
    }
    
    private void AtkGarment_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AtkDefRowViewModel vm)
            InputHelper.HandleKeyInput(tb, e, newKey => vm.AtkGarment = newKey, vm);
    }
    
    private void AtkShoes_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AtkDefRowViewModel vm)
            InputHelper.HandleKeyInput(tb, e, newKey => vm.AtkShoes = newKey, vm);
    }
}
