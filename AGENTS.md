# AGENTS.md — ORTools Modernization

Read this file in full before touching any code.

---

## What is ORTools?

ORTools (also called OSRO Tools or 4RTools) is a Windows automation assistant
for the private Ragnarok Online server OSRO. It runs alongside the game client
and automates tasks the game does not handle:

- **Autopot HP/SP** — uses a potion hotkey when HP or SP drops below a threshold
- **Skill Timer** — fires up to 10 skill hotkeys on configurable intervals
- **Skill Spammer** — rapid-fires skills while a key is held or toggled
- **Status Recovery** — uses a cure item when a debuff is detected
- **Debuff Recovery** — broader debuff monitoring and response
- **Autobuff (skills + items)** — recasts buff skills/items on a timer
- **Songs** — re-applies bard/dancer song buffs
- **Auto Off** — stops the tool on a condition (overweight, death, etc.)
- **ATK/DEF** — reads and displays raw ATK/DEF values from memory
- **Macro Switch** — switches profiles or states via hotkey
- **Transfer Helper** — automates zeny/item transfer actions
- **State Switch** — global ON/OFF toggle with a configurable hotkey

There are two server modes: **MR** (Midrate) and **HR** (Highrate).
`AppConfig.ServerMode` controls which memory addresses and server configs are active.
The current active version is **HR** (`ServerMode = 1`, title `v1.99.0/HR`).

---

## Architecture: Legacy (4RTools-OSRO, .NET 4.8.1 WinForms)

The legacy app is a single elevated process:

```
Container.cs (MDI WinForms window, requireAdministrator)
  ├── 14 MDI child forms (one per feature tab)
  ├── CharacterInfo (embedded non-MDI form, 15ms refresh timer)
  ├── Subject / Observer message bus (home-grown, synchronous)
  ├── ProfileSingleton — active profile, JSON-serialised via Newtonsoft
  ├── ConfigGlobal — global config (disable tray, etc.)
  ├── ClientSingleton — the active RO process handle
  ├── ThreadRunner — background STA thread wrapper with suspend/resume
  ├── KeyboardHook (WH_KEYBOARD_LL, runs on UI thread)
  └── Model classes — one per feature, each owns a ThreadRunner
```

### Why the legacy app drags slowly

Root cause confirmed via WM_MOVING stack traces: `DefFrameProc` (the MDI frame
window procedure) is in every drag-loop stack frame. `IsMdiContainer = true`
causes Windows to route all SC_MOVE syscommands through `DefFrameProc`, which
does extra MDI child coordination on every mouse-move event. Combined with
running elevated (`requireAdministrator`) which routes DWM compositing through
a slower UIPI path, the result is 100–700ms gaps between WM_MOVING messages on
older hardware (i5-4690K / GTX 970).

The fix is architectural: eliminate the MDI container and the elevated UI
process entirely. That is what this rewrite does.

### Key technical facts about the legacy code

- `requireAdministrator` is non-negotiable: `ReadProcessMemory` /
  `OpenProcess(PROCESS_VM_READ)` require it. Do not remove it from the Worker.
- `timeBeginPeriod(1)` is called at startup so `Thread.Sleep(1)` is accurate.
  Must be paired with `timeEndPeriod(1)` on shutdown.
- `ThreadRunner` threads are STA and start only when `ThreadRunner.Start()` is
  explicitly called. They do NOT start at construction time.
