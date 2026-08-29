namespace MacUtilGUI.Services

open System
open System.Diagnostics
open System.Globalization
open MacUtilGUI.Models

type IDefaultsClient =
    abstract Read: domain: string * key: string -> PrefValue option
    abstract Write: domain: string * key: string * value: PrefValue -> unit
    abstract Delete: domain: string * key: string -> unit

module private DefaultsProc =
    let run (args: string list) =
        use proc = new Process()
        proc.StartInfo.FileName <- "/usr/bin/defaults"
        proc.StartInfo.UseShellExecute <- false
        proc.StartInfo.RedirectStandardOutput <- true
        proc.StartInfo.RedirectStandardError <- true
        proc.StartInfo.CreateNoWindow <- true

        for arg in args do
            proc.StartInfo.ArgumentList.Add(arg)

        if not (proc.Start()) then
            failwith "defaults did not start"

        let stdout = proc.StandardOutput.ReadToEnd()
        let stderr = proc.StandardError.ReadToEnd()
        proc.WaitForExit()
        proc.ExitCode, stdout, stderr

    let parsePref (typeOut: string) (valueOut: string) =
        let kind = typeOut.Trim().ToLowerInvariant()
        let raw = valueOut.Trim()

        if kind.Contains "boolean" then
            match raw with
            | "1"
            | "true"
            | "yes" -> PrefValue.Bool true
            | "0"
            | "false"
            | "no" -> PrefValue.Bool false
            | _ -> failwith $"cannot parse boolean default '{raw}'"
        elif kind.Contains "integer" then
            match Int32.TryParse(raw) with
            | true, n -> PrefValue.Int n
            | false, _ -> failwith $"cannot parse integer default '{raw}'"
        elif kind.Contains "float" || kind.Contains "real" then
            match Double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture) with
            | true, f -> PrefValue.Float f
            | false, _ -> failwith $"cannot parse float default '{raw}'"
        elif kind.Contains "string" then
            PrefValue.Text raw
        else
            failwith $"unsupported defaults type '{typeOut.Trim()}'"

type UnixDefaultsClient() =
    interface IDefaultsClient with
        member _.Read(domain, key) =
            match DefaultsProc.run [ "read-type"; domain; key ] with
            | 0, typeOut, _ ->
                match DefaultsProc.run [ "read"; domain; key ] with
                | 0, valueOut, _ -> Some(DefaultsProc.parsePref typeOut valueOut)
                | _ -> None
            | _ -> None

        member _.Write(domain, key, value) =
            let flag, literal =
                match value with
                | PrefValue.Bool b -> "-bool", (if b then "true" else "false")
                | PrefValue.Int n -> "-int", string n
                | PrefValue.Float f -> "-float", f.ToString(CultureInfo.InvariantCulture)
                | PrefValue.Text s -> "-string", s

            match DefaultsProc.run [ "write"; domain; key; flag; literal ] with
            | 0, _, _ -> ()
            | _, _, err -> failwith $"defaults write failed: {err.Trim()}"

        member _.Delete(domain, key) =
            match DefaultsProc.run [ "delete"; domain; key ] with
            | 0, _, _ -> ()
            | _, _, err ->
                let msg = err.Trim()

                if msg.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0 then
                    ()
                else
                    failwith $"defaults delete failed: {msg}"
