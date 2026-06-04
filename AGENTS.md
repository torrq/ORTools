# AGENTS.md — ORTools Modernization

This document gives an AI agent full context on what ORTools is, the current
state of the codebase, and the plan for the rewrite. Read it in full before
touching any code.

---

## What is ORTools?

ORTools (also called OSRO Tools or 4RTools) is a Windows automation assistant
for the private Ragnarok Online server OSRO (MR variant). It runs alongside
the game client and automates repetitive tasks that the game does not handle:

- **Autopot HP/SP** — uses a potion hotkey when HP or SP drops below a threshold
- **Skill Timer** — fires up to 10 skill hotkeys on configurable intervals
- **Skill Spammer** — rapid-fires skills while a key is held or toggled
- **Debuff recovery (Status Recovery)** — uses a cure item when a debuff is detected
- **Autobuff (skills + items)** — recasts buff skills/items on a timer
- **Songs** — re-applies bard/dancer song buffs
- **Auto Off** — stops the tool on a condition (HP threshold, death, etc.)
- **ATK/DEF display** — reads and shows raw ATK/DEF from memory
- **Macro Switch** — switches profiles or states via hotkey
- **Transfer Helper** — automates zeny/item transfer actions
- **State Switch** — global ON/OFF toggle with a configurable hotkey

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

Attempted mitigations that did not help:
- WM_SETREDRAW suppression on form and MdiClient during drag
- Keyboard hook disable during drag
- Thread priority boost during drag
- Custom SC_MOVE intercept redirecting to DefWindowProc
- Custom WM_NCLBUTTONDOWN drag implementation

The fix is architectural: eliminate the MDI container and the elevated UI
process entirely. That is what this rewrite does.

### Key technical facts about the legacy code

- `requireAdministrator` is non-negotiable: `ReadProcessMemory` /
  `OpenProcess(PROCESS_VM_READ)` require it. Do not remove it from the Worker.
- `timeBeginPeriod(1)` is called at startup so `Thread.Sleep(1)` is accurate.
  Must be paired with `timeEndPeriod(1)` on shutdown.
- `KeyboardHook` (WH_KEYBOARD_LL) is installed at startup unconditionally.
  Its callback runs on the UI thread's message loop.
- `ThreadRunner` threads are STA and start only when `ThreadRunner.Start()` is
  explicitly called. They do NOT start at construction time.
- All model classes (`AutopotHP`, `SkillTimer`, etc.) are safe to move to the
  Worker unchanged — they have no WinForms dependencies.
- Profile and config data is stored in `%AppData%\ORTools\` as JSON files
  serialised with Newtonsoft.Json.
- `HpSpCache` and `StatusBufferCache` are lock-free (`volatile` + struct copy).
  Safe to read from multiple threads.

---

## Architecture: New (this repo, .NET 8)

```
ORTools.sln
├── ORTools.Shared      (.NET 8 class library)
│   └── Protocol/       IPC message types (both sides share this)
│
├── ORTools.Worker      (.NET 8 console, requireAdministrator)
│   ├── WorkerCore      root object, owns all model instances
│   ├── IPC/PipeServer  named pipe server (one client at a time)
│   ├── IPC/CommandDispatcher  routes UI commands to models
│   └── [Phase 2] all model classes from the legacy app
│
└── ORTools.UI          (Avalonia 11 .NET 8, asInvoker — NOT elevated)
    ├── Services/WorkerService    pipe client, reconnection, event dispatch
    ├── Services/WorkerLauncher   ShellExecute runas to start Worker
    ├── ViewModels/               MVVM (CommunityToolkit.Mvvm)
    └── Views/                    AXAML windows and user controls
