<div align="center">
  <img src="ORTools.UI/Views/Icons/ui/ortools_pixelart.png" alt="OSRO Tools Logo" width="256"/>
  <h1>OSRO Tools</h1>
  <p><strong>A high-performance automation assistant for Oldschool RO (OSRO)</strong></p>
</div>

---

## 🎯 Overview

OSRO Tools is a high-performance automation assistant built for Oldschool RO. It handles the repetitive and time-sensitive tasks that the game client doesn't manage natively, allowing you to focus on gameplay.

It provides extensive support for automated potion consumption, skill spamming, buff maintenance, and status recovery.

## ✨ Features

- **Autopot HP/SP**: Automatically consumes potions when your HP or SP falls below a configurable percentage.
- **Status Recovery**: Monitors your character's status and automatically uses items (like Panaceas or Green Potions) to cure negative debuffs.
- **Skill Timer**: Configurable multi-lane timers for maintaining specific skills or buffs, with support for cursor-based or center-click targeting.
- **Skill Spammer**: Rapidly spams specified skills while holding or toggling a hotkey.
- **Autobuff**: Automatically reapplies items and skills at configured intervals.
- **Auto Off**: Automatically shuts down the game client based on a timer or when your character becomes overweight.
- **Smart Pausing**: Automatically pauses automation features when you are chatting, dead, or standing in a town.

## 🏗️ Architecture

OSRO Tools is built on **.NET 8** and utilizes a strictly decoupled architecture to ensure maximum performance and seamless UI interaction on older hardware:

1. **ORTools.Worker (Background Core)**
   - Contains all the background logic, Win32 API calls (`ReadProcessMemory`), and state management.
   - Runs entirely on a dedicated background thread to prevent any heavy hooking operations from blocking the UI.
   - Publishes state changes via a decoupled event bus.

2. **ORTools.UI (WPF Frontend)**
   - Modern MVVM UI using the `CommunityToolkit.Mvvm`.
   - Hardware-accelerated DWM compositing allows for smooth window dragging.
   - Runs on the main STA thread.

The Worker and UI communicate seamlessly using an in-memory event bus that passes structured payload envelopes (IPC messages). This enforces a clean separation of concerns, ensuring that the UI models are strictly data-bound and real-time state syncing never causes UI lockups.

## 📋 Changelog (Legacy to Modern .NET 8)

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

## 🚀 Getting Started

### Prerequisites
- Windows OS
- [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### Building and Running

1. Clone the repository.
2. Build the solution using the .NET CLI or Visual Studio:
   ```bash
   dotnet build ORTools.sln
   ```
3. Run the **ORTools.UI** executable. 
   - The application must be run as an Administrator (a UAC prompt will appear) because it requires elevated privileges to read the game's process memory.
   - The background worker will initialize automatically on a separate thread, and you're ready to go!

## ⚙️ Development

If you're looking to contribute or understand the codebase, please read the [AGENTS.md](AGENTS.md) file. It contains comprehensive documentation on the IPC protocol, model conventions, and styling guidelines used throughout the application.
