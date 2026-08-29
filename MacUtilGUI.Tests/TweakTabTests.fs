namespace MacUtilGUI.Tests

open Xunit
open MacUtilGUI.Models
open MacUtilGUI.Services
open MacUtilGUI.ViewModels

module TweakTabTests =

    let private catalog () =
        ConfigLoader.load (ConfigSchemaTests.findConfigDir ())

    let private makeVm (client: FakeDefaultsClient) =
        let killer = FakeProcessKiller()
        MainWindowViewModel(catalog (), client :> IDefaultsClient, killer :> IProcessKiller)

    let private findRow (rows: TweakRowViewModel seq) id =
        rows |> Seq.find (fun row -> row.Id = id)

    let private uncheckAll (vm: MainWindowViewModel) =
        for row in vm.SafeTweaks do
            row.IsChecked <- false

        for row in vm.CautionTweaks do
            row.IsChecked <- false

    [<Fact>]
    let TweakRowUncheckedWhenAbsent () =
        let client = FakeDefaultsClient()
        let vm = makeVm client
        let pathBar = findRow vm.SafeTweaks "finder-path-bar"

        Assert.Equal("Show path bar", pathBar.Content)
        Assert.False(ActionEngine.detect client pathBar.Tweak)
        Assert.False(pathBar.IsChecked)

        for row in vm.SafeTweaks do
            Assert.Equal(Risk.Safe, row.Tweak.Risk)

        Assert.NotEmpty(vm.CautionTweaks)

        let standard = Set.ofList (catalog ()).Presets.["Standard"]

        for row in vm.CautionTweaks do
            Assert.Equal(Risk.Caution, row.Tweak.Risk)
            Assert.False(standard.Contains row.Id)
            Assert.False(vm.SafeTweaks |> Seq.exists (fun safe -> safe.Id = row.Id))

    [<Fact>]
    let ApplySelectedIds () =
        let client = FakeDefaultsClient()
        let vm = makeVm client
        uncheckAll vm

        let writesBefore = client.WriteCount
        vm.ApplySelected()
        Assert.Equal(writesBefore, client.WriteCount)
        Assert.Equal("Nothing is selected.", vm.StatusText)
        Assert.Empty(vm.SelectedIds)

        let pathBar = findRow vm.SafeTweaks "finder-path-bar"
        let extensions = findRow vm.SafeTweaks "finder-show-extensions"
        let hidden = findRow vm.SafeTweaks "finder-show-hidden"

        pathBar.IsChecked <- true
        extensions.IsChecked <- true
        Assert.Equal<Set<string>>(set [ "finder-path-bar"; "finder-show-extensions" ], Set.ofList vm.SelectedIds)

        vm.ApplySelected()

        Assert.True(ActionEngine.detect client pathBar.Tweak)
        Assert.True(ActionEngine.detect client extensions.Tweak)
        Assert.False(ActionEngine.detect client hidden.Tweak)
        Assert.True(pathBar.IsChecked)
        Assert.True(extensions.IsChecked)
        Assert.False(hidden.IsChecked)
        Assert.Equal(PrefValue.Bool true, client.Read("com.apple.finder", "ShowPathbar").Value)
        Assert.Equal(PrefValue.Bool true, client.Read("NSGlobalDomain", "AppleShowAllExtensions").Value)
        Assert.True(client.Read("com.apple.finder", "AppleShowAllFiles").IsNone)

        uncheckAll vm
        pathBar.IsChecked <- true
        vm.UndoSelected()
        Assert.True(client.Read("com.apple.finder", "ShowPathbar").IsNone)
        Assert.False(pathBar.IsChecked)
        Assert.True(ActionEngine.detect client extensions.Tweak)