```

### Why two processes?

The UI runs at **medium integrity** (non-elevated). This means:
- DWM composites it on the normal fast path — no UIPI overhead
- Window dragging is smooth
- The elevated Worker handles all Win32 that needs admin

The two processes communicate over a **named pipe** (`ORTools-Worker`).
The pipe is created with a world-readable ACL so non-elevated UI can connect.

### IPC protocol

Every message is a single newline-terminated JSON line:
```json
{"t":"MessageType","p":{...typed payload...}}
```

`t` is the type discriminator (see `MessageTypes.cs`).
`p` is the typed payload (see `Commands.cs` and `Updates.cs`).

**Commands** flow UI → Worker.
**Updates** flow Worker → UI (pushed on state change, no polling).

On connect, the UI sends `RequestFullState`. The Worker responds with
`AppState`, `ClientState`, `ProfileList`, and `ProcessList` so the UI is
fully in sync after any reconnect.

### Pipe lifecycle

1. UI starts, `WorkerService.StartAsync` begins trying to connect.
2. After 2 failed attempts, `WorkerLauncher.TryLaunch` starts the Worker
   with `ShellExecute("runas")`. Windows shows a UAC prompt once.
3. Worker starts, creates the pipe, sends `WorkerReady`.
4. UI connects, sends `RequestFullState`, begins receiving updates.
5. If the pipe breaks, `WorkerService` reconnects automatically (2s retry).
6. Worker stays alive across UI restarts; UI re-syncs on reconnect.

---

## Rewrite phases

### Phase 1 — IPC skeleton ✅ (this PR)

What exists:
- `ORTools.Shared`: all protocol types
- `ORTools.Worker`: pipe server + stub dispatcher (no real models yet)
- `ORTools.UI`: Avalonia shell, WorkerService, MainWindowViewModel,
  MainWindow with all tabs as stubs, connection/process/profile bar

What to verify:
1. `dotnet build ORTools.sln` passes with zero errors
2. Launch Worker manually (it will print `Waiting for UI connection...`)
3. Launch UI — it should show "Connected" indicator within 2 seconds
4. Worker console should print `← RequestFullState` then `UI disconnected`
   if you close the UI

### Phase 2 — Port model classes to Worker

Move these files from the legacy project into `ORTools.Worker/Model/`:
- `Client.cs`, `ProcessMemoryReader.cs`
- `AutopotHP.cs`, `AutopotSP.cs`
- `SkillTimer.cs`, `SkillTimerKey.cs`
- `SkillSpammer.cs`
- `StatusRecovery.cs`
- `AutobuffSkill.cs`, `AutobuffItem.cs`
- `Songs.cs`, `ATKDEFReader.cs`
- `AutoOff.cs`, `MacroSwitch.cs`, `TransferHelper.cs`
- `ThreadRunner.cs`, `KeyboardHook.cs`, `Win32Interop.cs`, `Constants.cs`
- `ProfileSingleton.cs`, `ConfigGlobal.cs`, `AppConfig.cs`
- `Subject.cs`, `IObserver.cs`, `Message.cs`, `MessageCode.cs`
- `Server.cs`, `PotionManager.cs`, `HpSpCache.cs`, `StatusBufferCache.cs`
- `JobList.cs`, `EffectStatusIDs.cs`, `SPECIAL_ITEMS.cs`

Wire each model's events/outputs to `WorkerCore.BroadcastAsync` so state
changes are pushed to the UI automatically.

Replace `CommandDispatcher` stub handlers with real model calls.

Add a state-push thread in WorkerCore that calls `ReadHpSp()` every 50ms
and broadcasts `HpSpUpdate`, and calls `ReadJobBlock()` + map every 1s and
broadcasts `CharacterUpdate`.

### Phase 3 — Implement each tab view

One tab at a time, replace the `"Phase 2"` placeholder with a real
AXAML view + ViewModel. Suggested order (easiest → most complex):

1. **StateSwitchView** — toggle button + hotkey input (already in header,
   may not need its own tab)
2. **AutopotHPView** — 5-row grid: key, HP%, enabled checkbox per row
3. **AutopotSPView** — same as HP
4. **StatusRecoveryView** — 3 cure-item lists (Panacea, Royal Jelly, Green Pot)
5. **SkillSpammerView** — list of skill entries + delay + toggle mode
6. **AutobuffSkillView** / **AutobuffItemView** — list of buff slots + interval
7. **SkillTimerView** — 10 lanes, most complex layout (key, delay, click mode,
   alt key, enabled per lane)
8. **SongsView**
9. **DebuffsView**
10. **AutoOffView**
11. **TransferHelperView**
12. **ATKDEFView**
13. **MacroSwitchView**
14. **SettingsView**
15. **ProfilesView**

### Phase 4 — Polish

- System tray icon (`TrayIcon` is built into Avalonia 11)
- Debug log window (forward `LogMessageUpdate` to a scrolling text panel)
- Theme toggle (Avalonia `RequestedThemeVariant`)
- Minimize to tray on close
- Auto-launch Worker on Windows startup (optional, via registry Run key)
- Installer / release packaging

---

## Conventions for this codebase

### General
- Target: .NET 8, C# 12, nullable enabled, implicit usings enabled
- No `Application.DoEvents()`, no `Thread.Sleep` on the UI thread
- All UI updates via `Dispatcher.UIThread.Post(action)`
- ViewModels use `[ObservableProperty]` and `[RelayCommand]` from
  CommunityToolkit.Mvvm — do not hand-write `INotifyPropertyChanged`

### IPC
- Never add a new message type without adding it to **both** `MessageTypes.cs`
  (the string constant) and either `Commands.cs` or `Updates.cs` (the record)
- The envelope type discriminator `"t"` must exactly match a `MessageTypes`
  constant — casing matters
- Keep messages small. Do not put large arrays in updates; paginate if needed

### Worker
- All model instances live on `WorkerCore` as public properties
- `CommandDispatcher` methods are the only place models are called from IPC
- Models must never call `BeginInvoke` or touch any WinForms/Avalonia type
- `ThreadRunner` threads must not be started until a client is connected
  (guard with `ClientSingleton.GetClient() != null`)

### Avalonia UI
- Use compiled bindings (`x:DataType`) on every view
- ViewModels must not import Avalonia namespaces except `Avalonia.Media` for
  brush types and `Avalonia.Threading.Dispatcher` for marshalling
- One ViewModel per view; no code-behind logic beyond `InitializeComponent()`
- Colors: use the Catppuccin Mocha palette already in MainWindow.axaml
  (`#1E1E2E` base, `#252535` surface, `#E0E0FF` text, `#5865F2` accent)

