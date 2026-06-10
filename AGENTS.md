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

## Phase 3 — Tab views

### Completed

#### Autopot HP + Autopot SP ✅
- `ORTools.Shared/Protocol/AutopotData.cs` — `AutopotSlotData` record (wire format for one slot)
- `ORTools.Shared/Protocol/MessageTypes.cs` — added `UpdateAutopotHPSlot`, `UpdateAutopotHPSettings`, `UpdateAutopotSPSlot`, `UpdateAutopotSPSettings`, `AutopotHPConfig`, `AutopotSPConfig`
- `ORTools.Shared/Protocol/Commands.cs` — `UpdateAutopotHPSlotCommand`, `UpdateAutopotHPSettingsCommand`, `UpdateAutopotSPSlotCommand`, `UpdateAutopotSPSettingsCommand`
- `ORTools.Shared/Protocol/Updates.cs` — `AutopotHPConfigUpdate`, `AutopotSPConfigUpdate`
- `ORTools.Worker/WorkerCore.cs` — `HandleUpdateAutopotHPSlot/Settings`, `HandleUpdateAutopotSPSlot/Settings`, `BuildAutopotHPConfig`, `BuildAutopotSPConfig`; `HandleFullStateRequest` now includes autopot configs
- `ORTools.Worker/IPC/CommandDispatcher.cs` — routes all four new commands
- `ORTools.UI/Services/WorkerService.cs` — `AutopotHPConfigReceived` and `AutopotSPConfigReceived` events
- `ORTools.UI/ViewModels/AutopotHPViewModel.cs` — `AutopotHPSlotViewModel` (per-slot) + `AutopotHPViewModel` (tab)
- `ORTools.UI/ViewModels/AutopotSPViewModel.cs` — `AutopotSPSlotViewModel` + `AutopotSPViewModel`
- `ORTools.UI/ViewModels/MainWindowViewModel.cs` — exposes `AutopotHP` and `AutopotSP` child VMs; wires `AutopotHPConfigReceived` → `AutopotHP.OnConfigUpdate`
- `ORTools.UI/Views/AutopotHPView.xaml` + `.xaml.cs` — 5-slot grid, delay, Stop on Critical Wound checkbox, key capture, numeric-only percent
- `ORTools.UI/Views/AutopotSPView.xaml` + `.xaml.cs` — same without Stop on Critical Wound
- `ORTools.UI/Views/MainWindow.xaml` — added `xmlns:views`, replaced both Autopot stubs with real `<views:AutopotHPView>` / `<views:AutopotSPView>`

**Key capture pattern (WPF):**
- TextBox is `IsReadOnly="True"` with `Mode=OneWay` binding
- `GotFocus` highlights border with `AppSuccessBrush`; `LostFocus` clears it
- `PreviewKeyDown` sets `slot.KeyText` and `tb.Text` directly; `e.Handled = true` prevents normal input
- Escape resets to `"None"`; modifier-only keys (Shift, Ctrl, Alt, Win, Tab, CapsLock, NumLock) are ignored
- The slot VM's `_syncing` flag prevents the change from echoing back to the Worker during `OnConfigUpdate`

**Suppress-flag pattern:**
- `_syncing = true` wraps all property sets in `OnConfigUpdate`/`SyncFrom`
- `partial void OnXxxChanged` checks `!_syncing` before sending IPC commands
- This prevents config echoes: Worker sends config → UI updates → UI would normally send command back → loop

---

### Still needed for Phase 3

Tabs remaining (in suggested order):

1. **Status Recovery** — 3 cure-item lists (Panacea, Royal Jelly, Green Potion), each with a key picker and a delay. Needs `UpdateStatusRecoveryCommand` / `StatusRecoveryConfigUpdate` IPC pair.

2. **Skill Timer** — 10 lanes, each with: key picker, delay (ms), click mode (None/Current/Center radio), Alt-key toggle, enabled checkbox. Most complex layout in the app. Needs `UpdateSkillTimerLaneCommand` / `SkillTimerConfigUpdate`.

3. **Skill Spammer** — list of named entries, each with: key picker, click active checkbox, indeterminate checkbox. Plus global delay and toggle mode. Needs `UpdateSkillSpammerCommand` / `SkillSpammerConfigUpdate`.

4. **Autobuff Skills** — list of buff slots with key picker and interval. Grouped by class (Archer, Swordman, Mage, etc.) via `BuffService`. Needs `UpdateAutobuffSkillCommand` / `AutobuffSkillConfigUpdate`.

5. **Autobuff Items** — same shape as Autobuff Skills but using item buff lists.

6. **Debuff Recovery** — key picker per debuff type.

7. **Songs** — song macro sequence editor.

8. **Auto Off** — timer config, overweight threshold, macro keys.

9. **Transfer Helper** — key pickers for transfer actions.

10. **ATK/DEF** — equip config lanes.

11. **Macro Switch** — hotkey-triggered profile/state switches.

12. **Settings** — expose `Config` fields (DebugMode, DisableSystray, ClearAutoOffTimerOnDisable, etc.).

13. **Profiles** — list of profiles with create/rename/delete/copy actions.

**For each new tab, the pattern is always:**
1. Add message type constants to `MessageTypes.cs`
2. Add command record(s) to `Commands.cs`
3. Add update record(s) to `Updates.cs`
4. Add handler(s) to `WorkerCore`
5. Route in `CommandDispatcher`
6. Add event(s) to `WorkerService` and dispatch case
7. Add child VM to `MainWindowViewModel`; wire event
8. Create `XxxView.xaml` + `.xaml.cs`
9. Replace stub in `MainWindow.xaml`
10. Add to `HandleFullStateRequest` so config syncs on connect

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

**8. `ClientSingleton` had no `SetClient` method**
The legacy `ClientSingleton` only exposed `Instance(Client)` and `GetClient()`.
`WorkerCore` needs `SetClient(Client? c)` to connect and disconnect cleanly.
Added `SetClient` and made the backing `client` field nullable:
```csharp
private static volatile Client? client;
public static void SetClient(Client? c) { client = c; }
public static Client? GetClient()       { return client; }
```

**9. `Constants` missing `MINIMUM_HP_TO_RECOVER`, `WM_RBUTTONDOWN`, `WM_RBUTTONUP`**
Referenced by `AutobuffSkill.cs`, `AutobuffItem.cs`, and `TransferHelper.cs`.
Add to `Constants.cs`:
```csharp
public const int  MINIMUM_HP_TO_RECOVER = 1;
public const int  WM_RBUTTONDOWN        = 0x0204;
public const int  WM_RBUTTONUP          = 0x0205;
```

**10. `FormHelper.StateSwitchFormInstance` leftover in `AutobuffSkill.cs:254`**
The sed pass missed a multi-line `BeginInvoke` block. Replace the entire
`shouldAutoOff` block with:
```csharp
if (shouldAutoOff)
{
    WorkerNotifier.RequestTurnOff("AutobuffSkill_Overweight");
    WeightLimitMacro.SendOverweightMacro();
}
```

**11. `Process` not found in `KeyboardHook.cs`**
Add `using System.Diagnostics` to `GlobalUsings.cs`.

**12. `MouseHelper` stub was missing `TryClickAtCurrentPosition` and `TryClickAtWindowCenter`**
The stub only had `GetCursorPosition` and `LeftClick`. Replace the entire file
with the full legacy implementation from `Utils/MouseHelper.cs`.

**13. `DebugLogger.IsDebugMode` was `private` — change to `internal`**
`StatusEffectLogger` accesses it from the same assembly.
