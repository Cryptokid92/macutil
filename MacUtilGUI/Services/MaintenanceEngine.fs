namespace MacUtilGUI.Services

open System
open System.IO

type MaintenanceId =
    | BrewUpdate
    | BrewCleanup
    | UserCacheBrew

type MaintenanceAction =
    { Id: MaintenanceId
      Key: string
      Content: string
      Description: string }

type CacheClear = string -> Result<string, string>

module MaintenanceEngine =

    let idString =
        function
        | BrewUpdate -> "brew-update"
        | BrewCleanup -> "brew-cleanup"
        | UserCacheBrew -> "user-cache-brew"

    let refusePath (path: string) =
        if String.IsNullOrWhiteSpace path then
            Error "Path is empty."
        else
            try
                let full = Path.GetFullPath(path.Trim())

                let normalized =
                    full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)

                let forbidden =
                    normalized = "/var"
                    || normalized.StartsWith("/var/", StringComparison.Ordinal)
                    || normalized = "/private/var"
                    || normalized.StartsWith("/private/var/", StringComparison.Ordinal)

                if forbidden then
                    Error $"Refused path under /var: {path.Trim()}"
                else
                    Ok normalized
            with ex ->
                Error ex.Message

    let catalog =
        [ { Id = BrewUpdate
            Key = idString BrewUpdate
            Content = "Brew update"
            Description = "Run brew update. Refresh Homebrew formulae." }
          { Id = BrewCleanup
            Key = idString BrewCleanup
            Content = "Brew cleanup"
            Description = "Run brew cleanup. Remove old kegs and stale lock files." }
          { Id = UserCacheBrew
            Key = idString UserCacheBrew
            Content = "Homebrew cache"
            Description = "Delete files in the user Homebrew cache reported by brew --cache." } ]

    let unixClear (path: string) =
        match refusePath path with
        | Error msg -> Error msg
        | Ok root ->
            try
                if Directory.Exists root then
                    for entry in Directory.GetFileSystemEntries root do
                        match refusePath entry with
                        | Error _ -> ()
                        | Ok safe ->
                            let attrs = File.GetAttributes safe
                            let isDir = (attrs &&& FileAttributes.Directory) = FileAttributes.Directory
                            let isLink = (attrs &&& FileAttributes.ReparsePoint) = FileAttributes.ReparsePoint

                            if isDir && not isLink then
                                Directory.Delete(safe, true)
                            else
                                File.Delete safe

                Ok "Cleared Homebrew cache."
            with ex ->
                Error ex.Message

    let private firstLine (text: string) =
        text.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.tryHead
        |> Option.map (fun line -> line.Trim())
        |> Option.defaultValue ""

    let private brewOk (exec: BrewExec) (args: string list) (ok: string) =
        match exec args with
        | 0, _, _ -> Ok ok
        | _, _, stderr ->
            let msg = stderr.Trim()
            Error(if msg = "" then "brew failed." else msg)

    let run (exec: BrewExec) (clear: CacheClear) (id: MaintenanceId) =
        match id with
        | BrewUpdate -> brewOk exec [ "update" ] "Ran brew update."
        | BrewCleanup -> brewOk exec [ "cleanup" ] "Ran brew cleanup."
        | UserCacheBrew ->
            match exec [ "--cache" ] with
            | 0, stdout, _ ->
                match refusePath (firstLine stdout) with
                | Error msg -> Error msg
                | Ok safe -> clear safe
            | _, _, stderr ->
                let msg = stderr.Trim()
                Error(if msg = "" then "brew --cache failed." else msg)
