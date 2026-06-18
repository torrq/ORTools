# AGENTS.md — OSRO Tools Modernization

Read this file in full before touching any code.

---

## What is OSRO Tools?

OSRO Tools (internally known as ORTools) is a high-performance automation assistant for Oldschool RO. It automates tasks the game does not handle (Autopot, Skill Timer, Autobuff, Status Recovery, Auto Off, etc.).

There are two server modes: **MR** (Midrate) and **HR** (Highrate).
`Directory.Build.props` at the repository root is the single source of truth for the application version and `ServerMode`.
*Note: The `ServerMode` property in `Directory.Build.props` automatically controls the MSBuild preprocessor directives (`SERVERMODE_HR` / `SERVERMODE_MR`), which conditionally embeds the correct `.exe` icon and sets the runtime `AppConfig.ServerMode` flag in C#.*

---

## Changelog (Legacy to Modern .NET 8)

```text
* Architecture & Framework
  - Upgraded entire codebase from legacy .NET Framework to modern .NET 8
  - Split architecture into distinct Worker Core and UI layers to eliminate UI lockups
  - Replaced the heavy MDI WinForms container with a hardware-accelerated WPF interface
* User Interface (UI)
  - Implemented the MVVM pattern via CommunityToolkit.Mvvm for responsive data binding
  - Separated UI logic completely from data models (e.g., Buff models are now strictly data-only)
  - Consolidated Autobuff Skills and Items into a unified Search interface
  - Introduced a centralized AppColors palette for consistent theming and easy customization
  - Added a rich, colorized Debug Log view panel for monitoring backend worker events
* Core Features
  - Rebuilt unified Key Input handling with global duplicate interception across all tabs
  - Refactored auto-off conditions, particularly overweight handling and timer integrations
  - Standardized all UI inputs with consistent styling
```

---

## Architecture: New (this repo, .NET 8)

```text
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
└── ORTools.UI          (.NET 8 WPF, requireAdministrator)
    ├── Services/WorkerService    pipe client, reconnection, event dispatch
    ├── Services/WorkerLauncher   ShellExecute runas to start Worker
    ├── ViewModels/               MVVM via CommunityToolkit.Mvvm
    └── Views/                    WPF XAML windows and user controls
```

### Why two processes?

The legacy app used a single elevated process with an MDI WinForms container, which caused severe lag on older hardware when dragging the window. The new architecture splits the app into two processes, because separating the WPF rendering thread from the heavy Win32 hooks and memory reading improves performance. This means:
- DWM composites it smoothly.
- Window dragging is smooth on older hardware.
- The elevated Worker handles all Win32 `ReadProcessMemory` / `OpenProcess` calls that need admin.

### IPC Protocol

Every message is a single newline-terminated JSON line:
```json
{"t":"MessageType","p":{...typed payload...}}
```

- `t` is the type discriminator (see `MessageTypes.cs`).
- `p` is the typed payload (see `Commands.cs` and `Updates.cs`).
- **Commands** flow UI → Worker.
- **Updates** flow Worker → UI (pushed on state change, never polled).
- When a new message type is needed, it must be added to `MessageTypes.cs` and either `Commands.cs` or `Updates.cs`.
- On connect, the UI sends `RequestFullState`. The Worker responds with `AppState`, `ClientState`, `ProfileList`, and `ProcessList`.

### Pipe Lifecycle

1. UI starts, `WorkerService.StartAsync` begins trying to connect.
2. After 2 failed attempts, `WorkerLauncher.TryLaunch` starts the Worker with `ShellExecute("runas")` — Windows shows a UAC prompt once.
3. Worker starts, creates the named pipe, sends `WorkerReady`.
4. UI connects, sends `RequestFullState`, begins receiving updates.
5. If the pipe breaks, `WorkerService` reconnects automatically (2s retry).
6. Worker stays alive across UI restarts; UI re-syncs on reconnect.

---

## Conventions

### General & UI
- Target: .NET 8, C# 12, nullable enabled, implicit usings enabled.
- All UI updates must be dispatched: `Application.Current.Dispatcher.BeginInvoke(action)`.
- ViewModels use `[ObservableProperty]` and `[RelayCommand]` from CommunityToolkit.Mvvm.
- Color palette is defined in `App.xaml` (e.g., `{StaticResource AppPrimaryBrush}`, `{StaticResource AppDangerBrush}`). Do not hardcode colors in XAML.
- Keep bindings clean. WPF does not use `x:DataType` compiled bindings by default.
- Key inputs should use `Style="{StaticResource KeyTextBoxStyle}"` combined with the `{StaticResource KeyToStringConverter}` to pretty-print key names (e.g., "NP/" instead of "Divide").

### Worker
- All model instances live on `WorkerCore` as fields/properties.
- `CommandDispatcher` is the only entry point from IPC into models.
- Models must never reference WPF or UI types.
- `WorkerNotifier.RequestTurnOff(reason)` is used by model threads to signal the app should stop.
- Avoid `Thread.Sleep` on the UI thread. Worker background STA threads must use `System.Timers.Timer` or safe loops.

---

## Gotchas & Lessons Learned

