namespace MacUtilGUI.Tests

open System.IO
open System.Text.Json.Nodes
open Xunit
open MacUtilGUI.Models
open MacUtilGUI.Services
open MacUtilGUI.ViewModels
open MacUtilCLI

module PresetTests =

    let private catalog () =
        ConfigLoader.load (ConfigSchemaTests.findConfigDir ())

    let private makeVm (client: FakeDefaultsClient) =
        let killer = FakeProcessKiller()
        MainWindowViewModel(catalog (), client :> IDefaultsClient, killer :> IProcessKiller)

    let private findRow (rows: TweakRowViewModel seq) id =
        rows |> Seq.find (fun row -> row.Id = id)

    let private runCli catalog client killer argv =
        let outw = new StringWriter()
        let errw = new StringWriter()

        let code =
            Cli.run catalog (client :> IDefaultsClient) (killer :> IProcessKiller) argv outw errw

        code, outw.ToString(), errw.ToString()

    [<Fact>]
    let StandardSafeOnly () =
        let loaded = catalog ()
        Assert.True(loaded.Presets.ContainsKey "Standard")
        Assert.True(loaded.Presets.ContainsKey "Minimal")

        let standard = loaded.Presets.["Standard"]
        let minimal = loaded.Presets.["Minimal"]
        Assert.NotEmpty(standard)
        Assert.NotEmpty(minimal)

        let finderDockSafe =
            loaded.Tweaks
            |> Map.toList
            |> List.choose (fun (id, tweak) ->
                match tweak.Risk, tweak.Category with
                | Risk.Safe, "Finder"
                | Risk.Safe, "Dock" -> Some id
                | _ -> None)
            |> Set.ofList

        Assert.Equal<Set<string>>(finderDockSafe, Set.ofList standard)

        for id in standard do
            let tweak = loaded.Tweaks.[id]
            Assert.Equal(Risk.Safe, tweak.Risk)
            Assert.True(tweak.Category = "Finder" || tweak.Category = "Dock")

        for id in minimal do
            Assert.Contains(id, standard)
            Assert.Equal(Risk.Safe, loaded.Tweaks.[id].Risk)

        Assert.True(minimal.Length < standard.Length)

        for KeyValue(id, tweak) in loaded.Tweaks do
            if tweak.Risk = Risk.Caution then
                Assert.DoesNotContain(id, standard)
                Assert.DoesNotContain(id, minimal)

    [<Fact>]
    let ImportUnknownId () =
        let loaded = catalog ()
        let client = FakeDefaultsClient()
        let killer = FakeProcessKiller()
        let vm = makeVm client
        let pathBar = findRow vm.SafeTweaks "finder-path-bar"
        pathBar.IsChecked <- true
        let writesBefore = client.WriteCount
        let json = """["not-a-real-tweak"]"""

        match PresetService.parseImport loaded json with
        | Error msg -> Assert.Contains("Unknown tweak id", msg)
        | Ok _ -> Assert.Fail("unknown id must fail")

        Assert.False(vm.ImportJson json)
        Assert.True(pathBar.IsChecked)
        Assert.Equal(writesBefore, client.WriteCount)
        Assert.Contains("Unknown tweak id", vm.StatusText)

        let path = Path.GetTempFileName()
        File.WriteAllText(path, json)
        let code, _, err = runCli loaded client killer [| "import"; path |]
        Assert.NotEqual(0, code)
        Assert.Equal(writesBefore, client.WriteCount)
        Assert.Contains("Unknown tweak id", err)

        match PresetService.parseImport loaded """["finder-path-bar", "not-a-real-tweak"]""" with
        | Error msg -> Assert.Contains("Unknown tweak id", msg)
        | Ok _ -> Assert.Fail("mixed unknown id must fail without a partial list")

    [<Fact>]
    let CliDetect () =
        let loaded = catalog ()
        let client = FakeDefaultsClient()
        let killer = FakeProcessKiller()
        let code, stdout, err = runCli loaded client killer [| "detect" |]
        Assert.Equal(0, code)
        Assert.Equal("", err)
        Assert.Contains("finder-path-bar", stdout)

        let node = JsonNode.Parse(stdout)
        Assert.False(node.["finder-path-bar"].GetValue<bool>())
        Assert.False(node.["finder-show-extensions"].GetValue<bool>())

        ActionEngine.apply client killer loaded.Tweaks.["finder-path-bar"]
        Assert.True(ActionEngine.detect client loaded.Tweaks.["finder-path-bar"])

        let code2, stdout2, err2 = runCli loaded client killer [| "detect" |]
        Assert.Equal(0, code2)
        Assert.Equal("", err2)
        let node2 = JsonNode.Parse(stdout2)
        Assert.True(node2.["finder-path-bar"].GetValue<bool>())
        Assert.False(node2.["finder-show-extensions"].GetValue<bool>())

    [<Fact>]
    let SelectStandardLeavesCautionUnchecked () =
        let client = FakeDefaultsClient()
        let vm = makeVm client
        let caution = Seq.head vm.CautionTweaks
        caution.IsChecked <- true
        let standard = Set.ofList (catalog ()).Presets.["Standard"]

        vm.SelectStandard()

        Assert.False(caution.IsChecked)

        for row in vm.SafeTweaks do
            Assert.Equal(standard.Contains row.Id, row.IsChecked)

        for row in vm.CautionTweaks do
            Assert.False(row.IsChecked)

        vm.SelectMinimal()
        let minimal = Set.ofList (catalog ()).Presets.["Minimal"]

        for row in vm.SafeTweaks do
            Assert.Equal(minimal.Contains row.Id, row.IsChecked)

        for row in vm.CautionTweaks do
            Assert.False(row.IsChecked)

        let json = vm.ExportSelected()
        Assert.Contains("finder-path-bar", json)

        for row in vm.SafeTweaks do
            row.IsChecked <- false

        Assert.True(vm.ImportJson json)
        Assert.True((findRow vm.SafeTweaks "finder-path-bar").IsChecked)
        Assert.False((findRow vm.SafeTweaks "finder-show-hidden").IsChecked)
