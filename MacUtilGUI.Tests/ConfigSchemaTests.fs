namespace MacUtilGUI.Tests

open System
open System.IO
open System.Text.Json.Nodes
open Xunit
open MacUtilGUI.Models
open MacUtilGUI.Services

module ConfigSchemaTests =

    let findConfigDir () =
        let outputConfig = Path.Combine(AppContext.BaseDirectory, "config")

        if File.Exists(Path.Combine(outputConfig, "tweaks.json")) then
            outputConfig
        else
            let rec walk dir =
                if String.IsNullOrEmpty dir then
                    None
                else
                    let candidate = Path.Combine(dir, "config")

                    if File.Exists(Path.Combine(candidate, "tweaks.json")) then
                        Some candidate
                    else
                        let parent = Directory.GetParent(dir)

                        if isNull parent then None else walk parent.FullName

            match walk AppContext.BaseDirectory with
            | Some dir -> dir
            | None ->
                match walk (Directory.GetCurrentDirectory()) with
                | Some dir -> dir
                | None -> failwith "config/tweaks.json not found"

    let tweakJson typ apply original apple =
        $"""{{"id":{{"content":"c","description":"d","category":"Finder","writes":[{{"domain":"NSGlobalDomain","key":"k","type":"{typ}","apply":{apply},"OriginalValue":{original}}}],"appleDefault":{apple},"reload":"Finder","risk":"Safe"}}}}"""

    [<Fact>]
    let OriginalValueRequired () =
        let node = JsonNode.Parse(tweakJson "bool" "true" "false" "false")
        let writeObj = node.["id"].["writes"].AsArray().[0].AsObject()
        Assert.True(writeObj.Remove("OriginalValue"))

        let thrown =
            try
                ConfigLoader.parseTweaks (node.ToJsonString()) |> ignore
                false
            with :? ConfigLoadException as ex ->
                Assert.Contains("OriginalValue", ex.Message)
                true

        Assert.True(thrown)

        let unknown =
            try
                ConfigLoader.parseTweaks (
                    """{"id":{"content":"c","description":"d","category":"Finder","writes":[{"domain":"NSGlobalDomain","key":"k","type":"bool","apply":true,"OriginalValue":false,"sudo":true}],"appleDefault":false,"reload":"Finder","risk":"Safe"}}"""
                )
                |> ignore

                false
            with :? ConfigLoadException as ex ->
                Assert.Contains("Unknown field", ex.Message)
                Assert.Contains("sudo", ex.Message)
                true

        Assert.True(unknown)

    [<Fact>]
    let LoadSeededTweak () =
        let catalog = ConfigLoader.load (findConfigDir ())
        Assert.True(catalog.Tweaks.ContainsKey "finder-show-extensions")
        Assert.True(catalog.Apps.ContainsKey "visual-studio-code")
        Assert.Contains("finder-show-extensions", catalog.Presets.["Standard"])

        let tweak = catalog.Tweaks.["finder-show-extensions"]
        Assert.Equal("finder-show-extensions", tweak.Id)
        Assert.Equal(Risk.Safe, tweak.Risk)
        Assert.Equal(Reload.Finder, tweak.Reload)
        Assert.Equal(PrefValue.Bool false, tweak.AppleDefault)

        match tweak.Writes with
        | [ write ] ->
            Assert.Equal("NSGlobalDomain", write.Domain)
            Assert.Equal("AppleShowAllExtensions", write.Key)
            Assert.Equal(PrefValue.Bool true, write.Apply)
            Assert.Equal(PrefValue.Bool false, write.OriginalValue)
        | _ -> Assert.Fail("finder-show-extensions must have one write")

    [<Fact>]
    let PrefValueParse () =
        let bools = ConfigLoader.parseTweaks (tweakJson "bool" "true" "false" "false")
        Assert.Equal(PrefValue.Bool true, bools.["id"].Writes.Head.Apply)
        Assert.Equal(PrefValue.Bool false, bools.["id"].Writes.Head.OriginalValue)

        let ints = ConfigLoader.parseTweaks (tweakJson "int" "2" "1" "0")
        Assert.Equal(PrefValue.Int 2, ints.["id"].Writes.Head.Apply)

        let floats = ConfigLoader.parseTweaks (tweakJson "float" "1.5" "0.5" "0.0")
        Assert.Equal(PrefValue.Float 1.5, floats.["id"].Writes.Head.Apply)

        let texts =
            ConfigLoader.parseTweaks (tweakJson "text" "\"Nlsv\"" "\"icnv\"" "\"icnv\"")

        Assert.Equal(PrefValue.Text "Nlsv", texts.["id"].Writes.Head.Apply)

        let mixed =
            try
                ConfigLoader.parseTweaks (tweakJson "bool" "1" "false" "false") |> ignore

                false
            with :? ConfigLoadException as ex ->
                Assert.Contains("mixed types", ex.Message)
                true

        Assert.True(mixed)