**1. WPF Slider Rubberbanding**
When a UI slider is bound to a property that triggers an IPC command to the worker, the worker will eventually broadcast an update back to the UI. Since the slider triggers continuous updates while dragging, the worker's delayed echo of older values will overwrite the UI slider's current value, causing violent rubberbanding.
**Fix**: Always apply a `Delay` to the binding in XAML, e.g., `Value="{Binding AutoOffTime, Delay=150}"`. This ensures the UI waits 150ms after dragging stops before sending the final value, effectively debouncing the IPC loop.

**2. Duplicate Key Binding Interception**
We use a unified `InputHelper.HandleKeyInput()` utility to detect keystrokes in `TextBoxes`. When a user assigns a key, this helper checks if it's already in use across the entire application via `MainWindowViewModel.IsKeyInUse()`. Always route key bindings through this helper instead of raw `PreviewKeyDown` events.

**3. Data-Only Worker Models**
The legacy `Buff.cs` and other models had tightly coupled UI rendering logic (`Bitmap`, `GroupBox`, etc.). The new worker models must be strictly data-only. For example, UI image resolution is handled entirely by WPF using static `pack://application:,,,/Icons/` references.

**4. Global Textbox Styles**
To keep inputs styled consistently across tabs, we define `NumericTextBoxStyle` and `KeyTextBoxStyle` in `App.xaml`. Avoid copy/pasting `Background="{StaticResource AppInputBrush}"` onto individual text boxes; instead use `Style="{StaticResource NumericTextBoxStyle}"` and override sizes locally if necessary.

**5. WPF App Startup Crashes (StaticResource Exceptions)**
If the UI crashes instantly on startup after adding new UI elements to a tab (especially when copying from another location), check the Windows Event Viewer (`Application` log, `.NET Runtime` source). 
A very common cause is a `XamlParseException` related to missing `StaticResource` references (e.g. `System.Exception: Cannot find resource named '...Brush'`). `StaticResource` strictly demands the resource to exist in `App.xaml` at load time, and if it doesn't (due to a typo or copy-pasting an old generic name like `AppTextMutedBrush` instead of `AppSubtleBrush`), the entire WPF application will crash at startup instead of ignoring it.

**6. WPF Dispatcher & IPC Events (InvalidOperationException)**
The `WorkerService` runs the `WorkerCore` IPC client on a background thread. This means all events fired by `WorkerService` (e.g. `AppStateReceived`, `GlobalConfigReceived`) execute on a background threadpool thread. If ViewModels subscribe to these events and update `ObservableProperty` fields that trigger UI updates, or try to interact with WPF singletons like `Application.Current.Resources`, WPF will throw an `InvalidOperationException` and instantly terminate the IPC callback thread.
**Fix**: Always wrap background event callbacks that touch the UI in `Application.Current.Dispatcher.BeginInvoke(action)`.

**7. Profile Loading Data Bleed**
The application uses `ProfileSingleton` to hold the currently loaded configuration. Historically, `Profile.Load` updated the static `_profile` object incrementally by deserializing JSON sections. If an older profile was loaded that was missing a section (e.g., `UserPreferences`), the deserializer would fall back to the defaults of the *currently running* memory instance, causing data like `ToggleStateKey` to invisibly bleed from the previous profile into the loaded profile.
**Fix**: Always create a completely fresh instance (`_profile = new Profile(profileName);`) before loading JSON into singletons to guarantee a clean slate that correctly falls back to system defaults.

---

## The Feature Tabs

The application features the following tabs, implemented as individual WPF views:

1. **AutopotHP/SP** — 5-row grids containing key, percent threshold, and enabled checkboxes.
2. **StatusRecovery** — 3 cure-item lists (Panacea, Royal Jelly, Green Pot) that automatically trigger when character status changes.
3. **SkillTimer** — 10 lanes configuring a key to press, delay interval, click mode, alt-key toggle, and enable state.
4. **SkillSpammer** — List of skill entries to spam at a specific delay with a toggle hotkey.
5. **Autobuff (Skills/Items)** — Buff slot lists for items and skills.
6. **Debuffs** — Auto-cure lists for various debuffs.
7. **AutoOff** — Configures auto-shutdown or auto-exit conditions (timer-based or overweight-based).
8. **Misc** — Houses standalone helper tools, like the **Transfer Helper** (simulates Alt+RightClick to move items between inventory/storage).
9. **Settings/Profiles** — Global application settings and profile management.
10. **ATK x DEF / Songs / MacroSwitch** — Currently pending UI implementation.

---

## File Reference

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
| `ORTools.Worker/Model/Client.cs` | Client, ClientSingleton, HpSpCache, etc. |
| `ORTools.UI/Services/WorkerService.cs` | Pipe client, reconnection, event dispatch |
| `ORTools.UI/Services/WorkerLauncher.cs` | ShellExecute runas to start Worker |
| `ORTools.UI/ViewModels/MainWindowViewModel.cs` | All observable state for main window |
| `ORTools.UI/Views/MainWindow.xaml` | Main window layout (WPF) |
| `ORTools.UI/App.xaml` | Application resources and color palette |


