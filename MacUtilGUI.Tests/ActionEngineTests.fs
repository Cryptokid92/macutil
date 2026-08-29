namespace MacUtilGUI.Tests

open Xunit
open MacUtilGUI.Models
open MacUtilGUI.Services

type FakeProcessKiller() =
    let killed = ResizeArray<string>()

    member _.Killed = List.ofSeq killed

    interface IProcessKiller with
        member _.KillProcess(name) = killed.Add name

module ActionEngineTests =

    let private tweak apply apple original =
        { Id = "test-tweak"
          Content = "c"
          Description = "d"
          Category = "Finder"
          Writes =
            [ { Domain = "NSGlobalDomain"
                Key = "k"
                Apply = apply
                OriginalValue = original } ]
          AppleDefault = apple
          Reload = Reload.Finder
          Risk = Risk.Safe }

    [<Fact>]
    let DetectMissingKey () =
        let client = FakeDefaultsClient()
        let t = tweak (PrefValue.Bool true) (PrefValue.Bool false) (PrefValue.Bool false)
        Assert.False(ActionEngine.detect client t)

    [<Fact>]
    let ApplyThenDetect () =
        let client = FakeDefaultsClient()
        let killer = FakeProcessKiller()
        let t = tweak (PrefValue.Bool true) (PrefValue.Bool false) (PrefValue.Bool false)
        ActionEngine.apply client killer t
        Assert.True(ActionEngine.detect client t)
        Assert.Equal(PrefValue.Bool true, client.Read("NSGlobalDomain", "k").Value)
        Assert.Equal<string list>([ "Finder" ], killer.Killed)

    [<Fact>]
    let UndoDeletesKey () =
        let client = FakeDefaultsClient()
        let killer = FakeProcessKiller()
        let t = tweak (PrefValue.Bool true) (PrefValue.Bool false) (PrefValue.Bool false)
        ActionEngine.apply client killer t
        ActionEngine.undo client killer t
        Assert.True(client.Read("NSGlobalDomain", "k").IsNone)
        Assert.False(ActionEngine.detect client t)

    [<Fact>]
    let ApplyTwiceNoSecondWrite () =
        let client = FakeDefaultsClient()
        let killer = FakeProcessKiller()
        let t = tweak (PrefValue.Bool true) (PrefValue.Bool false) (PrefValue.Bool false)
        ActionEngine.apply client killer t
        Assert.Equal(1, client.WriteCount)
        ActionEngine.apply client killer t
        Assert.Equal(1, client.WriteCount)
        Assert.Equal<string list>([ "Finder" ], killer.Killed)
