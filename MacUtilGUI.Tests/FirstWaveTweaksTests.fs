namespace MacUtilGUI.Tests

open System.IO
open System.Text.Json.Nodes
open Xunit
open MacUtilGUI.Models
open MacUtilGUI.Services

module FirstWaveTweaksTests =

    let private firstWaveIds =
        [| "finder-list-view"
           "finder-search-current-folder"
           "finder-empty-trash-30d"
           "finder-show-extensions"
           "finder-status-bar"
           "finder-path-bar"
           "finder-show-hidden"
           "finder-folders-first"
           "finder-no-ext-warning"
           "finder-posix-title"
           "dock-no-mru-spaces"
           "dock-autohide"
           "dock-autohide-delay-0"
           "dock-autohide-time-0"
           "dock-hide-recents"
           "dock-no-launch-anim"
           "dock-min-to-app"
           "kbd-key-repeat"
           "kbd-no-autocap"
           "kbd-no-period"
           "kbd-no-dash"
           "kbd-no-quotes"
           "kbd-no-autocorrect"
           "kbd-full-access"
           "screenshot-no-shadow"
           "screenshot-type-png"
           "privacy-no-ad-tracking"
           "privacy-save-not-icloud"
           "privacy-no-dsstore-network"
           "privacy-no-dsstore-usb"
           "kbd-repeat-rate"
           "kbd-repeat-delay"
           "screenshot-no-thumbnail"
           "privacy-apple-intelligence-off" |]

    let private cautionIds =
        Set
            [ "kbd-repeat-rate"
              "kbd-repeat-delay"
              "screenshot-no-thumbnail"
              "privacy-apple-intelligence-off" ]

    let private catalog () =
        ConfigLoader.load (ConfigSchemaTests.findConfigDir ())

    let private tweaksJsonPath () =
        Path.Combine(ConfigSchemaTests.findConfigDir (), "tweaks.json")

    [<Fact>]
    let FirstWaveSchema () =
        let loaded = catalog ()
        Assert.True(loaded.Tweaks.Count >= 20)

        let json = File.ReadAllText(tweaksJsonPath ())
        let root = JsonNode.Parse(json).AsObject()

        for id in firstWaveIds do
            Assert.True(loaded.Tweaks.ContainsKey id, $"missing tweak '{id}'")
            Assert.True(root.ContainsKey id, $"missing json object '{id}'")

            let tweak = loaded.Tweaks.[id]
            let obj = root.[id].AsObject()
            Assert.True(obj.ContainsKey "appleDefault", $"{id} missing appleDefault")

            match tweak.Writes with
            | [ _ ] ->
                let writeObj = obj.["writes"].AsArray().[0].AsObject()
                Assert.True(writeObj.ContainsKey "OriginalValue", $"{id} missing OriginalValue")
            | _ -> Assert.Fail($"{id} must have one write")

            if cautionIds.Contains id then
                Assert.Equal(Risk.Caution, tweak.Risk)
            else
                Assert.Equal(Risk.Safe, tweak.Risk)

        Assert.Equal<Set<string>>(Set.ofArray firstWaveIds, Set.ofSeq loaded.Tweaks.Keys)

        let pathBar = loaded.Tweaks.["finder-path-bar"]
        Assert.Equal(PrefValue.Bool true, pathBar.Writes.Head.Apply)
        Assert.Equal(pathBar.AppleDefault, pathBar.Writes.Head.OriginalValue)
        Assert.Equal("com.apple.finder", pathBar.Writes.Head.Domain)
        Assert.Equal("ShowPathbar", pathBar.Writes.Head.Key)

        let mru = loaded.Tweaks.["dock-no-mru-spaces"]
        Assert.Equal(PrefValue.Bool true, mru.AppleDefault)
        Assert.Equal(PrefValue.Bool false, mru.Writes.Head.Apply)
        Assert.Equal("com.apple.dock", mru.Writes.Head.Domain)
        Assert.Equal("mru-spaces", mru.Writes.Head.Key)

        Assert.False(loaded.Tweaks.ContainsKey "android-debloat")

    [<Fact>]
    let ForbiddenKeysAbsent () =
        let json = File.ReadAllText(tweaksJsonPath ())
        Assert.DoesNotContain("reduceMotion", json)
        Assert.DoesNotContain("NSTextShowsControlCharacters", json)

        let loaded = catalog ()

        for KeyValue(_, tweak) in loaded.Tweaks do
            for write in tweak.Writes do
                Assert.False(write.Key = "reduceMotion")
                Assert.False(write.Key = "NSTextShowsControlCharacters")
                Assert.False(write.Domain = "com.apple.mail")
                Assert.False(write.Domain = "com.apple.Mail")
                Assert.False(write.Domain = "com.apple.universalaccess")

    [<Fact>]
    let ApplyFinderExtensions () =
        let tweak = catalog().Tweaks.["finder-show-extensions"]
        let client = FakeDefaultsClient()
        let killer = FakeProcessKiller()
        ActionEngine.apply client killer tweak
        Assert.Equal(PrefValue.Bool true, client.Read("NSGlobalDomain", "AppleShowAllExtensions").Value)
        Assert.Equal<string list>([ "Finder" ], killer.Killed)
        Assert.True(ActionEngine.detect client tweak)
