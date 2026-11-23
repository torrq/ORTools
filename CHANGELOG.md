# Changelog

All notable changes to OSRO Tools are documented in this file.

---

## v2.1

### Skill Spammer & ATK x DEF
- **Classic v1 Speed**: Matched legacy v1.0.10 key and click timing, making Skill Spammer and ATK x DEF noticeably faster.
- **Multi-Key Hold Modes** (*Settings > Behavior*): Choose how holding multiple spam keys behaves:
  - **Round Robin**: Alternates casts evenly across all held keys.
  - **Last Pressed Wins**: Spams the most recently pressed key and falls back to earlier held keys when you let go.
- **ATK x DEF Improvements**:
  - Fixed Row 2 edits overwriting Row 1 when loading profiles.
  - Consistent equip order (`Head → Body → Weapon → Shield → Garment → Shoes`) and automatic DEF gear restore on key release (skipped safely if you die, chat, or alt-tab).
  - Supports Multi-Key Hold Modes and interleaved ammo switching.
- **Spammer Reliability Fixes**:
  - Fixed `NoShift` leaving Shift stuck down when interrupted.
  - Toggle mode resets cleanly on Turn Off, and `Alt` combos (like `Alt+Tab`) no longer trigger Toggle Mode.
  - Fixed `F10` and `1–9` keybinds registering/spamming properly, and added instant stop when you open chat, die, alt-tab, or turn automation off.
  - Added a 1ms minimum delay floor so `0`ms settings don't lock up or behave inconsistently.

### Songs, Macro Switch & Skill Timer
- **Songs & Macro Switch**: Sequences finish their full playback even on a quick tap without repeating endlessly; fixed Songs sending the Adaptation/cancel key twice.
- **Skill Timer**: Removed the unreliable *Center* click option (existing profiles auto-convert to *Cursor*).

### Autobuff, Status Recovery & Debuffs
- **Debuff Cure Priority**: Always cures in a fixed order (**Green Potion → Panacea → Royal Jelly**) with status icons shown on each debuff entry and no duplicate key presses when multiple debuffs share a cure.
- **Autobuff Items (HR)**: Fixed *Unlock Bubblegum* buff detection on High Rate.

### Auto Off & Live Status
- **No False Triggers**: Auto Off timers and overweight checks no longer fire while automation is turned off; countdowns pause on Turn Off and resume on Turn On.
- **Always-On Header**: HP, SP, Weight, Level, EXP, and Map keep updating even while automation is turned off.
- **Safer Client Exit**: *Kill Client* on overweight now works even if macro keys 1 and 2 are left blank.

### Settings, UI & Stability
- **Custom Theme** (*Settings > Interface*): Pick any accent color to generate a matching light or dark theme.
- **Debug View & Light Theme Polish**: Improved light-theme readability for dropdowns and debug logs, plus a font size slider and reset-to-default buttons.
- **Legacy Profile Migration**: Automatically converts older WinForms ATK x DEF slot/key formats, Skill Spammer `1–9` keys, and Skill Timer *Center* clicks.
- **Keybind & Default Improvements**: The `Delete` key can now be bound as a hotkey (use `Backspace` or `Escape` to clear a keybind), and *Pause when chatting* is now enabled by default for new configs.
- **Safer Profile Saving & Text Encoding**: Profiles save atomically to prevent corruption on crashes, and accented character/map names display properly.
- **Better Background Performance**: Opted out of Windows 11 background power throttling and optimized status checks so timers and spamming stay responsive.

---

## v2.0 (from v1.0)

### Complete Rebuild & Performance
- **Modern .NET 8 & WPF Engine**: Rebuilt from the ground up (replacing .NET Framework 4.8 and WinForms) for a faster, smoother, hardware-accelerated interface that no longer freezes or stutters when dragging the window.
- **Dedicated Background Engine**: Automation (HP/SP reading, key presses, buff timers) now runs on its own thread, separate from the UI, so busy automation never lags the window.
- **Profile Compatibility**: Supports importing profiles from **v1.0.10** and **v1.0.11** (once opened and saved in v2.0+, profiles are upgraded to the new format).

### New Features & UI
- **Unified Autobuff Search**: Combined Autobuff Skills and Items into a single search bar.
- **Language Support** (*Settings*): Added **English** and **Filipino (Tagalog)** localization.
- **App Themes** (*Settings*): Added **Green**, **Red**, **Blue**, and **Mono** themes in **Light** and **Dark** variants, plus a **System** option that follows Windows.
- **Status Logger**: New tab showing active buffs and debuffs read directly from game memory in real time.
- **EXP Per Hour Tracker**: Optional live EXP/hour counter on the main panel below your character name.
- **Auto-Update Checker**: Automatically checks GitHub on startup and notifies you when a new version is available.
- **Configurable Row Count**: Choose how many rows to display in **Songs**, **Macro Switch**, and **ATK x DEF** from **Settings**.
- **Improved Debug Log**: Rebuilt with color-coded messages for easier reading.
- **General Polish**: More reliable key input handling, more stable Auto Off (timer and overweight shutdown), and consistent input box styling across every tab.
