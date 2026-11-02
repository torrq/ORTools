# OSRO Tools v2.1 — BETA 11 Release Notes

## Overview
OSRO Tools v2.1 BETA 11 introduces major performance modernizations, input timing parity with legacy v1.0.10, multi-key hold strategies for Skill Spammer and ATK x DEF, deterministic debuff recovery priority, and robust state-locking to eliminate Turn On / Turn Off race conditions and unwanted auto-off triggering.

---

## Changes by Category

### Auto Off & State Synchronization
- **Prevent Auto Off While OFF**: The countdown timer and overweight monitor are strictly gated by application state. When turned off, timers auto-pause and background monitor loops immediately abort without triggering macros or killing the client.
- **Timer Pause & Resume**: Turning automation OFF pauses the running countdown; toggling it back ON resumes seamlessly.
- **Continuous Live Status Display**: Real-time HP, SP, Weight, Level, Job Level, EXP, and current Map continue streaming to the UI header even when automation is turned off.
- **Serialized State Transitions**: Synchronized `HandleTurnOn` and `HandleTurnOff` in `WorkerCore` using an internal state lock, eliminating race conditions between UI clicks, hotkey toggles, and background thread terminations.
- **Startup Process Verification**: Turn On re-verifies that the game client is responsive after the initial 300ms startup delay, aborting cleanly if the client exited.
- **Macro Execution Hardening**: Added null and process exit checks in `WeightLimitMacro`. Decoupled `AutoOffKillClient` so the client exit routine executes reliably even if Key 1 and Key 2 are blank.
- **Suppressed Redundant Logs**: `WorkerNotifier` ignores turn-off requests if automation is already inactive, eliminating duplicate log spam from shutting threads.

### Skill Spammer & ATK x DEF Modernization
- **Multi-Key Hold Modes** (*Settings > Behavior*):
  - `RoundRobinAll`: Alternates skill casts evenly across all currently held hotkeys.
  - `LastPressedWins`: Prioritizes the most recently pressed hotkey, falling back seamlessly to any remaining held keys when released.
- **High-Precision Timing (`WaitInterruptibly`)**: Replaced drifting sleep accumulators with monotonic `Stopwatch` deadline waits featuring a 1.5ms spin cushion, eliminating OS scheduler wake-up overshoot.
- **Fast Win32 Input (`SendKeyFast` / `SendLeftClickFast`)**: Matched legacy v1.0.10 timing by posting raw `WM_KEYDOWN` and fast mouse clicks (`WM_LBUTTONDOWN` + 1ms + `WM_LBUTTONUP`), reducing click injection latency from 25ms to 1ms.
- **Finishing Action Fix**: Click-enabled macros now cleanly finish with a standard left-click to clear skill targeting reticles without causing accidental camera rotation.
- **Shift Key Ownership**: Atomic ownership tracking ensures Skill Spammer only releases Shift if it physically engaged it, preventing sticky OS-level Shift states.
- **ATK x DEF Integration**: ATK x DEF shares the unified `HoldMultiKeyMode` and high-speed input loop, interleaving ammo switching and automatically restoring defensive gear upon key release.

### Status Recovery & Debuffs
- **Deterministic Priority Order**: Debuff curing now strictly follows priority: **Green Potion** -> **Panacea** -> **Royal Jelly**.
- **Per-Cycle Key Deduplication**: Multiple simultaneous debuffs that share a cure item only trigger a single keystroke per cycle.
- **Responsive Thread Termination**: Added responsive wait intervals and active flags so stopping automation halts recovery threads immediately.
- **Debuff Status Icons**: Debuff recovery entries now display their respective in-game icons.

### Performance & Windows 11
- **Power Throttling Opt-Out**: Disabled Windows 11 process power throttling on the worker process to preserve 1ms timer resolution when minimized or behind the game window.
- **Chat Detection Caching**: Added a 15ms cache to chat/text input checks, eliminating thousands of redundant cross-process memory reads per second across background loops.
- **Hot-Path Zero Allocation**: Pre-allocated buffers and cached snapshot collections in polling loops to minimize garbage collection pressure during rapid skill spam.
