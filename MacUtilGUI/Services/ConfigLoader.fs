namespace MacUtilGUI.Services

open System
open System.IO
open System.Text.Json
open MacUtilGUI.Models

type ConfigLoadException(message: string) =
    inherit Exception(message)

module ConfigLoader =

    let private fail msg = raise (ConfigLoadException msg)

    let private parseDoc (json: string) =
        try
            JsonDocument.Parse(json)
        with :? JsonException as ex ->
            fail $"Invalid JSON: {ex.Message}"

    let private ensureKnown (el: JsonElement) (allowed: string array) =
        let allowedSet = Set.ofArray allowed

        for prop in el.EnumerateObject() do
            if not (Set.contains prop.Name allowedSet) then
                fail $"Unknown field '{prop.Name}'"

    let private require (el: JsonElement) (name: string) =
        match el.TryGetProperty(name) with
        | true, prop -> prop
        | false, _ -> fail $"Missing field '{name}'"

    let private requireString (el: JsonElement) (name: string) =
        let prop = require el name

        if prop.ValueKind <> JsonValueKind.String then
            fail $"Field '{name}' must be a string"

        let value = prop.GetString()

        if String.IsNullOrWhiteSpace value then
            fail $"Field '{name}' must not be empty"

        value

    let private optionalString (el: JsonElement) (name: string) =
        match el.TryGetProperty(name) with
        | false, _ -> None
        | true, prop when prop.ValueKind = JsonValueKind.Null -> None
        | true, prop when prop.ValueKind = JsonValueKind.String ->
            let value = prop.GetString()

            if String.IsNullOrWhiteSpace value then None else Some value
        | true, _ -> fail $"Field '{name}' must be a string"

    let parsePrefValue (typ: string) (el: JsonElement) =
        match typ with
        | "bool" ->
            match el.ValueKind with
            | JsonValueKind.True
            | JsonValueKind.False -> PrefValue.Bool(el.GetBoolean())
            | _ -> fail "mixed types: expected bool"
        | "int" ->
            match el.TryGetInt32() with
            | true, n -> PrefValue.Int n
            | false, _ -> fail "mixed types: expected int"
        | "float" ->
            if el.ValueKind = JsonValueKind.Number then
                PrefValue.Float(el.GetDouble())
            else
                fail "mixed types: expected float"
        | "text" ->
            if el.ValueKind = JsonValueKind.String then
                PrefValue.Text(el.GetString())
            else
                fail "mixed types: expected text"
        | other -> fail $"Unknown PrefValue type '{other}'"

    let private parseReload (el: JsonElement) =
        match el.ValueKind with
        | JsonValueKind.Null -> Reload.NoReload
        | JsonValueKind.String ->
            match el.GetString() with
            | ""
            | "none" -> Reload.NoReload
            | "Finder" -> Reload.Finder
            | "Dock" -> Reload.Dock
            | other -> fail $"Unknown reload '{other}'"
        | _ -> fail "Field 'reload' must be a string"

    let private parseRisk (el: JsonElement) =
        if el.ValueKind <> JsonValueKind.String then
            fail "Field 'risk' must be a string"

        match el.GetString() with
        | "Safe" -> Risk.Safe
        | "Caution" -> Risk.Caution
        | other -> fail $"Unknown risk '{other}'"

    let private parseWrite (el: JsonElement) =
        if el.ValueKind <> JsonValueKind.Object then
            fail "Each write must be an object"

        ensureKnown el [| "domain"; "key"; "type"; "apply"; "OriginalValue" |]
        let typ = requireString el "type"
        let apply = parsePrefValue typ (require el "apply")
        let original = parsePrefValue typ (require el "OriginalValue")

        { Domain = requireString el "domain"
          Key = requireString el "key"
          Apply = apply
          OriginalValue = original }

    let private parseTweak (id: string) (el: JsonElement) =
        if String.IsNullOrWhiteSpace id then
            fail "Tweak id must not be empty"

        ensureKnown
            el
            [| "content"
               "description"
               "category"
               "writes"
               "appleDefault"
               "reload"
               "risk" |]

        let writesEl = require el "writes"

        if writesEl.ValueKind <> JsonValueKind.Array then
            fail "Field 'writes' must be an array"

        let writes = writesEl.EnumerateArray() |> Seq.map parseWrite |> Seq.toList

        if writes.IsEmpty then
            fail $"Tweak '{id}' has no writes"

        let firstType = requireString (writesEl.[0]) "type"

        { Id = id
          Content = requireString el "content"
          Description = requireString el "description"
          Category = requireString el "category"
          Writes = writes
          AppleDefault = parsePrefValue firstType (require el "appleDefault")
          Reload = parseReload (require el "reload")
          Risk = parseRisk (require el "risk") }

    let private parseApp (id: string) (el: JsonElement) =
        if String.IsNullOrWhiteSpace id then
            fail "App id must not be empty"

        ensureKnown el [| "content"; "description"; "category"; "cask"; "formula"; "link" |]

        let cask = optionalString el "cask"
        let formula = optionalString el "formula"

        match cask, formula with
        | None, None -> fail $"App '{id}' must have cask or formula"
        | _ -> ()

        { Id = id
          Content = requireString el "content"
          Description = requireString el "description"
          Category = requireString el "category"
          Cask = cask
          Formula = formula
          Link = optionalString el "link" }

    let private parseObjectMap parseEntry (json: string) (label: string) =
        use doc = parseDoc json
        let root = doc.RootElement

        if root.ValueKind <> JsonValueKind.Object then
            fail $"{label} must be an object"

        root.EnumerateObject()
        |> Seq.map (fun prop ->
            if prop.Value.ValueKind <> JsonValueKind.Object then
                fail $"{label} entry '{prop.Name}' must be an object"

            prop.Name, parseEntry prop.Name prop.Value)
        |> Seq.toList
        |> Map.ofList

    let parseTweaks (json: string) =
        parseObjectMap parseTweak json "tweaks.json"

    let parseApps (json: string) =
        parseObjectMap parseApp json "applications.json"

    let parsePresets (json: string) =
        use doc = parseDoc json
        let root = doc.RootElement

        if root.ValueKind <> JsonValueKind.Object then
            fail "preset.json must be an object"

        root.EnumerateObject()
        |> Seq.map (fun prop ->
            if prop.Value.ValueKind <> JsonValueKind.Array then
                fail $"Preset '{prop.Name}' must be an array of string ids"

            let ids =
                prop.Value.EnumerateArray()
                |> Seq.map (fun item ->
                    if item.ValueKind <> JsonValueKind.String then
                        fail $"Preset '{prop.Name}' must be an array of string ids"

                    let id = item.GetString()

                    if String.IsNullOrWhiteSpace id then
                        fail $"Preset '{prop.Name}' contains an empty id"

                    id)
                |> Seq.toList

            prop.Name, ids)
        |> Seq.toList
        |> Map.ofList

    let private validatePresets (tweaks: Map<string, Tweak>) (presets: Map<string, string list>) =
        if not (Map.containsKey "Standard" presets) then
            fail "preset.json must define Standard"

        if not (Map.containsKey "Minimal" presets) then
            fail "preset.json must define Minimal"

        for KeyValue(name, ids) in presets do
            for id in ids do
                match Map.tryFind id tweaks with
                | None -> fail $"Preset '{name}' references unknown tweak '{id}'"
                | Some tweak ->
                    match tweak.Risk with
                    | Risk.Caution -> fail $"Preset '{name}' includes Caution tweak '{id}'"
                    | Risk.Safe when name = "Standard" ->
                        match tweak.Category with
                        | "Finder"
                        | "Dock" -> ()
                        | cat -> fail $"Standard preset includes non Finder/Dock tweak '{id}' ({cat})"
                    | Risk.Safe -> ()

    let load (configDir: string) =
        let read name =
            let path = Path.Combine(configDir, name)

            if not (File.Exists path) then
                fail $"Missing {name}"

            File.ReadAllText path

        let tweaks = parseTweaks (read "tweaks.json")
        let apps = parseApps (read "applications.json")
        let presets = parsePresets (read "preset.json")
        validatePresets tweaks presets

        { Tweaks = tweaks
          Apps = apps
          Presets = presets }
