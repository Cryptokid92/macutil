namespace MacUtilGUI.Tests

open Xunit
open MacUtilGUI.Models
open MacUtilGUI.Services

module BrewClientTests =

    type FakeBrew(casks: string list, formulas: string list, ?outdated: string list) =
        let outdated = defaultArg outdated []
        let calls = ResizeArray<string list>()

        member _.Calls = List.ofSeq calls

        member _.InstallCalls =
            calls
            |> Seq.filter (fun args ->
                match args with
                | "install" :: _ -> true
                | _ -> false)
            |> Seq.toList

        member _.UpgradeCalls =
            calls
            |> Seq.filter (fun args ->
                match args with
                | "upgrade" :: _ -> true
                | _ -> false)
            |> Seq.toList

        member _.Exec(args: string list) =
            calls.Add args

            match args with
            | [ "list"; "--cask" ] -> 0, String.concat "\n" casks, ""
            | [ "list"; "--formula" ] -> 0, String.concat "\n" formulas, ""
            | [ "outdated" ]
            | [ "outdated"; "--verbose" ] -> 0, String.concat "\n" outdated, ""
            | "install" :: _ -> 0, "", ""
            | "upgrade" :: _ -> 0, "", ""
            | _ -> 1, "", "unexpected brew args"

    let private vscode =
        { Id = "visual-studio-code"
          Content = "Visual Studio Code"
          Description = "d"
          Category = "Developer Tools"
          Cask = Some "visual-studio-code"
          Formula = None
          Link = None }

    [<Fact>]
    let DetectInstalledCask () =
        let brew = FakeBrew([ "visual-studio-code" ], [])
        Assert.True(BrewClient.installed brew.Exec vscode)
        Assert.Contains([ "list"; "--cask" ], brew.Calls)

        let missing = { vscode with Cask = Some "discord" }
        Assert.False(BrewClient.installed brew.Exec missing)

    [<Fact>]
    let InstallIdempotent () =
        let brew = FakeBrew([ "visual-studio-code" ], [])

        match BrewClient.install brew.Exec vscode with
        | Ok() -> ()
        | Error msg -> Assert.Fail msg

        Assert.Empty(brew.InstallCalls)
