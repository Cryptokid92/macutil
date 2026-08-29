namespace MacUtilGUI.Services

open System
open System.Diagnostics

type SoftwareUpdateList = unit -> int * string * string

type AppleUpdateKind =
    | Major
    | Recommended

type AppleUpdate =
    { Label: string
      Title: string
      Version: string
      Recommended: bool
      Kind: AppleUpdateKind }

type BrewOutdated = { Name: string; Detail: string }

module UpdateService =

    let systemSettingsHelp =
        "Install major macOS upgrades in System Settings, General, Software Update."

    let systemSettingsCommand =
        "open x-apple.systempreferences:com.apple.Software-Update-Settings.extension"

    let isMajorUpgrade (label: string) (title: string) =
        let blob = label + " " + title
        blob.IndexOf("macOS Tahoe", StringComparison.OrdinalIgnoreCase) >= 0

    let private kindOf label title =
        if isMajorUpgrade label title then
            AppleUpdateKind.Major
        else
            AppleUpdateKind.Recommended

    let private splitFields (line: string) =
        line.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map (fun part -> part.Trim())
        |> Array.choose (fun part ->
            let idx = part.IndexOf(':')

            if idx <= 0 then
                None
            else
                Some(part.Substring(0, idx).Trim(), part.Substring(idx + 1).Trim()))

    let private fieldValue (fields: (string * string) array) (name: string) =
        fields
        |> Array.tryPick (fun (key, value) ->
            if key.Equals(name, StringComparison.OrdinalIgnoreCase) then
                Some value
            else
                None)
        |> Option.defaultValue ""

    let private parseTitleLine (raw: string) =
        let trimmed = raw.Trim()

        if trimmed.StartsWith("Title:", StringComparison.OrdinalIgnoreCase) then
            let fields = splitFields trimmed
            let title = fieldValue fields "Title"
            let version = fieldValue fields "Version"
            let recommended = fieldValue fields "Recommended"
            Some(title, version, recommended.Equals("YES", StringComparison.OrdinalIgnoreCase))
        else
            None

    let parseSoftwareUpdateList (text: string) =
        if isNull text then
            []
        else
            let lines = text.Split([| '\n'; '\r' |], StringSplitOptions.None)

            let acc = ResizeArray<AppleUpdate>()
            let mutable i = 0

            while i < lines.Length do
                let line = lines.[i].Trim()
                let star = line.StartsWith("* ", StringComparison.Ordinal)

                let labelIdx =
                    if star then
                        line.IndexOf("Label:", StringComparison.OrdinalIgnoreCase)
                    else
                        -1

                if labelIdx >= 0 then
                    let label = line.Substring(labelIdx + "Label:".Length).Trim()
                    let mutable title = label
                    let mutable version = ""
                    let mutable recommended = false

                    if i + 1 < lines.Length then
                        match parseTitleLine lines.[i + 1] with
                        | Some(parsedTitle, parsedVersion, parsedRecommended) ->
                            if parsedTitle <> "" then
                                title <- parsedTitle

                            version <- parsedVersion
                            recommended <- parsedRecommended
                            i <- i + 1
                        | None -> ()

                    acc.Add(
                        { Label = label
                          Title = title
                          Version = version
                          Recommended = recommended
                          Kind = kindOf label title }
                    )

                i <- i + 1

            List.ofSeq acc

    let private skipBrewLine (line: string) =
        String.IsNullOrWhiteSpace line
        || line.StartsWith("==>", StringComparison.Ordinal)
        || line.StartsWith("✔", StringComparison.Ordinal)
        || line.StartsWith("✓", StringComparison.Ordinal)
        || line.IndexOf("JSON API", StringComparison.OrdinalIgnoreCase) >= 0
        || line.StartsWith("Downloading", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("No outdated", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("Already up-to-date", StringComparison.OrdinalIgnoreCase)

    let parseBrewOutdated (text: string) =
        if isNull text then
            []
        else
            text.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.choose (fun raw ->
                let line = raw.Trim()

                if skipBrewLine line then
                    None
                else
                    let name =
                        line.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
                        |> Array.tryHead
                        |> Option.defaultValue ""

                    if name = "" then
                        None
                    else
                        Some { Name = name; Detail = line })
            |> Array.toList

    let upgradeBrew (exec: BrewExec) (names: string list) =
        match names with
        | [] -> Error "Nothing is selected."
        | _ ->
            match exec ("upgrade" :: names) with
            | 0, _, _ -> Ok(sprintf "Updated %d Homebrew package(s)." names.Length)
            | _, _, stderr ->
                let msg = stderr.Trim()
                Error(if msg = "" then "brew upgrade failed." else msg)

    let unixList () =
        try
            use proc = new Process()
            proc.StartInfo.FileName <- "/usr/sbin/softwareupdate"
            proc.StartInfo.UseShellExecute <- false
            proc.StartInfo.RedirectStandardOutput <- true
            proc.StartInfo.RedirectStandardError <- true
            proc.StartInfo.CreateNoWindow <- true
            proc.StartInfo.ArgumentList.Add("--list")

            if not (proc.Start()) then
                1, "", "softwareupdate did not start"
            else
                let stdout = proc.StandardOutput.ReadToEnd()
                let stderr = proc.StandardError.ReadToEnd()
                proc.WaitForExit()
                proc.ExitCode, stdout, stderr
        with ex ->
            1, "", ex.Message
