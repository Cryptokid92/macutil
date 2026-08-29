namespace MacUtilGUI.Services

open MacUtilGUI.Models

type IProcessKiller =
    abstract KillProcess: name: string -> unit

module ActionEngine =

    let private isWriteApplied (client: IDefaultsClient) (tweak: Tweak) (write: PrefWrite) =
        match client.Read(write.Domain, write.Key) with
        | None -> tweak.AppleDefault = write.Apply
        | Some live -> live = write.Apply

    let private isAtOriginal (client: IDefaultsClient) (tweak: Tweak) (write: PrefWrite) =
        match client.Read(write.Domain, write.Key) with
        | None -> write.OriginalValue = tweak.AppleDefault
        | Some live -> live = write.OriginalValue

    let private reload (killer: IProcessKiller) (tweak: Tweak) =
        match tweak.Reload with
        | Finder -> killer.KillProcess "Finder"
        | Dock -> killer.KillProcess "Dock"
        | NoReload -> ()

    let private restoreWrite (client: IDefaultsClient) (tweak: Tweak) (write: PrefWrite) =
        if write.OriginalValue = tweak.AppleDefault then
            client.Delete(write.Domain, write.Key)
        else
            client.Write(write.Domain, write.Key, write.OriginalValue)

    let detect (client: #IDefaultsClient) (tweak: Tweak) =
        tweak.Writes |> List.forall (isWriteApplied client tweak)

    let apply (client: #IDefaultsClient) (killer: #IProcessKiller) (tweak: Tweak) =
        let toWrite =
            tweak.Writes
            |> List.filter (fun write -> not (isWriteApplied client tweak write))

        for write in toWrite do
            client.Write(write.Domain, write.Key, write.Apply)

        if not toWrite.IsEmpty then
            reload killer tweak

    let undo (client: #IDefaultsClient) (killer: #IProcessKiller) (tweak: Tweak) =
        let toRestore =
            tweak.Writes
            |> List.filter (fun write -> not (isAtOriginal client tweak write))

        for write in toRestore do
            restoreWrite client tweak write

        if not toRestore.IsEmpty then
            reload killer tweak
