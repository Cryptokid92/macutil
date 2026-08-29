namespace MacUtilGUI.Models

type Reload =
    | NoReload
    | Finder
    | Dock

type PrefWrite =
    { Domain: string
      Key: string
      Apply: PrefValue
      OriginalValue: PrefValue }

type Tweak =
    { Id: string
      Content: string
      Description: string
      Category: string
      Writes: PrefWrite list
      AppleDefault: PrefValue
      Reload: Reload
      Risk: Risk }

type AppEntry =
    { Id: string
      Content: string
      Description: string
      Category: string
      Cask: string option
      Formula: string option
      Link: string option }

type Catalog =
    { Tweaks: Map<string, Tweak>
      Apps: Map<string, AppEntry>
      Presets: Map<string, string list> }
