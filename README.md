<div align="center">
  <img src="ORTools.UI/Views/ortools-hr.png" alt="OSRO Tools Logo" width="150"/>
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

OSRO Tools is built on **.NET 8** and utilizes a modern, dual-process architecture to ensure maximum performance and seamless UI interaction on older hardware:

1. **ORTools.Worker (Elevated Console)**
   - Runs as Administrator.
   - Handles all `ReadProcessMemory` / `OpenProcess` Win32 API calls directly.
   - Contains all the background logic and state management.
   - Hosts a Named Pipe Server for IPC communication.

2. **ORTools.UI (Non-Elevated WPF)**
   - Runs as standard user (medium integrity).
   - Modern MVVM UI using the `CommunityToolkit.Mvvm`.
   - Bypasses UIPI overhead, allowing for smooth window dragging and hardware-accelerated DWM compositing.

The two processes communicate seamlessly using a lightweight, JSON-based Inter-Process Communication (IPC) protocol over Named Pipes, enabling real-time feedback and state syncing without UI lockups.

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
   - The UI will attempt to launch the background Worker automatically.
   - You will see a Windows UAC prompt once to allow the Worker to run as Administrator.
   - The UI will connect to the Worker and you're ready to go!

## ⚙️ Development

If you're looking to contribute or understand the codebase, please read the [AGENTS.md](AGENTS.md) file. It contains comprehensive documentation on the IPC protocol, model conventions, and styling guidelines used throughout the application.
