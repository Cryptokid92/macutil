namespace MacUtilGUI.Tests

open System
open System.Diagnostics
open System.IO
open Xunit
open MacUtilGUI.Services
open MacUtilGUI.ViewModels

module UpdateServiceTests =

    let private tahoeList =
        String.concat
            "\n"
            [ "Software Update Tool"
              ""
              "Finding available software"
              "Software Update found the following new or updated software:"
              "* Label: Safari26.6.1SequoiaAuto-26.6.1"
              "\tTitle: Safari, Version: 26.6.1, Size: 225311KiB, Recommended: YES, "
              "* Label: macOS Tahoe 26.6.2-25G83"
              "\tTitle: macOS Tahoe 26.6.2, Version: 26.6.2, Size: 5885236KiB, Recommended: YES, Action: restart, " ]

    let private catalog () =
        ConfigLoader.load (ConfigSchemaTests.findConfigDir ())

    let private listApple () = 0, tahoeList, ""

    let private makeVm (brew: BrewClientTests.FakeBrew) =
        let client = FakeDefaultsClient()
        let killer = FakeProcessKiller()

        MainWindowViewModel(
            catalog (),
            client :> IDefaultsClient,
            killer :> IProcessKiller,
            brew.Exec,
            MaintenanceEngine.unixClear,
            listApple
        )

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

    let private textExts =
        set
            [ ".fs"
              ".fsproj"
              ".axaml"
              ".md"
              ".json"
              ".sh"
              ".yml"
              ".yaml"
              ".toml"
              ".plist"
              ".txt"
              ".xml" ]

    let private skipDir name =
        name = "bin" || name = "obj" || name = ".git" || name = "node_modules"

    let rec private textFiles dir =
        seq {
            for file in Directory.GetFiles dir do
                let ext = Path.GetExtension(file).ToLowerInvariant()

                if textExts.Contains ext then
                    yield file

            for sub in Directory.GetDirectories dir do
                let name = Path.GetFileName sub

                if not (skipDir name) then
                    yield! textFiles sub
        }

    [<Fact>]
    let ParseMajorUpgrade () =
        let rows = UpdateService.parseSoftwareUpdateList tahoeList
        Assert.Equal(2, rows.Length)

        let safari = rows |> List.find (fun row -> row.Title = "Safari")
        Assert.Equal(AppleUpdateKind.Recommended, safari.Kind)
        Assert.Equal("Safari26.6.1SequoiaAuto-26.6.1", safari.Label)
        Assert.Equal("26.6.1", safari.Version)
        Assert.True(safari.Recommended)

        let tahoe =
            rows
            |> List.find (fun row -> row.Title.IndexOf("macOS Tahoe", StringComparison.OrdinalIgnoreCase) >= 0)

        Assert.Equal(AppleUpdateKind.Major, tahoe.Kind)
        Assert.Contains("macOS Tahoe", tahoe.Label)
        Assert.True(UpdateService.isMajorUpgrade tahoe.Label tahoe.Title)

        let sw = Stopwatch.StartNew()

        for _ in 1..100 do
            UpdateService.parseSoftwareUpdateList tahoeList |> ignore

        sw.Stop()
        Assert.True(sw.ElapsedMilliseconds < 200L, sprintf "100 parses took %d ms" sw.ElapsedMilliseconds)

        let brew = BrewClientTests.FakeBrew([], [], [ "wget (1.21.4) < 1.25.3" ])
        let vm = makeVm brew
        Assert.Equal("MacUtil", vm.Title)
        Assert.Equal(2, vm.AppleUpdates.Count)
        Assert.Equal(1, vm.BrewOutdated.Count)
        Assert.Contains("System Settings", vm.SystemSettingsHelp)

        let tahoeRow =
            vm.AppleUpdates
            |> Seq.find (fun row -> row.Content.IndexOf("macOS Tahoe", StringComparison.OrdinalIgnoreCase) >= 0)

        Assert.Equal("Major", tahoeRow.KindLabel)
        Assert.True(tahoeRow.IsMajor)
        Assert.False(tahoeRow.IsEnabled)
        Assert.False(tahoeRow.IsChecked)
        tahoeRow.IsChecked <- true
        Assert.False(tahoeRow.IsChecked)

        let safariRow = vm.AppleUpdates |> Seq.find (fun row -> row.Content = "Safari")
        Assert.Equal("Recommended", safariRow.KindLabel)
        Assert.False(safariRow.IsMajor)
        Assert.True(safariRow.IsEnabled)
        Assert.False(safariRow.IsChecked)

        let wget = Seq.exactlyOne vm.BrewOutdated
        Assert.Equal("wget", wget.Name)
        Assert.False(wget.IsChecked)
        Assert.NotEmpty(vm.SafeTweaks)
        Assert.Equal(26, Seq.length vm.AllApps)

    [<Fact>]
    let NoSilentInstall () =
        let needle = String.concat "" [ "softwareupdate"; " --"; "install" ]
        let root = repoRoot ()

        let hits =
            textFiles root
            |> Seq.choose (fun path ->
                let text = File.ReadAllText path

                if text.IndexOf(needle, StringComparison.Ordinal) >= 0 then
                    Some(Path.GetRelativePath(root, path))
                else
                    None)
            |> Seq.toList

        Assert.True(hits.IsEmpty, String.concat ", " hits)

        let brew = BrewClientTests.FakeBrew([], [], [ "wget (1.21.4) < 1.25.3" ])
        let vm = makeVm brew
        Assert.Empty(brew.UpgradeCalls)

        vm.UpdateHomebrew()
        Assert.Empty(brew.UpgradeCalls)
        Assert.Equal("Nothing is selected.", vm.StatusText)

        let wget = Seq.exactlyOne vm.BrewOutdated
        wget.IsChecked <- true
        vm.UpdateHomebrew()
        Assert.Equal<string list>([ [ "upgrade"; "wget" ] ], brew.UpgradeCalls)
        Assert.Equal("Updated 1 Homebrew package(s).", vm.StatusText)

        let axaml =
            File.ReadAllText(Path.Combine(root, "MacUtilGUI", "Views", "MainWindow.axaml"))

        Assert.Contains("Updates", axaml)
        Assert.Contains("Homebrew", axaml)
        Assert.Contains("Copy command", axaml)
        Assert.DoesNotContain("Install all", axaml, StringComparison.OrdinalIgnoreCase)
        Assert.DoesNotContain("Install All", axaml)
        Assert.Contains("Tweaks", axaml)
        Assert.Contains("Install", axaml)
        Assert.Contains("Maintenance", axaml)
        Assert.Contains("Standard", axaml)
        Assert.Contains("Minimal", axaml)
        Assert.Contains("#1C1C1E", axaml)