- Profile and config data lives in the app's working directory under `Profiles\`
  and `Config\` (not in `%AppData%`).
- `HpSpCache` and `StatusBufferCache` are lock-free (`volatile` + struct copy).
  Safe to read from multiple threads without locking.

---

## Architecture: New (this repo, .NET 8)

```
ORTools.sln
├── ORTools.Shared      (.NET 8 class library)
│   └── Protocol/       IPC message types shared by both processes
│
├── ORTools.Worker      (.NET 8 console, requireAdministrator)
│   ├── WorkerCore.cs          root object; owns all model instances
│   ├── IPC/PipeServer.cs      named pipe server (one UI client at a time)
│   ├── IPC/CommandDispatcher  routes incoming commands to WorkerCore
│   ├── IPC/StatePublisher     pushes HP/SP (50ms) + character (1s) updates
│   ├── Utils/                 KeyboardHook, ThreadRunner, Win32Interop, etc.
│   └── Model/                 all ported model classes (Tabs/, Buffs/, Settings/)
│
└── ORTools.UI          (.NET 8 WPF, asInvoker — NOT elevated)
    ├── Services/WorkerService    pipe client, reconnection, event dispatch
    ├── Services/WorkerLauncher   ShellExecute runas to start Worker
    ├── ViewModels/               MVVM via CommunityToolkit.Mvvm
    └── Views/                    WPF XAML windows and user controls
```

### Why two processes?

The UI runs at **medium integrity** (non-elevated). This means:
- DWM composites it on the normal fast path — no UIPI overhead
- Window dragging is smooth on older hardware
- The elevated Worker handles all Win32 that needs admin

### IPC protocol

Every message is a single newline-terminated JSON line:
```json
{"t":"MessageType","p":{...typed payload...}}
```

`t` is the type discriminator (see `MessageTypes.cs`).
`p` is the typed payload (see `Commands.cs` and `Updates.cs`).

**Commands** flow UI → Worker.
**Updates** flow Worker → UI (pushed on state change, never polled).

On connect, the UI sends `RequestFullState`. The Worker responds with
`AppState`, `ClientState`, `ProfileList`, and `ProcessList`.

### Pipe lifecycle

1. UI starts, `WorkerService.StartAsync` begins trying to connect.
2. After 2 failed attempts, `WorkerLauncher.TryLaunch` starts the Worker
   with `ShellExecute("runas")` — Windows shows a UAC prompt once.
3. Worker starts, creates the named pipe, sends `WorkerReady`.
4. UI connects, sends `RequestFullState`, begins receiving updates.
5. If the pipe breaks, `WorkerService` reconnects automatically (2s retry).
6. Worker stays alive across UI restarts; UI re-syncs on reconnect.

---

## Current status

### Phase 1 — IPC skeleton ✅

- `ORTools.Shared`: all protocol types
- `ORTools.Worker`: pipe server + command dispatcher (wired to real models in Phase 2)
- `ORTools.UI`: WPF shell, WorkerService, MainWindowViewModel,
  MainWindow with tab stubs, connection/process/profile bar

### Phase 2 — Port model classes to Worker ✅ (in progress / needs build fixes)

All legacy model files have been ported into `ORTools.Worker/` with:
- Namespace changed from `_ORTools.*` to `ORTools.Worker`
- `FormHelper.ToggleStateOff(x)` → `WorkerNotifier.RequestTurnOff(x)`
- `FormHelper.IsValidKey(k)` → `WorkerNotifier.IsValidKey(k)`
- `SendKeys.SendWait` → `keybd_event` Alt+key synthesis (no message pump in Worker)
- `System.Windows.Forms.Timer` → `System.Timers.Timer` (AutoOff.cs)
- `Buff.cs` rewritten as data-only (no `Bitmap`, no `IResourceLoader`)
- `BuffService.Initialize()` must be called at Worker startup

`WorkerCore.cs` owns all model instances and wires them up.
`StatePublisher` pushes `HpSpUpdate` every 50ms and `CharacterUpdate` every 1s.

### Phase 3 — Implement each tab view (not started)

One WPF view + ViewModel per feature. Suggested order:

1. **AutopotHP/SP** — 5-row grid: key, percent threshold, enabled checkbox
2. **StatusRecovery** — 3 cure-item lists (Panacea, Royal Jelly, Green Pot)
3. **SkillTimer** — 10 lanes: key, delay, click mode, alt-key, enabled
4. **SkillSpammer** — list of skill entries + delay + toggle mode
5. **Autobuff (skills + items)** — buff slot list + interval
6. **Songs**, **Debuffs**, **AutoOff**, **TransferHelper**
7. **ATK/DEF**, **MacroSwitch**, **Settings**, **Profiles**

Each view needs a corresponding `XxxCommand` / `XxxUpdate` IPC message pair
in `ORTools.Shared/Protocol/` if the Worker needs to receive settings from UI.

### Phase 4 — Polish (not started)

- System tray icon (use `System.Windows.Forms.NotifyIcon` from Worker side,
  or `Hardcodet.NotifyIcon.Wpf` in the UI)
- Debug log panel (forward `LogMessageUpdate` to a scrolling TextBox)
- Minimize to tray on close
- Installer / release packaging
- Hide Worker console window in release builds (re-add `ConsoleHelper.Hide()`
  guarded by `#if !DEBUG`)

