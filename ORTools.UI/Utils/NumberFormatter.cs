namespace ORTools.UI.Utils;

public static class NumberFormatter
{
    public static string FormatExp(double exp)
    {
        if (exp < 0) return "-" + FormatExp(-exp);
        if (exp >= 1_000_000_000) return (exp / 1_000_000_000D).ToString("#,##0.##") + "B";
        if (exp >= 1_000_000) return (exp / 1_000_000D).ToString("#,##0.##") + "M";
        if (exp >= 1_000) return (exp / 1_000D).ToString("#,##0.##") + "K";
        return exp.ToString("#,##0");
    }
}
