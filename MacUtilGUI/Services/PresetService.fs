namespace MacUtilGUI.Services

open System
open System.Text.Json
open System.Text.Json.Nodes
open MacUtilGUI.Models

module PresetService =

    let resolve (catalog: Catalog) (name: string) =
        match Map.tryFind name catalog.Presets with
        | Some ids -> Ok ids
        | None -> Error $"Unknown preset '{name}'"

    let exportIds (ids: string list) =
        JsonSerializer.Serialize(Array.ofList ids)

    let parseImport (catalog: Catalog) (json: string) =
        try
            use doc = JsonDocument.Parse(json)
            let root = doc.RootElement

            if root.ValueKind <> JsonValueKind.Array then
                Error "Import file must be a JSON array of string ids"
            else
                let rec walk (items: JsonElement list) acc =
                    match items with
                    | [] -> Ok(List.rev acc)
                    | item :: _ when item.ValueKind <> JsonValueKind.String ->
                        Error "Import file must be a JSON array of string ids"
                    | item :: rest ->
                        let id = item.GetString()

                        if String.IsNullOrWhiteSpace id then
                            Error "Import file contains an empty id"
                        elif not (Map.containsKey id catalog.Tweaks) then
                            Error $"Unknown tweak id '{id}'"
                        else
                            walk rest (id :: acc)

                walk (root.EnumerateArray() |> Seq.toList) []
        with :? JsonException as ex ->
            Error $"Invalid JSON: {ex.Message}"

    let detectMap (catalog: Catalog) (client: IDefaultsClient) =
        catalog.Tweaks |> Map.map (fun _ tweak -> ActionEngine.detect client tweak)

    let detectJson (catalog: Catalog) (client: IDefaultsClient) =
        let obj = JsonObject()

        for KeyValue(id, applied) in detectMap catalog client do
            obj.[id] <- JsonValue.Create(applied)

        obj.ToJsonString(JsonSerializerOptions(WriteIndented = true))

    let applyIds (catalog: Catalog) (client: IDefaultsClient) (killer: IProcessKiller) (ids: string list) =
        for id in ids do
            ActionEngine.apply client killer catalog.Tweaks.[id]

    let undoIds (catalog: Catalog) (client: IDefaultsClient) (killer: IProcessKiller) (ids: string list) =
        for id in ids do
            ActionEngine.undo client killer catalog.Tweaks.[id]

    let appliedIds (catalog: Catalog) (client: IDefaultsClient) =
        detectMap catalog client
        |> Map.toList
        |> List.choose (fun (id, applied) -> if applied then Some id else None)