---

## Conventions

### General
- Target: .NET 8, C# 12, nullable enabled, implicit usings enabled
- No `Thread.Sleep` or blocking calls on the UI thread
- All UI updates via `Application.Current.Dispatcher.BeginInvoke(action)`
- ViewModels: `[ObservableProperty]` and `[RelayCommand]` from CommunityToolkit.Mvvm

### WPF UI
- One ViewModel per view; code-behind contains only `InitializeComponent()`
- All bindings via `{Binding PropertyName}` — WPF does not use `x:DataType`
  compiled bindings by default (unlike Avalonia)
- Color palette (neutral grey, defined in `App.xaml` as `StaticResource`):
  - Base: `#222222`, Surface: `#2D2D2D`, Text: `#E0E0E0`
  - Primary buttons: `#585858`, hover `#474747`, pressed `#3C3C3C`
  - Danger: `#ED4245`, Success: `#4CAF50`
- Do not hardcode colors in XAML — use `{StaticResource AppXxxBrush}` keys

### IPC
- Never add a message type without adding it to **both** `MessageTypes.cs`
  (string constant) and `Commands.cs` or `Updates.cs` (record type)
- The `"t"` discriminator must exactly match a `MessageTypes` constant — casing matters
- Keep payloads small; do not embed large arrays

### Worker
- All model instances live on `WorkerCore` as fields/properties
- `CommandDispatcher` is the only entry point from IPC into models
- Models must never reference WPF or UI types
- `WorkerNotifier.RequestTurnOff(reason)` is how model threads signal the app
  should stop (replaces `FormHelper.ToggleStateOff`)
- `ThreadRunner` threads must not start until a client is connected
- `BuffService.Initialize()` must be called before any autobuff model starts

### Worker — porting rules for future legacy files
When porting additional legacy `.cs` files:
1. Replace namespace `_ORTools.*` with `ORTools.Worker`
2. Remove all `using _ORTools.*` directives
3. Replace `FormHelper.ToggleStateOff(x)` with `WorkerNotifier.RequestTurnOff(x)`
4. Replace `FormHelper.IsValidKey(k)` with `WorkerNotifier.IsValidKey(k)`
5. Replace `SendKeys.SendWait("%" + key)` with `keybd_event` Alt combo (see SkillTimer.cs)
6. Replace `System.Windows.Forms.Timer` with `System.Timers.Timer`; `Tick` → `Elapsed`
7. Strip any `Bitmap`, `GroupBox`, or other WinForms rendering types
8. `using System.ComponentModel` is in `GlobalUsings.cs` — no need to add per-file

---

## Bugs found during bring-up

### Phase 1

**1. WPF `MainWindow.xaml.cs` must call `InitializeComponent()`**
Without it the window is blank. Always include:
```csharp
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
}
```

**2. `System.IO.Pipes.AccessControl` NuGet not needed on `net8.0-windows`**
Causes `CS0234`. Use plain `NamedPipeServerStream` — same-user
elevated↔medium-integrity pipes work without an explicit world-readable ACL.

