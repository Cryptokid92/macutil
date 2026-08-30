# MacUtil

Cryptokid92's continuation of [ChrisTitusTech/macutil](https://github.com/ChrisTitusTech/macutil) under MIT. CTT marked the original HOLD in March 2026 after Apple closed more of the system. This fork keeps SIP on and only writes things you already own: your user defaults, and Homebrew packages.

The contract is [SPEC.md](SPEC.md). It forbids SIP disable, Apple system app deletion, `/var/log` wipes, and MicroWin-style ISO work.

Version is 0.2.1. Intel and Apple Silicon both build.

## Run it

You need .NET 9. Install, Maintenance, and Updates talk to Homebrew.

```bash
git clone https://github.com/Cryptokid92/macutil.git
cd macutil
dotnet run --project MacUtilGUI/MacUtilGUI.fsproj
```

The window is opaque `#1C1C1E`. The menu bar says MacUtil. Tabs are Tweaks, Install, Maintenance, and Updates. Each tab has its own status line. Tweaks copy does not leak onto Install.

**Tweaks.** 34 user defaults. Finder, Dock, Keyboard, Screenshots, and Privacy sit in expanders. Caution is a separate band, not mixed into Safe. Detect reads live `defaults`. Apply and Undo write selected ids only. Empty selection writes nothing and says `Nothing is selected.` Standard selects 17 Safe Finder and Dock ids. Minimal selects 4. Import and Export use a JSON array of ids.

**Install.** 26 Homebrew apps from `config/applications.json`, grouped by category: Communication Apps, Developer Tools, Web Browsers, Terminal, Shell, Utilities. Detect is `brew list`, not a name match in `/Applications`. Search filters rows, then drops empty groups. Header checkboxes select a whole group. Install, Uninstall, and Refresh sit in the footer. Refresh re-runs `brew list` and checks installed catalog rows. Empty Install or Uninstall writes nothing. Missing uninstall is success. No sudo. Never deletes Apple system apps.

**Maintenance.** `brew update`, `brew cleanup`, and the user Homebrew cache from `brew --cache`. The engine refuses a path under `/var`. `brew cleanup` twice is success. Emptying Trash is not a default Safe action. No sudo.

**Updates.** Lists `softwareupdate --list` and `brew outdated`. A macOS Tahoe row is labeled Major, stays unchecked, and cannot be checked. Copy command and the help line point at System Settings for major OS upgrades. Update Homebrew is opt-in on selected outdated packages. The program never runs Apple's install flag.

CLI commands share `ActionEngine`. No sudo. The CLI does not install or uninstall Homebrew apps.

```bash
dotnet run --project MacUtilCLI -- detect
dotnet run --project MacUtilCLI -- apply --preset Standard
dotnet run --project MacUtilCLI -- undo --preset Standard
dotnet run --project MacUtilCLI -- export
dotnet run --project MacUtilCLI -- import preset.json
```

```bash
dotnet test MacUtilGUI.Tests --configuration Release
```

## What is added

- Fork contract in `SPEC.md`. SIP stays on. User-domain `defaults` never run as root.
- Action registry in JSON. `config/tweaks.json` and `config/applications.json` load through `ConfigLoader`. A tweak without `OriginalValue` fails load.
- Engine. `ActionEngine.detect`, `apply`, and `undo` talk to `/usr/bin/defaults` as the current user. No sudo. No osascript. A missing key is Apple default, not off. A second apply writes nothing when the live value already matches. Undo deletes the key when `OriginalValue` equals `appleDefault`. After a successful Finder or Dock write the engine runs `/usr/bin/killall` as you, not as root.
- 34 first-wave tweaks. Ten Finder, seven Dock, nine Keyboard, three Screenshots, five Privacy. Four are Caution. The rest are Safe.
- 26 Homebrew apps, including Alacritty, Fastfetch, Kitty, and ZSH, which the old TOML hid because they had no entries. Android Debloater is not in the catalog.
- `BrewClient` lists, installs, and uninstalls casks and formulas. Already installed is success. Missing uninstall is success.
- Tweaks tab. Checkboxes bind to detect. Groups use `Tweak.Category` inside Safe and Caution. Apply and Undo pass selected ids only.
- Install tab. Groups use `AppEntry.Category`. Search filters. Install, Uninstall, Refresh. No sudo brew.
- Presets. `config/preset.json` names Standard (17 Safe Finder and Dock ids) and Minimal (4 Safe ids). Caution ids stay out. Import of an unknown id fails and writes nothing.
- Maintenance tab. `brew-update`, `brew-cleanup`, and `user-cache-brew`. `MaintenanceEngine` refuses a path under `/var`.
- Updates tab. Parses `softwareupdate --list`. Labels macOS Tahoe as Major and leaves it unchecked. `brew outdated` is a separate opt-in list. No Apple install action.
- Per-tab status. Export of ids uses `Utf8JsonWriter` so a trimmed GUI can still save a preset.
- CLI. `dotnet run --project MacUtilCLI`. `detect` prints JSON of id to applied bool. `apply` and `undo` take `--preset`. `export` writes applied ids. `import` applies a JSON array of ids.
- Opaque window. AcrylicBlur hid the window on Sequoia, so it is gone.
- Universal CI. macos-13 is set to publish `osx-x64`. macos-14 publishes `osx-arm64` and lipos a universal zip. Release no longer tries to emit Mach-O from Ubuntu.
- Tests. Schema, engine, tabs, presets, Maintenance, Updates, CLI, grouping, uninstall, and trim-safe export. Fake `defaults` and fake brew so CI does not write the runner's prefs.

Counts at this commit, regenerate with:

```bash
python3 -c 'import json; print(len(json.load(open("config/tweaks.json"))), len(json.load(open("config/applications.json"))))'
```

## What changed from the CTT tree

- The GUI is no longer a TOML script launcher. `ScriptService.fs`, `ScriptInfo`, `ScriptCategory`, and `tab_data.toml` are gone.
- The leftover linutil workflow `bashisms.yml` is gone.
- `system-cleanup.sh` is gone. It used to delete `/var/log`. Maintenance now runs brew only.
- `fix-finder.sh` is gone. It used to wipe home `.DS_Store`. Finder view is a catalog tweak now.
- Version lock is 0.2.1 across the fsproj, Info.plist, and local build scripts.

## What is coming

Not on `main` yet, and not promised as a date:

- More Homebrew apps in `config/applications.json`.
- CLI `install` and `uninstall` of catalog ids.
- Header checkboxes that follow later child clicks. Today a mixed group stays unchecked on the header until you use the header again.

Still out. Winutil Config, MicroWin, SIP off, Apple system app deletion, `/var/log` wipes, a plugin store, a second package manager.

## License

MIT. See [LICENSE](LICENSE). Upstream credit remains Chris Titus Tech. Cryptokid92 holds the 2026 continuation line.
