# MacUtil

Cryptokid92's continuation of [ChrisTitusTech/macutil](https://github.com/ChrisTitusTech/macutil) under MIT. CTT marked the original HOLD in March 2026 after Apple closed more of the system. This fork keeps SIP on and only writes things you already own: your user defaults, and Homebrew packages.

The contract is [SPEC.md](SPEC.md). It forbids SIP disable, Apple system app deletion, `/var/log` wipes, and MicroWin-style ISO work.

Version is 0.2.1. Intel and Apple Silicon both build.

## Run it

You need .NET 9.

```bash
git clone https://github.com/Cryptokid92/macutil.git
cd macutil
dotnet run --project MacUtilGUI/MacUtilGUI.fsproj
dotnet run --project MacUtilCLI -- detect
```

The window is opaque `#1C1C1E`. The menu bar says MacUtil.

**Tweaks.** 34 user defaults as checkboxes. Detect reads live `defaults`. Apply and Undo write the selected ids only. Empty selection writes nothing and the status line says `Nothing is selected.` Caution rows sit in their own group, not mixed into Safe. Standard and Minimal select Safe Finder and Dock ids. Import and Export use a JSON array of ids.

**Install.** 26 Homebrew apps from `config/applications.json`. Detect is `brew list`, not a name match in `/Applications`. Search filters the list. Empty Install writes nothing. No sudo.

**Maintenance.** `brew update`, `brew cleanup`, and the user Homebrew cache from `brew --cache`. The engine refuses a path under `/var`. `brew cleanup` twice is success. Emptying Trash is not a default Safe action. No sudo.

**Updates.** Lists `softwareupdate --list` and `brew outdated`. A macOS Tahoe row is labeled Major and stays unchecked. Copy command and the help line point at System Settings for major OS upgrades. Update Homebrew is opt-in on selected outdated packages. The program never runs Apple's install flag.

CLI commands share `ActionEngine`. No sudo.

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
- 34 first-wave tweaks. Finder, Dock, keyboard, screenshots, and a few privacy keys in the user domain. Four are Caution. The rest are Safe.
- 26 Homebrew apps, including Alacritty, Fastfetch, Kitty, and ZSH, which the old TOML hid because they had no entries. Android Debloater is not in the catalog.
- `BrewClient` lists and installs casks and formulas. Already installed is success.
- Tweaks tab. Checkboxes bind to detect. Apply and Undo pass selected ids only.
- Install tab. Checkboxes bind to brew. Search filters. No sudo brew.
- Presets. `config/preset.json` names Standard (Safe Finder and Dock ids) and Minimal (a smaller Safe subset). Caution ids stay out. Import of an unknown id fails and writes nothing.
- Maintenance tab. `brew-update`, `brew-cleanup`, and `user-cache-brew`. `MaintenanceEngine` refuses a path under `/var`. `brew cleanup` twice still calls brew and returns ok. Emptying Trash is not a default Safe action.
- Updates tab. Parses `softwareupdate --list`. Labels macOS Tahoe as Major and leaves it unchecked. `brew outdated` is a separate opt-in list. Help text points at System Settings for major OS upgrades. No Apple install action.
- CLI. `dotnet run --project MacUtilCLI`. `detect` prints JSON of id to applied bool. `apply` and `undo` take `--preset`. `export` writes applied ids. `import` applies a JSON array of ids.
- Opaque window. AcrylicBlur hid the window on Sequoia, so it is gone.
- Universal CI. macos-13 publishes `osx-x64`. macos-14 publishes `osx-arm64` and lipos a universal zip. Release no longer tries to emit Mach-O from Ubuntu.
- Tests. Schema, engine, Tweaks tab, Install tab, presets, Maintenance, Updates, and CLI. Fake `defaults` and fake brew so CI does not write the runner's prefs.

Counts at this commit, regenerate with:

```bash
python3 -c 'import json; print(len(json.load(open("config/tweaks.json"))), len(json.load(open("config/applications.json"))))'
```

## What changed from the CTT tree

- The GUI is no longer a TOML script launcher. `ScriptService.fs`, `ScriptInfo`, `ScriptCategory`, and `tab_data.toml` are gone.
- The leftover linutil workflow `bashisms.yml` is gone.
- `system-cleanup.sh` no longer deletes `/var/log`. `fix-finder.sh` no longer wipes home `.DS_Store`.
- Version lock is 0.2.1 across the fsproj, Info.plist, and local build scripts.

## License

MIT. See [LICENSE](LICENSE). Upstream credit remains Chris Titus Tech. Cryptokid92 holds the 2026 continuation line.
