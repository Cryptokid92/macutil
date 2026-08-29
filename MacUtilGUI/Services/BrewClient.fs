namespace MacUtilGUI.Services

open System
open System.Diagnostics
open System.IO
open MacUtilGUI.Models

type BrewExec = string list -> int * string * string

module BrewClient =

    type private Pkg =
        | Cask of string
        | Formula of string

    let private parseList (stdout: string) =
        stdout.Split([| ' '; '\t'; '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
        |> Set.ofArray

    let private listNames (exec: BrewExec) (flag: string) =
        match exec [ "list"; flag ] with
        | 0, stdout, _ -> parseList stdout
        | _ -> Set.empty

    let private pkg (app: AppEntry) =
        match app.Cask, app.Formula with
        | Some cask, _ -> Some(Cask cask)
        | None, Some formula -> Some(Formula formula)
        | None, None -> None

    let installed (exec: BrewExec) (app: AppEntry) =
        match pkg app with
        | Some(Cask name) -> listNames exec "--cask" |> Set.contains name
        | Some(Formula name) -> listNames exec "--formula" |> Set.contains name
        | None -> false

    let install (exec: BrewExec) (app: AppEntry) =
        if installed exec app then
            Ok()
        else
            match pkg app with
            | None -> Error $"App '{app.Id}' has no cask or formula"
            | Some token ->
                let args =
                    match token with
                    | Cask name -> [ "install"; "--cask"; name ]
                    | Formula name -> [ "install"; "--formula"; name ]

                match exec args with
                | 0, _, _ -> Ok()
                | _, _, stderr when
                    stderr.IndexOf("already installed", StringComparison.OrdinalIgnoreCase)
                    >= 0
                    ->
                    Ok()
                | _, _, stderr -> Error(stderr.Trim())

    let uninstall (exec: BrewExec) (app: AppEntry) =
        if not (installed exec app) then
            Ok()
        else
            match pkg app with
            | None -> Error $"App '{app.Id}' has no cask or formula"
            | Some token ->
                let args =
                    match token with
                    | Cask name -> [ "uninstall"; "--cask"; name ]
                    | Formula name -> [ "uninstall"; "--formula"; name ]

                match exec args with
                | 0, _, _ -> Ok()
                | _, _, stderr when
                    stderr.IndexOf("not installed", StringComparison.OrdinalIgnoreCase)
                    >= 0
                    || stderr.IndexOf("No such keg", StringComparison.OrdinalIgnoreCase)
                       >= 0
                    ->
                    Ok()
                | _, _, stderr -> Error(stderr.Trim())

    let private brewPath () =
        [ "/opt/homebrew/bin/brew"; "/usr/local/bin/brew" ]
        |> List.tryFind File.Exists
        |> Option.defaultValue "brew"

    let private runBrew (brew: string) (args: string list) =
        try
            use proc = new Process()
            proc.StartInfo.FileName <- brew
            proc.StartInfo.UseShellExecute <- false
            proc.StartInfo.RedirectStandardOutput <- true
            proc.StartInfo.RedirectStandardError <- true
            proc.StartInfo.CreateNoWindow <- true

            for arg in args do
                proc.StartInfo.ArgumentList.Add(arg)

            if not (proc.Start()) then
                1, "", "brew did not start"
            else
                let stdout = proc.StandardOutput.ReadToEnd()
                let stderr = proc.StandardError.ReadToEnd()
                proc.WaitForExit()
                proc.ExitCode, stdout, stderr
        with ex ->
            1, "", ex.Message

    let private resolvedBrew =
        lazy
            let probe = brewPath ()

            match runBrew probe [ "--prefix" ] with
            | 0, stdout, _ ->
                let candidate = Path.Combine(stdout.Trim(), "bin", "brew")
                if File.Exists candidate then candidate else probe
            | _ -> probe

    let unixExec (args: string list) = runBrew resolvedBrew.Value args
