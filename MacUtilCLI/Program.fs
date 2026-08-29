namespace MacUtilCLI

open System
open System.IO
open MacUtilGUI.Models
open MacUtilGUI.Services

module Cli =

    let usage =
        "usage: macutil detect | apply --preset <name> | undo --preset <name> | export | import <file>"

    type Command =
        | Detect
        | Apply of string
        | Undo of string
        | Export
        | Import of string

    let parse (argv: string[]) =
        match List.ofArray argv with
        | [ "detect" ] -> Ok Detect
        | [ "apply"; "--preset"; name ] when not (String.IsNullOrWhiteSpace name) -> Ok(Apply name)
        | [ "undo"; "--preset"; name ] when not (String.IsNullOrWhiteSpace name) -> Ok(Undo name)
        | [ "export" ] -> Ok Export
        | [ "import"; path ] when not (String.IsNullOrWhiteSpace path) -> Ok(Import path)
        | _ -> Error usage

    let run
        (catalog: Catalog)
        (client: IDefaultsClient)
        (killer: IProcessKiller)
        (argv: string[])
        (outw: TextWriter)
        (errw: TextWriter)
        =
        match parse argv with
        | Error msg ->
            errw.WriteLine msg
            1
        | Ok Detect ->
            outw.WriteLine(PresetService.detectJson catalog client)
            0
        | Ok(Apply name) ->
            match PresetService.resolve catalog name with
            | Error msg ->
                errw.WriteLine msg
                1
            | Ok ids ->
                PresetService.applyIds catalog client killer ids
                outw.WriteLine(sprintf "Applied %d tweak(s)." ids.Length)
                0
        | Ok(Undo name) ->
            match PresetService.resolve catalog name with
            | Error msg ->
                errw.WriteLine msg
                1
            | Ok ids ->
                PresetService.undoIds catalog client killer ids
                outw.WriteLine(sprintf "Undid %d tweak(s)." ids.Length)
                0
        | Ok Export ->
            outw.WriteLine(PresetService.exportIds (PresetService.appliedIds catalog client))
            0
        | Ok(Import path) ->
            if not (File.Exists path) then
                errw.WriteLine(sprintf "File not found: %s" path)
                1
            else
                match PresetService.parseImport catalog (File.ReadAllText path) with
                | Error msg ->
                    errw.WriteLine msg
                    1
                | Ok ids ->
                    PresetService.applyIds catalog client killer ids
                    outw.WriteLine(sprintf "Imported %d tweak(s)." ids.Length)
                    0

module Program =

    [<EntryPoint>]
    let main argv =
        try
            let dir = Path.Combine(AppContext.BaseDirectory, "config")
            let catalog = ConfigLoader.load dir
            let client = UnixDefaultsClient() :> IDefaultsClient
            let killer = UnixProcessKiller() :> IProcessKiller
            Cli.run catalog client killer argv Console.Out Console.Error
        with
        | :? ConfigLoadException as ex ->
            eprintfn "%s" ex.Message
            1
        | ex ->
            eprintfn "%s" ex.Message
            1
