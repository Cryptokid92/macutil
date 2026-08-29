namespace MacUtilGUI.Tests

open System
open System.IO
open System.Text.Json.Nodes
open Xunit
open MacUtilGUI.Services

module AppCatalogTests =

    let private catalog () =
        ConfigLoader.load (ConfigSchemaTests.findConfigDir ())

    let private requiredIds =
        [ "discord"
          "signal"
          "slack"
          "telegram"
          "thunderbird"
          "whatsapp"
          "github-desktop"
          "jetbrains-toolbox"
          "neovim"
          "sublime-text"
          "visual-studio-code"
          "vscodium"
          "zed"
          "brave-browser"
          "chromium"
          "google-chrome"
          "librewolf"
          "firefox"
          "zen-browser"
          "thorium"
          "vivaldi"
          "waterfox"
          "alacritty"
          "fastfetch"
          "kitty"
          "zsh" ]

    [<Fact>]
    let AppIdsUnique () =
        let dir = ConfigSchemaTests.findConfigDir ()
        let path = Path.Combine(dir, "applications.json")
        let root = JsonNode.Parse(File.ReadAllText path).AsObject()
        let keys = [ for prop in root -> prop.Key ]
        Assert.Equal(keys.Length, keys |> Set.ofList |> Set.count)

        let apps = (ConfigLoader.load dir).Apps
        Assert.Equal(keys.Length, apps.Count)

        for id in requiredIds do
            Assert.True(apps.ContainsKey id, $"missing app id '{id}'")

        let vscode = apps.["visual-studio-code"]
        Assert.Equal(Some "visual-studio-code", vscode.Cask)

        Assert.Equal(Some "telegram-desktop", apps.["telegram"].Cask)
        Assert.Equal(Some "neovim", apps.["neovim"].Formula)
        Assert.Equal(Some "fastfetch", apps.["fastfetch"].Formula)
        Assert.Equal(Some "zsh", apps.["zsh"].Formula)

    [<Fact>]
    let NoAndroidDebloat () =
        let apps = catalog().Apps
        Assert.False(apps.ContainsKey "android-debloat")
        Assert.False(apps.ContainsKey "android-debloater")

        for KeyValue(id, _) in apps do
            Assert.True(
                id.IndexOf("android", StringComparison.OrdinalIgnoreCase) < 0,
                $"android id '{id}'"
            )
