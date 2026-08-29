namespace MacUtilGUI.ViewModels

open System
open System.IO
open System.Collections.ObjectModel
open System.Windows.Input
open MacUtilGUI.Models
open MacUtilGUI.Services

type RelayCommand(canExecute: obj -> bool, execute: obj -> unit) =
    let canExecuteChanged = Event<System.EventHandler, System.EventArgs>()

    interface ICommand with
        [<CLIEvent>]
        member _.CanExecuteChanged = canExecuteChanged.Publish

        member _.CanExecute(parameter) = canExecute parameter
        member _.Execute(parameter) = execute parameter

    new(execute: obj -> unit) = RelayCommand((fun _ -> true), execute)

type MainWindowViewModel(catalog: Catalog, client: IDefaultsClient, killer: IProcessKiller, brew: BrewExec) as this =
    inherit ViewModelBase()

    let mutable statusText: string = ""
    let mutable searchText: string = ""
    let safeTweaks = ObservableCollection<TweakRowViewModel>()
    let cautionTweaks = ObservableCollection<TweakRowViewModel>()
    let allApps = ResizeArray<AppRowViewModel>()
    let apps = ObservableCollection<AppRowViewModel>()

    let allRows () = Seq.append safeTweaks cautionTweaks

    let refreshDetect () =
        for row in allRows () do
            row.IsChecked <- ActionEngine.detect client row.Tweak

    let selectedRows () =
        allRows () |> Seq.filter (fun row -> row.IsChecked) |> Seq.toList

    let selectedAppRows () =
        allApps |> Seq.filter (fun row -> row.IsChecked) |> Seq.toList

    let setStatus text =
        statusText <- text
        this.OnPropertyChanged("StatusText")

    let applySelected () =
        let selected = selectedRows ()

        if selected.IsEmpty then
            setStatus "Nothing is selected."
        else
            for row in selected do
                ActionEngine.apply client killer row.Tweak

            refreshDetect ()
            setStatus (sprintf "Applied %d tweak(s)." selected.Length)

    let undoSelected () =
        let selected = selectedRows ()

        if selected.IsEmpty then
            setStatus "Nothing is selected."
        else
            for row in selected do
                ActionEngine.undo client killer row.Tweak

            refreshDetect ()
            setStatus (sprintf "Undid %d tweak(s)." selected.Length)

    let refreshAppDetect () =
        for row in allApps do
            row.IsChecked <- BrewClient.installed brew row.App

    let matches (row: AppRowViewModel) (query: string) =
        if String.IsNullOrWhiteSpace query then
            true
        else
            let hit (value: string) =
                value.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0

            hit row.Content || hit row.Id || hit row.Category || hit row.Description

    let applyFilter () =
        apps.Clear()

        for row in allApps do
            if matches row searchText then
                apps.Add(row)

    let installSelected () =
        let selected = selectedAppRows ()

        if selected.IsEmpty then
            setStatus "Nothing is selected."
        else
            let mutable errors = []

            for row in selected do
                match BrewClient.install brew row.App with
                | Ok() -> ()
                | Error msg -> errors <- msg :: errors

            refreshAppDetect ()

            if errors.IsEmpty then
                setStatus (sprintf "Installed %d app(s)." selected.Length)
            else
                setStatus (String.concat "\n" (List.rev errors))

    let applyCommand = RelayCommand(fun _ -> applySelected ())
    let undoCommand = RelayCommand(fun _ -> undoSelected ())
    let installCommand = RelayCommand(fun _ -> installSelected ())

    let categoryRank category =
        match category with
        | "Finder" -> 0
        | "Dock" -> 1
        | "Keyboard" -> 2
        | "Screenshots" -> 3
        | "Privacy" -> 4
        | _ -> 5

    let appCategoryRank category =
        match category with
        | "Communication Apps" -> 0
        | "Developer Tools" -> 1
        | "Web Browsers" -> 2
        | "Terminal" -> 3
        | "Shell" -> 4
        | "Utilities" -> 5
        | _ -> 6

    do
        catalog.Tweaks
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.sortBy (fun tweak -> categoryRank tweak.Category, tweak.Content)
        |> Seq.iter (fun tweak ->
            let row = TweakRowViewModel(tweak, ActionEngine.detect client tweak)

            match tweak.Risk with
            | Risk.Safe -> safeTweaks.Add(row)
            | Risk.Caution -> cautionTweaks.Add(row))

        catalog.Apps
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.sortBy (fun app -> appCategoryRank app.Category, app.Content)
        |> Seq.iter (fun app ->
            allApps.Add(AppRowViewModel(app, BrewClient.installed brew app)))

        applyFilter ()

    new(catalog, client, killer) =
        MainWindowViewModel(catalog, client, killer, fun _ -> 0, "", "")

    new() =
        let dir = Path.Combine(AppContext.BaseDirectory, "config")

        MainWindowViewModel(
            ConfigLoader.load dir,
            UnixDefaultsClient() :> IDefaultsClient,
            UnixProcessKiller() :> IProcessKiller,
            BrewClient.unixExec
        )

    member _.SafeTweaks = safeTweaks

    member _.CautionTweaks = cautionTweaks

    member _.Apps = apps

    member _.AllApps = allApps :> seq<AppRowViewModel>

    member _.StatusText = statusText

    member this.SearchText
        with get () = searchText
        and set v =
            if searchText <> v then
                searchText <- if isNull v then "" else v
                applyFilter ()
                this.OnPropertyChanged("SearchText")

    member _.SelectedIds = selectedRows () |> List.map (fun row -> row.Id)

    member _.SelectedAppIds = selectedAppRows () |> List.map (fun row -> row.Id)

    member _.ApplySelected() = applySelected ()

    member _.UndoSelected() = undoSelected ()

    member _.InstallSelected() = installSelected ()

    member _.ApplyCommand = applyCommand

    member _.UndoCommand = undoCommand

    member _.InstallCommand = installCommand

    member _.Title = "MacUtil"
