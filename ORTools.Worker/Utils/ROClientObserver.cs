namespace ORTools.Worker;

public interface IObserver { void Update(ISubject subject); }

public interface ISubject
{
    Message Message { get; }
    void Attach(IObserver o);
    void Detach(IObserver o);
    void Notify(Message m);
}

public enum MessageCode
{
    PROCESS_CHANGED, PROFILE_CHANGED, PROFILE_INPUT_CHANGE,
    TURN_ON, TURN_OFF, SHUTDOWN_APPLICATION, CLICK_ICON_TRAY,
    SERVER_LIST_CHANGED, ADDED_NEW_AUTOBUFF_SKILL,
    CHANGED_AUTOSWITCH_SKILL, ADDED_NEW_AUTOSWITCH_PETS,
    DEBUG_MODE_CHANGED
}

public class Message
{
    public MessageCode Code { get; }
    public object? Data    { get; set; }
    public Message() { }
    public Message(MessageCode code, object? data) { Code = code; Data = data; }
}

public class Subject : ISubject
{
    public Message Message { get; private set; } = new();
    private readonly List<IObserver> _obs = new();
    private readonly object _lock = new();

    public void Attach(IObserver o) { lock (_lock) { if (!_obs.Contains(o)) _obs.Add(o); } }
    public void Detach(IObserver o) { lock (_lock) { _obs.Remove(o); } }

    public void Notify(Message m)
    {
        Message = m;
        IObserver[] snap;
        lock (_lock) snap = _obs.ToArray();
        foreach (var o in snap) o.Update(this);
    }
}
