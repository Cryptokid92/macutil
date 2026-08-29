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

type MainWindowViewModel
    (
        catalog: Catalog,
        client: IDefaultsClient,
        killer: IProcessKiller,
        brew: BrewExec,
        cacheClear: CacheClear,
        listApple: SoftwareUpdateList
    ) as this =
    inherit ViewModelBase()

    let mutable statusText: string = ""
    let mutable searchText: string = ""
    let safeTweaks = ObservableCollection<TweakRowViewModel>()
    let cautionTweaks = ObservableCollection<TweakRowViewModel>()
    let allApps = ResizeArray<AppRowViewModel>()
    let apps = ObservableCollection<AppRowViewModel>()
    let appleUpdates = ObservableCollection<AppleUpdateRowViewModel>()
    let brewOutdated = ObservableCollection<BrewOutdatedRowViewModel>()

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

    let selectIds (ids: string list) =
        let wanted = Set.ofList ids

        for row in allRows () do
            row.IsChecked <- wanted.Contains row.Id

    let selectPreset name =
        match PresetService.resolve catalog name with
        | Error msg -> setStatus msg
        | Ok ids ->
            selectIds ids
            setStatus (sprintf "Selected %s." name)

    let exportSelected () =
        let ids = selectedRows () |> List.map (fun row -> row.Id)
        setStatus (sprintf "Exported %d id(s)." ids.Length)
        PresetService.exportIds ids

    let importJson json =
        match PresetService.parseImport catalog json with
        | Error msg ->
            setStatus msg
            false
        | Ok ids ->
            selectIds ids
            setStatus (sprintf "Imported %d id(s)." ids.Length)
            true

    let runMaintenance id =
        match MaintenanceEngine.run brew cacheClear id with
        | Ok msg -> setStatus msg
        | Error msg -> setStatus msg

    let combinedOutput stdout stderr = stdout + "\n" + stderr

    let refreshUpdates report =
        appleUpdates.Clear()
        brewOutdated.Clear()

        let _, stdout, stderr = listApple ()

        for update in UpdateService.parseSoftwareUpdateList (combinedOutput stdout stderr) do
            appleUpdates.Add(AppleUpdateRowViewModel(update))

        let _, brewOut, brewErr = brew [ "outdated"; "--verbose" ]

        for pkg in UpdateService.parseBrewOutdated (combinedOutput brewOut brewErr) do
            brewOutdated.Add(BrewOutdatedRowViewModel(pkg))

        if report then
            setStatus (
                sprintf "Listed %d Apple update(s) and %d Homebrew package(s)." appleUpdates.Count brewOutdated.Count
            )

    let copySystemSettings () =
        setStatus UpdateService.systemSettingsHelp

    let updateBrewSelected () =
        let selected =
            brewOutdated
            |> Seq.filter (fun row -> row.IsChecked)
            |> Seq.map (fun row -> row.Name)
            |> Seq.toList

        if selected.IsEmpty then
            setStatus "Nothing is selected."
        else
            match UpdateService.upgradeBrew brew selected with
            | Ok msg ->
                refreshUpdates false
                setStatus msg
            | Error msg -> setStatus msg

    let applyCommand = RelayCommand(fun _ -> applySelected ())
    let undoCommand = RelayCommand(fun _ -> undoSelected ())
    let installCommand = RelayCommand(fun _ -> installSelected ())
    let selectStandardCommand = RelayCommand(fun _ -> selectPreset "Standard")
    let selectMinimalCommand = RelayCommand(fun _ -> selectPreset "Minimal")
    let brewUpdateCommand = RelayCommand(fun _ -> runMaintenance BrewUpdate)
    let brewCleanupCommand = RelayCommand(fun _ -> runMaintenance BrewCleanup)
    let userCacheBrewCommand = RelayCommand(fun _ -> runMaintenance UserCacheBrew)
    let refreshUpdatesCommand = RelayCommand(fun _ -> refreshUpdates true)
    let copySystemSettingsCommand = RelayCommand(fun _ -> copySystemSettings ())
    let updateHomebrewCommand = RelayCommand(fun _ -> updateBrewSelected ())

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
        |> Seq.iter (fun app -> allApps.Add(AppRowViewModel(app, BrewClient.installed brew app)))

        applyFilter ()
        refreshUpdates false

    new(catalog, client, killer, brew, cacheClear) =
        MainWindowViewModel(catalog, client, killer, brew, cacheClear, fun () -> 0, "", "")

    new(catalog, client, killer, brew) = MainWindowViewModel(catalog, client, killer, brew, MaintenanceEngine.unixClear)

    new(catalog, client, killer) =
        MainWindowViewModel(catalog, client, killer, (fun _ -> 0, "", ""), MaintenanceEngine.unixClear)

    new() =
        let dir = Path.Combine(AppContext.BaseDirectory, "config")

        MainWindowViewModel(
            ConfigLoader.load dir,
            UnixDefaultsClient() :> IDefaultsClient,
            UnixProcessKiller() :> IProcessKiller,
            BrewClient.unixExec,
            MaintenanceEngine.unixClear,
            UpdateService.unixList
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

    member _.MaintenanceActions = MaintenanceEngine.catalog

    member _.InstallSelected() = installSelected ()

    member _.RunBrewUpdate() = runMaintenance BrewUpdate

    member _.RunBrewCleanup() = runMaintenance BrewCleanup

    member _.RunUserCacheBrew() = runMaintenance UserCacheBrew

    member _.SelectStandard() = selectPreset "Standard"

    member _.SelectMinimal() = selectPreset "Minimal"

    member _.ExportSelected() = exportSelected ()

    member _.ImportJson(json: string) = importJson json

    member _.SetStatus(text: string) = setStatus text

    member _.ApplyCommand = applyCommand

    member _.UndoCommand = undoCommand

    member _.InstallCommand = installCommand

    member _.SelectStandardCommand = selectStandardCommand

    member _.SelectMinimalCommand = selectMinimalCommand

    member _.BrewUpdateCommand = brewUpdateCommand

    member _.BrewCleanupCommand = brewCleanupCommand

    member _.UserCacheBrewCommand = userCacheBrewCommand

    member _.RefreshUpdatesCommand = refreshUpdatesCommand

    member _.CopySystemSettingsCommand = copySystemSettingsCommand

    member _.UpdateHomebrewCommand = updateHomebrewCommand

    member _.AppleUpdates = appleUpdates

    member _.BrewOutdated = brewOutdated

    member _.SystemSettingsHelp = UpdateService.systemSettingsHelp

    member _.SystemSettingsCommand = UpdateService.systemSettingsCommand

    member _.RefreshUpdates() = refreshUpdates true

    member _.CopySystemSettings() = copySystemSettings ()

    member _.UpdateHomebrew() = updateBrewSelected ()

    member _.Title = "MacUtil"
