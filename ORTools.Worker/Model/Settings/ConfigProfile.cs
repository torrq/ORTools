namespace ORTools.Worker;

public class ConfigProfile : IAction
{
    public enum OverweightAutoOffMode { Weight50 = 50, Weight90 = 90 }

    private const string ACTION_NAME = "UserPreferences";

    public decimal              ConfigVersion          { get; set; }
    public string               ToggleStateKey         { get; set; } = ConfigGlobal.GetConfig().DefaultToggleStateKey;
    public bool                 StartAutoOffTimerOnEnable { get; set; } = false;
    public bool                 ClearAutoOffTimerOnDisable { get; set; } = false;
    public List<EffectStatusIDs> AutoBuffOrder         { get; set; } = new();
    public bool                 StopBuffsCity          { get; set; } = false;
    public bool                 SoundEnabled           { get; set; } = false;
    public bool                 AutoOffOverweight      { get; set; } = false;
    public OverweightAutoOffMode AutoOffOverweightMode { get; set; } = OverweightAutoOffMode.Weight90;
    public Keys                 AutoOffKey1            { get; set; }
    public Keys                 AutoOffKey2            { get; set; }
    public bool                 AutoOffKillClient      { get; set; } = false;
    public bool                 SwitchAmmo             { get; set; } = false;
    public Keys                 Ammo1Key               { get; set; }
    public Keys                 Ammo2Key               { get; set; }
    public int                  AutoOffTime            { get; set; } = 1;
    public bool                 KeepDeadClientInfo         { get; set; } = false;

    public void   Start() { }
    public void   Stop()  { }
    public string GetConfiguration()  => JsonConvert.SerializeObject(this);
    public string GetActionName()     => ACTION_NAME;

    public void SetAutoBuffOrder(List<EffectStatusIDs> buffs) => AutoBuffOrder = buffs;
}
