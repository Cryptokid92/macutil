namespace MacUtilGUI.Tests

open MacUtilGUI.Models
open MacUtilGUI.Services

type FakeDefaultsClient() =
    let mutable store = Map.empty<string * string, PrefValue>
    let mutable writeCount = 0

    member _.WriteCount = writeCount

    member _.Read(domain: string, key: string) =
        Map.tryFind (domain, key) store

    member _.Write(domain: string, key: string, value: PrefValue) =
        writeCount <- writeCount + 1
        store <- Map.add (domain, key) value store

    member _.Delete(domain: string, key: string) =
        store <- Map.remove (domain, key) store

    interface IDefaultsClient with
        member this.Read(domain, key) = this.Read(domain, key)
        member this.Write(domain, key, value) = this.Write(domain, key, value)
        member this.Delete(domain, key) = this.Delete(domain, key)