**3. The ProfileList/CurrentProfile WPF feedback loop**
Setting `ProfileList` (new collection) causes the WPF ComboBox to reset
`SelectedItem`, which fires `OnCurrentProfileChanged` before the suppress flag
is set, which sends `SwitchProfile` to the Worker, which replies with another
`ProfileListUpdate`, looping forever.

Fix: set `_suppressProfileCommand = true` **before** setting `ProfileList`:
```csharp
_suppressProfileCommand = true;
ProfileList    = u.Profiles;
CurrentProfile = u.CurrentProfile;
_suppressProfileCommand = false;
```

### Phase 2

**4. `[Description]` attribute requires `System.ComponentModel`**
`EffectStatusIDs.cs` uses `[Description("...")]`. Add `using System.ComponentModel`
to `GlobalUsings.cs` — already done.

**5. `Buff.cs` is UI-coupled and must be rewritten for the Worker**
The legacy `Buff.cs` depends on `IResourceLoader`, `Bitmap`, `GroupBox`, and
`FormHelper`. In the Worker, rewrite as data-only: `Buff` has only `Name` and
`EffectStatusID`. All 229 buff entries are preserved; `BuffFactory.CreateBuff`
is replaced with a `B(name, effectStr)` static helper that parses the enum.
`BuffService.Initialize()` replaces the old `IResourceLoader`-based init.

**6. `SendKeys.SendWait` does not work in a console Worker process**
There is no WinForms message pump in the Worker. Replace `SendKeys.SendWait("%"
+ key)` with direct `keybd_event` Alt+key synthesis (see `SkillTimer.SendAltKey`
and `WeightLimitMacro.SendAltKeyCombo`).

**7. `System.Windows.Forms.Timer` requires a message pump**
Replace with `System.Timers.Timer` in `AutoOff.cs`. Change `Tick` → `Elapsed`.
The `Elapsed` event fires on a thread-pool thread, which is fine for the Worker.

---

## File reference

| File | Purpose |
|------|---------|
| `ORTools.Shared/Protocol/IpcEnvelope.cs` | Wire format, Wrap/Parse helpers |
| `ORTools.Shared/Protocol/MessageTypes.cs` | String constants for all message types |
| `ORTools.Shared/Protocol/Commands.cs` | All UI→Worker command records |
| `ORTools.Shared/Protocol/Updates.cs` | All Worker→UI update records |
| `ORTools.Worker/WorkerCore.cs` | Root object; owns models, handles turn on/off |
| `ORTools.Worker/IPC/PipeServer.cs` | Named pipe server, one client at a time |
| `ORTools.Worker/IPC/CommandDispatcher.cs` | Routes IPC commands to WorkerCore |
| `ORTools.Worker/IPC/StatePublisher.cs` | Pushes HP/SP + character updates to UI |
| `ORTools.Worker/Utils/WorkerNotifier.cs` | RequestTurnOff, IsValidKey (replaces FormHelper) |
| `ORTools.Worker/Utils/AppConfig.cs` | All app constants and server addresses |
| `ORTools.Worker/Model/Settings/Profile.cs` | Profile + ProfileSingleton |
| `ORTools.Worker/Model/Settings/Config.cs` | Config class + ConfigGlobal |
| `ORTools.Worker/Model/Buffs/Buff.cs` | Data-only Buff + BuffDefinitions + BuffService |
| `ORTools.Worker/Model/Client.cs` | Client, ClientSingleton, HpSpCache, etc. |
| `ORTools.UI/Services/WorkerService.cs` | Pipe client, reconnection, event dispatch |
| `ORTools.UI/Services/WorkerLauncher.cs` | ShellExecute runas to start Worker |
| `ORTools.UI/ViewModels/MainWindowViewModel.cs` | All observable state for main window |
| `ORTools.UI/Views/MainWindow.xaml` | Main window layout (WPF) |
| `ORTools.UI/App.xaml` | Application resources and color palette |
