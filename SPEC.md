# MacUtil contract

This file is the project contract.

MacUtil is Cryptokid92's continuation of ChrisTitusTech/macutil under MIT.

The program runs on Intel Macs and Apple Silicon Macs. MacUtil provides detect, apply, undo, Homebrew install, and presets. The rule is SIP stays on.

## Goals

- detect live user defaults and Homebrew app state
- apply selected tweaks and Homebrew install
- undo applied tweaks from OriginalValue
- Homebrew install of catalog apps
- presets that select a named set of Safe tweak ids

## Non-Goals

- SIP disable
- Apple system app deletion
- /var/log wipes
- MicroWin-style ISO work

## Safety

SIP stays on. The program never disables SIP. User-domain defaults never run as root. The engine never uses sudo for defaults write. The engine never uses osascript elevation for defaults write.

## Maintenance

Actions are brew-update, brew-cleanup, and user-cache-brew. The engine refuses a path under /var. brew cleanup twice is success. The engine never deletes /var/log. Emptying Trash is not a default Safe action.

## Action registry

Tweaks and apps live in JSON under `config/`. That catalog is the Action registry.

Tweaks have domain, key, apply, original, appleDefault, reload, and risk.

The original field is OriginalValue.

Apps have cask or formula.

Missing defaults keys are Apple default, not off.

detect compares the live value to apply. A missing key matches appleDefault.

apply writes the apply value. A second apply writes nothing when the live value already matches.

undo restores OriginalValue. If original is absent, undo deletes the key.

reload names Finder or Dock after a successful write.

risk is Safe or Caution. Standard presets include only Safe ids.
