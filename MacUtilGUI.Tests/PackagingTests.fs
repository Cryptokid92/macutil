namespace MacUtilGUI.Tests

open System
open System.IO
open Xunit

module PackagingTests =

    let private repoRoot () =
        let rec walk dir =
            if String.IsNullOrEmpty dir then
                None
            elif
                File.Exists(Path.Combine(dir, "scripts", "make-dmg.sh"))
                || File.Exists(Path.Combine(dir, ".git"))
                || Directory.Exists(Path.Combine(dir, ".git"))
            then
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

    let private read pathParts =
        File.ReadAllText(Path.Combine(Array.append [| repoRoot () |] pathParts))

    [<Fact>]
    let MakeDmgScript () =
        let script = read [| "scripts"; "make-dmg.sh" |]
        Assert.Contains("PublishTrimmed=false", script)
        Assert.Contains("hdiutil", script)
        Assert.Contains("Applications", script)
        Assert.Contains("osx-x64", script)
        Assert.Contains("osx-arm64", script)
        Assert.Contains("lipo", script)
        Assert.DoesNotContain("sudo", script)
        Assert.DoesNotContain("/var/log", script)

    [<Fact>]
    let MacosAppWorkflow () =
        let yml = read [| ".github"; "workflows"; "macos-app.yml" |]
        Assert.DoesNotContain("needs: macos-13", yml)
        Assert.Contains("scripts/make-dmg.sh", yml)

    [<Fact>]
    let ReleaseListsDmg () =
        let yml = read [| ".github"; "workflows"; "Release.yml" |]
        Assert.Contains(".dmg", yml)

    [<Fact>]
    let GitignoreIgnoresDist () =
        let lines =
            File.ReadAllLines(Path.Combine(repoRoot (), ".gitignore"))
            |> Array.map (fun line -> line.Trim())
            |> Array.filter (fun line -> line.Length > 0 && not (line.StartsWith "#"))

        let ignored =
            lines
            |> Array.exists (fun line -> line = "/dist/" || line = "dist/" || line = "/dist" || line = "dist")

        Assert.True(ignored, ".gitignore must ignore repo-root dist/")
