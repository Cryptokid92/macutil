namespace MacUtilGUI.Tests

open System
open System.IO
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

    let private repoRoot () =
        let rec walk dir =
            if String.IsNullOrEmpty dir then
                None
            elif File.Exists(Path.Combine(dir, "MacUtilGUI", "Views", "MainWindow.axaml")) then
                Some dir
            else
                let parent = Directory.GetParent dir

                if isNull parent then None else walk parent.FullName

        match walk AppContext.BaseDirectory with
        | Some dir -> dir
        | None ->
            match walk (Directory.GetCurrentDirectory()) with
            | Some dir -> dir
            | None -> failwith "repo root not found"

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

    [<Fact>]
    let TweakGroupsByCategory () =
        let client = FakeDefaultsClient()
        let vm = makeVm client

        let safeNames =
            vm.SafeTweakGroups |> Seq.map (fun group -> group.Name) |> Seq.toList

        Assert.Equal<string list>([ "Finder"; "Dock"; "Keyboard"; "Screenshots"; "Privacy" ], safeNames)

        let cautionNames =
            vm.CautionTweakGroups |> Seq.map (fun group -> group.Name) |> Seq.toList

        Assert.Equal<string list>([ "Keyboard"; "Screenshots"; "Privacy" ], cautionNames)

        Assert.Equal(vm.SafeTweaks.Count, vm.SafeTweakGroups |> Seq.sumBy (fun group -> group.Tweaks.Count))
        Assert.Equal(vm.CautionTweaks.Count, vm.CautionTweakGroups |> Seq.sumBy (fun group -> group.Tweaks.Count))

        Assert.False(
            vm.CautionTweakGroups
            |> Seq.exists (fun group -> group.Name = "Finder" || group.Name = "Dock")
        )

        let groupedSafe =
            vm.SafeTweakGroups |> Seq.collect (fun group -> group.Tweaks) |> Seq.toList

        Assert.Equal(vm.SafeTweaks.Count, groupedSafe.Length)

        for row in vm.SafeTweaks do
            Assert.Contains(row, groupedSafe)

        let finder = vm.SafeTweakGroups |> Seq.find (fun group -> group.Name = "Finder")
        finder.AllSelected <- true
        Assert.True(finder.Tweaks |> Seq.forall (fun row -> row.IsChecked))
        Assert.True(finder.AllSelected)

        for row in vm.CautionTweaks do
            Assert.False(row.IsChecked)

        finder.AllSelected <- false
        Assert.True(finder.Tweaks |> Seq.forall (fun row -> not row.IsChecked))
        Assert.False(finder.AllSelected)

        vm.SelectStandard()
        let standard = Set.ofList (catalog ()).Presets.["Standard"]

        for row in vm.SafeTweaks do
            Assert.Equal(standard.Contains row.Id, row.IsChecked)

            if row.IsChecked then
                Assert.True(row.Category = "Finder" || row.Category = "Dock")

        for row in vm.CautionTweaks do
            Assert.False(row.IsChecked)

        uncheckAll vm
        let writesBefore = client.WriteCount
        vm.ApplySelected()
        Assert.Equal(writesBefore, client.WriteCount)
        Assert.Equal("Nothing is selected.", vm.StatusText)
        Assert.Empty(vm.SelectedIds)

        let axaml =
            File.ReadAllText(Path.Combine(repoRoot (), "MacUtilGUI", "Views", "MainWindow.axaml"))

        Assert.Contains("SafeTweakGroups", axaml)
        Assert.Contains("CautionTweakGroups", axaml)
        Assert.Contains("TweakGroupViewModel", axaml)
        Assert.DoesNotContain("ItemsSource=\"{Binding SafeTweaks}\"", axaml)
        Assert.DoesNotContain("ItemsSource=\"{Binding CautionTweaks}\"", axaml)
        Assert.Contains("ItemsSource=\"{Binding AppGroups}\"", axaml)
        Assert.Contains("Maintenance", axaml)
        Assert.Contains("Updates", axaml)
        Assert.Contains("#1C1C1E", axaml)
