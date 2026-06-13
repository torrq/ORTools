# ORTools Scripts Directory

This directory contains various utility scripts written in Python (`.py`) and PowerShell (`.ps1`) that were used to automate large-scale refactoring and resource migration during the conversion from the legacy WinForms architecture to the modernized WPF/IPC architecture.

### Files and Purposes

* **`copy_item_icons.ps1`**
  Parses the legacy `4RTools-OSRO` codebase to extract item icon mappings and automatically copies the corresponding image assets over to the `ORTools.UI\Icons\Items` directory.

* **`patch_handlers.py`**, **`patch_handlers_fix.py`**, **`patch_missed_handlers.py`**
  Scripts used during the input refactoring phase to bulk-modify `WorkerCore.cs`. They automatically injected the `UnbindKeyGlobally` logic and rebroadcasted updated configuration state (`PushAllConfigs`) into dozens of repetitive IPC command handlers.

* **`patch_ui.py`**
  Automated bulk replacements across the `ORTools.UI` ViewModels and Views to align with the new strict IPC messaging structure and dispatcher requirements.

* **`patch_worker.py`**
  Automated bulk replacements across the `ORTools.Worker` backend models to migrate away from legacy WinForms tight coupling into the new data-only model structure.

* **`update_bindings.py`**
  A utility to mass-update WPF `.xaml` files, converting raw `<TextBox>` inputs to use the new standardized `{StaticResource KeyTextBoxStyle}` and `{StaticResource NumericTextBoxStyle}` with delayed bindings.

* **`update_resources.py`**
  A helper script to bulk-find and replace hardcoded colors or legacy brush names in XAML files to enforce the new global `App.xaml` palette (e.g., converting to `AppPrimaryBrush` or `AppInputBrush`).
