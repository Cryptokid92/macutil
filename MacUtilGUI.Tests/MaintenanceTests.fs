namespace MacUtilGUI.Tests

open System
open System.IO
open Xunit
open MacUtilGUI.Services
open MacUtilGUI.ViewModels

module MaintenanceTests =

    type RecordingBrew(cachePath: string) =
        let calls = ResizeArray<string list>()

        member _.CleanupCalls =
            calls |> Seq.filter (fun args -> args = [ "cleanup" ]) |> Seq.toList

        member _.Exec(args: string list) =
            calls.Add args

            match args with
            | [ "update" ] -> 0, "Already up-to-date.", ""
            | [ "cleanup" ] -> 0, "", ""
            | [ "--cache" ] -> 0, cachePath, ""
            | _ -> 1, "", "unexpected brew args"

    let private catalog () =
        ConfigLoader.load (ConfigSchemaTests.findConfigDir ())

    let private noClear _ = Error "cache clearer must not run"

    let private makeVm (brew: RecordingBrew) =
        let client = FakeDefaultsClient()
        let killer = FakeProcessKiller()

        MainWindowViewModel(catalog (), client :> IDefaultsClient, killer :> IProcessKiller, brew.Exec, noClear)

    [<Fact>]
    let RefuseVarLog () =
        match MaintenanceEngine.refusePath "/var/log" with
        | Error msg -> Assert.Contains("/var", msg)
        | Ok path -> Assert.Fail($"accepted {path}")

        match MaintenanceEngine.refusePath "/private/var/log" with
        | Error msg -> Assert.Contains("/var", msg)
        | Ok path -> Assert.Fail($"accepted {path}")

        match MaintenanceEngine.unixClear "/var/log" with
        | Error msg -> Assert.Contains("/var", msg)
        | Ok _ -> Assert.Fail "cleared /var/log"

        let brew = RecordingBrew("/var/log")
        let mutable cleared = []

        let clear path =
            cleared <- path :: cleared
            Ok "cleared"

        match MaintenanceEngine.run brew.Exec clear UserCacheBrew with
        | Error msg ->
            Assert.Contains("/var", msg)
            Assert.Empty(cleared)
        | Ok _ -> Assert.Fail "cleared a /var/log cache"

    [<Fact>]
    let CleanupIdempotent () =
        let brew = RecordingBrew("/tmp/macutil-not-used")

        match MaintenanceEngine.run brew.Exec noClear BrewCleanup with
        | Ok msg -> Assert.Equal("Ran brew cleanup.", msg)
        | Error msg -> Assert.Fail msg

        match MaintenanceEngine.run brew.Exec noClear BrewCleanup with
        | Ok msg -> Assert.Equal("Ran brew cleanup.", msg)
        | Error msg -> Assert.Fail msg

        Assert.Equal(2, brew.CleanupCalls.Length)
        Assert.Equal<string list>([ "cleanup" ], brew.CleanupCalls.[0])
        Assert.Equal<string list>([ "cleanup" ], brew.CleanupCalls.[1])

    [<Fact>]
    let MaintenanceActionsAreBrewOnly () =
        let keys = MaintenanceEngine.catalog |> List.map (fun action -> action.Key)

        Assert.Equal<string list>([ "brew-update"; "brew-cleanup"; "user-cache-brew" ], keys)

        for action in MaintenanceEngine.catalog do
            Assert.DoesNotContain("/var/log", action.Content)
            Assert.DoesNotContain("/var/log", action.Description)

        let brew = RecordingBrew("/tmp/macutil-not-used")
        let vm = makeVm brew
        vm.RunBrewCleanup()
        Assert.Equal("Ran brew cleanup.", vm.StatusText)
        Assert.Equal(1, brew.CleanupCalls.Length)
        Assert.NotEmpty(vm.SafeTweaks)
        Assert.Equal(26, Seq.length vm.AllApps)
        Assert.Equal("MacUtil", vm.Title)

        let cleanup =
            vm.MaintenanceActions |> List.find (fun action -> action.Key = "brew-cleanup")

        Assert.Contains("brew cleanup", cleanup.Description)

    [<Fact>]
    let UserCacheBrewClearsUserDir () =
        let dir =
            Path.Combine(Path.GetFullPath "/tmp", "macutil-cache-" + Guid.NewGuid().ToString("n"))

        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "stale"), "x")
        let brew = RecordingBrew(dir)

        try
            match MaintenanceEngine.run brew.Exec MaintenanceEngine.unixClear UserCacheBrew with
            | Ok msg ->
                Assert.Equal("Cleared Homebrew cache.", msg)
                Assert.Empty(Directory.GetFileSystemEntries dir)
            | Error msg -> Assert.Fail msg
        finally
            if Directory.Exists dir then
                Directory.Delete(dir, true)