---

## File reference

| File | Purpose |
|------|---------|
| `ORTools.Shared/Protocol/IpcEnvelope.cs` | Wire format, Wrap/Parse helpers |
| `ORTools.Shared/Protocol/MessageTypes.cs` | String constants for all message types |
| `ORTools.Shared/Protocol/Commands.cs` | All UI→Worker command records |
| `ORTools.Shared/Protocol/Updates.cs` | All Worker→UI update records |
| `ORTools.Worker/WorkerCore.cs` | Root object, owns models + broadcasts |
| `ORTools.Worker/IPC/PipeServer.cs` | Named pipe server, one client at a time |
| `ORTools.Worker/IPC/CommandDispatcher.cs` | Routes commands to model handlers |
| `ORTools.UI/Services/WorkerService.cs` | Pipe client, reconnection, event dispatch |
| `ORTools.UI/Services/WorkerLauncher.cs` | ShellExecute runas to start Worker |
| `ORTools.UI/ViewModels/MainWindowViewModel.cs` | All observable state for main window |
| `ORTools.UI/Views/MainWindow.axaml` | Main window layout |

---

## Phase 1 bringup — bugs found and fixed

These were hit during initial bring-up. Do not reintroduce them.

### 1. `MainWindow.axaml.cs` must call `InitializeComponent()`
Without it Avalonia never loads the AXAML. The window renders as a blank black
frame titled "Window". Always include a constructor that calls
`InitializeComponent()` in every code-behind file:

```csharp
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
}
```

### 2. AXAML color strings must be exactly 3, 4, 6, or 8 hex digits
`#XXXXXXX` (7 digits) is invalid and causes the AXAML parser to throw at load
time, producing the same blank window symptom as above. Always use `#RRGGBB`
(6) or `#AARRGGBB` (8). Watch for off-by-one typos: `#6060808` → `#606080`,
`#5555777` → `#555577`.

### 3. `System.IO.Pipes.AccessControl` NuGet package not needed on `net8.0-windows`
Produces a `CS0234` build error on .NET 8 Windows targets. `NamedPipeServerStream`
works directly for same-user elevated↔medium-integrity pipe communication — the
current user's DACL covers it. If an explicit world-readable ACL is ever needed
for production packaging, revisit with `NamedPipeServerStreamAcl`.

### 4. The ProfileList/CurrentProfile feedback loop
**Symptom:** Worker spams `[Dispatcher] ← SwitchProfile` immediately on UI connect.

**Cause:** Setting `ProfileList` (a new list object) causes Avalonia's ComboBox to
reset `SelectedItem`, which flows back through the `CurrentProfile` binding and
fires `OnCurrentProfileChanged` *before* `_suppressProfileCommand = true` is set.
This sends a `SwitchProfile` to the Worker, which replies with `ProfileListUpdate`,
which loops forever.

**Fix:** set `_suppressProfileCommand = true` *before* changing `ProfileList`:

```csharp
_suppressProfileCommand = true;   // must come first
ProfileList    = u.Profiles;
CurrentProfile = u.CurrentProfile;
_suppressProfileCommand = false;
```

**General rule:** any ViewModel property that is both bound to a selection control
*and* sends a command on change needs the suppress flag set before the owning
collection changes, not just before the selection property changes.

### 5. Worker console disappears on launch
`ConsoleHelper.Hide()` was hiding the console even in Debug builds in some
environments. Removed from `Program.cs` entirely. Add it back only in Phase 4
release packaging, and test the `#if !DEBUG` guard explicitly before shipping.
