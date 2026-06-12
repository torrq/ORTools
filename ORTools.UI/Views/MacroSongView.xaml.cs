using System.Windows.Controls;
using System.Windows.Input;
using ORTools.UI.ViewModels;

namespace ORTools.UI.Views;

public partial class MacroSongView : UserControl
{
    public MacroSongView()
    {
        InitializeComponent();
    }

    private void TriggerKey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var ctx = tb.DataContext as MacroSongRowViewModel;
        ORTools.UI.Helpers.InputHelper.HandleKeyInput(tb, e, key => { if (ctx != null) ctx.TriggerKey = key; }, ctx);
    }

    private void StepKey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var ctx = tb.DataContext as MacroSongStepViewModel;
        ORTools.UI.Helpers.InputHelper.HandleKeyInput(tb, e, key => { if (ctx != null) ctx.Key = key; }, ctx);
    }

    private void AdaptationKey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var ctx = tb.DataContext as MacroSongRowViewModel;
        ORTools.UI.Helpers.InputHelper.HandleKeyInput(tb, e, key => {
            if (ctx != null) {
                ctx.AdaptationKey = key;
                ctx.InstrumentKey = key; // Also set InstrumentKey since they are unified in the UI
            }
        }, ctx);
    }

    private void StepDelay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var ctx = tb.DataContext as MacroSongRowViewModel;
        ORTools.UI.Helpers.InputHelper.HandleNumericUpDown(tb, e, 1, 99999, 50, val => { if (ctx != null) ctx.Delay = val; });
    }
}
