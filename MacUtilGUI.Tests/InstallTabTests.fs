namespace MacUtilGUI.Tests

open Xunit
open MacUtilGUI.Services
open MacUtilGUI.ViewModels

module InstallTabTests =

    let private catalog () =
        ConfigLoader.load (ConfigSchemaTests.findConfigDir ())

    let private makeVm (brew: BrewClientTests.FakeBrew) =
        let client = FakeDefaultsClient()
        let killer = FakeProcessKiller()
        MainWindowViewModel(catalog (), client :> IDefaultsClient, killer :> IProcessKiller, brew.Exec)

    let private findApp (rows: AppRowViewModel seq) id =
        rows |> Seq.find (fun row -> row.Id = id)

    let private uncheckAll (vm: MainWindowViewModel) =
        for row in vm.AllApps do
            row.IsChecked <- false

    [<Fact>]
    let InstallDetectChecksBox () =
        let brew = BrewClientTests.FakeBrew([ "visual-studio-code" ], [ "neovim" ])
        let vm = makeVm brew
        let vscode = findApp vm.AllApps "visual-studio-code"
        let neovim = findApp vm.AllApps "neovim"
        let discord = findApp vm.AllApps "discord"

        Assert.Equal("Visual Studio Code", vscode.Content)
        Assert.True(BrewClient.installed brew.Exec vscode.App)
        Assert.True(vscode.IsChecked)
        Assert.True(neovim.IsChecked)
        Assert.False(discord.IsChecked)
        Assert.Equal(26, Seq.length vm.AllApps)
        Assert.Equal(26, vm.Apps.Count)
        Assert.NotEmpty(vm.SafeTweaks)

        let pathBar = vm.SafeTweaks |> Seq.find (fun row -> row.Id = "finder-path-bar")
        Assert.False(pathBar.IsChecked)

    [<Fact>]
    let EmptyInstallWritesNothing () =
        let brew = BrewClientTests.FakeBrew([ "visual-studio-code" ], [])
        let vm = makeVm brew
        uncheckAll vm

        vm.InstallSelected()
        Assert.Empty(brew.InstallCalls)
        Assert.Equal("Nothing is selected.", vm.StatusText)
        Assert.Empty(vm.SelectedAppIds)

    [<Fact>]
    let SearchFiltersList () =
        let brew = BrewClientTests.FakeBrew([], [])
        let vm = makeVm brew

        vm.SearchText <- "brave"
        Assert.Equal(1, vm.Apps.Count)
        Assert.Equal("brave-browser", Seq.exactlyOne vm.Apps |> fun row -> row.Id)
        Assert.Equal(26, Seq.length vm.AllApps)

        vm.SearchText <- ""
        Assert.Equal(26, vm.Apps.Count)
