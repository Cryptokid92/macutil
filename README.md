# MacUtil

Cryptokid92's continuation of [ChrisTitusTech/macutil](https://github.com/ChrisTitusTech/macutil) under MIT. CTT marked the original HOLD in March 2026. This fork keeps SIP on and builds toward Winutil-shaped detect, apply, and undo.

The contract is [SPEC.md](SPEC.md). Non-goals include SIP disable, Apple system app deletion, `/var/log` wipes, and MicroWin-style ISO work.

## Run it

You need .NET 9.

```bash
git clone https://github.com/Cryptokid92/macutil.git
cd macutil
dotnet run --project MacUtilGUI/MacUtilGUI.fsproj
```

The window is opaque `#1C1C1E`. The menu bar says MacUtil. Tweaks binds checkboxes to live defaults. Install lists Homebrew apps from `config/applications.json` and detects them with `BrewClient`.

## What changed from the CTT tree

- Fork contract in `SPEC.md`. SIP stays on. User-domain `defaults` never run as root.
- Window chrome. `MainWindow.axaml` dropped AcrylicBlur. `App.axaml` sets `Name="MacUtil"`.
- Action registry in JSON. `config/tweaks.json` and `config/applications.json` load through `ConfigLoader`. A tweak without `OriginalValue` fails load.
- Engine. `ActionEngine.detect`, `apply`, and `undo` talk to `/usr/bin/defaults` as the current user. No sudo. No osascript. A missing key is `appleDefault`, not off. A second apply writes nothing when the live value already matches. Undo deletes the key when `OriginalValue` equals `appleDefault`.
- 34 first-wave tweaks. Finder, Dock, keyboard, screenshots, and privacy-adjacent user defaults. Four of them are Caution. The rest are Safe.
- 26 Homebrew apps, including Alacritty, Fastfetch, Kitty, and ZSH, which the old TOML hid because they had no entries. Android Debloater is not in the catalog.
- `BrewClient` lists and installs casks and formulas. Already installed is success.
- Install tab. Checkboxes from `config/applications.json`. Detect and install go through `BrewClient` with no sudo. Search filters the list. An empty Install writes nothing.

Counts at this commit, regenerate with:

```bash
python3 -c 'import json; print(len(json.load(open("config/tweaks.json"))), len(json.load(open("config/applications.json"))))'
```

## What is coming

Queued after this:

- CLI. `macutil detect`, `apply`, and `undo`.
- Maintenance tab for user-level cleanup. Still no `/var/log`.
- Updates tab. Lists `softwareupdate --list` and `brew outdated`. Does not run `softwareupdate --install`.

## License

MIT. See [LICENSE](LICENSE). Upstream credit remains Chris Titus Tech. Cryptokid92 holds the 2026 continuation line.
