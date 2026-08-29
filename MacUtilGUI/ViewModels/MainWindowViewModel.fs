namespace MacUtilGUI.ViewModels

open System
open System.IO
open System.Collections.ObjectModel
open System.Windows.Input
open System.Threading.Tasks
open Avalonia.Threading
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

type MainWindowViewModel(catalog: Catalog, client: IDefaultsClient, killer: IProcessKiller) as this =
    inherit ViewModelBase()

    let mutable selectedScript: ScriptInfo option = None
    let mutable scriptOutput: string = ""
    let mutable isScriptRunning: bool = false
    let mutable statusText: string = ""
    let categories = ObservableCollection<ScriptCategory>()
    let safeTweaks = ObservableCollection<TweakRowViewModel>()
    let cautionTweaks = ObservableCollection<TweakRowViewModel>()

    let allRows () = Seq.append safeTweaks cautionTweaks

    let refreshDetect () =
        for row in allRows () do
            row.IsChecked <- ActionEngine.detect client row.Tweak

    let selectedRows () =
        allRows () |> Seq.filter (fun row -> row.IsChecked) |> Seq.toList

    let applySelected () =
        let selected = selectedRows ()

        if selected.IsEmpty then
            statusText <- "Nothing is selected."
            this.OnPropertyChanged("StatusText")
        else
            for row in selected do
                ActionEngine.apply client killer row.Tweak

            refreshDetect ()
            statusText <- sprintf "Applied %d tweak(s)." selected.Length
            this.OnPropertyChanged("StatusText")

    let undoSelected () =
        let selected = selectedRows ()

        if selected.IsEmpty then
            statusText <- "Nothing is selected."
            this.OnPropertyChanged("StatusText")
        else
            for row in selected do
                ActionEngine.undo client killer row.Tweak

            refreshDetect ()
            statusText <- sprintf "Undid %d tweak(s)." selected.Length
            this.OnPropertyChanged("StatusText")

    let applyCommand = RelayCommand(fun _ -> applySelected ())
    let undoCommand = RelayCommand(fun _ -> undoSelected ())

    let selectScriptCommand =
        RelayCommand(fun parameter ->
            match parameter with
            | :? ScriptInfo as script ->
                selectedScript <- Some script
                scriptOutput <- ""
                this.OnPropertyChanged("SelectedScript")
                this.OnPropertyChanged("ScriptOutput")
                this.OnPropertyChanged("CanRunScript")
                this.OnPropertyChanged("SelectedScriptName")
                this.OnPropertyChanged("SelectedScriptDescription")
                this.OnPropertyChanged("SelectedScriptCategory")
                this.OnPropertyChanged("SelectedScriptFile")
            | _ -> ())

    let runScriptCommand =
        RelayCommand(fun _ ->
            match selectedScript with
            | Some script when not isScriptRunning ->
                isScriptRunning <- true
                scriptOutput <- "Starting script...\n"
                this.OnPropertyChanged("ScriptOutput")
                this.OnPropertyChanged("CanRunScript")
                this.OnPropertyChanged("IsScriptRunning")

                let onOutput (line: string) =
                    Dispatcher.UIThread.InvokeAsync(fun () ->
                        scriptOutput <- scriptOutput + line + "\n"
                        this.OnPropertyChanged("ScriptOutput"))
                    |> ignore

                let onError (line: string) =
                    Dispatcher.UIThread.InvokeAsync(fun () ->
                        scriptOutput <- scriptOutput + "[ERROR] " + line + "\n"
                        this.OnPropertyChanged("ScriptOutput"))
                    |> ignore

                let scriptTask = ScriptService.runScript script onOutput onError

                scriptTask.ContinueWith(fun (task: Task<int>) ->
                    Dispatcher.UIThread.InvokeAsync(fun () ->
                        isScriptRunning <- false

                        scriptOutput <-
                            scriptOutput
                            + sprintf "\n=== Script completed with exit code: %d ===" task.Result

                        this.OnPropertyChanged("ScriptOutput")
                        this.OnPropertyChanged("CanRunScript")
                        this.OnPropertyChanged("IsScriptRunning"))
                    |> ignore)
                |> ignore
            | _ -> ())

    let categoryRank category =
        match category with
        | "Finder" -> 0
        | "Dock" -> 1
        | "Keyboard" -> 2
        | "Screenshots" -> 3
        | "Privacy" -> 4
        | _ -> 5

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

        for category in ScriptService.loadAllScripts () do
            let fromSystemSetup =
                category.Scripts
                |> List.exists (fun script -> script.FullPath.StartsWith("system-setup"))

            if not fromSystemSetup then
                categories.Add(category)

    new() =
        let dir = Path.Combine(AppContext.BaseDirectory, "config")

        MainWindowViewModel(
            ConfigLoader.load dir,
            UnixDefaultsClient() :> IDefaultsClient,
            UnixProcessKiller() :> IProcessKiller
        )

    member _.SafeTweaks = safeTweaks

    member _.CautionTweaks = cautionTweaks

    member _.StatusText = statusText

    member _.SelectedIds = selectedRows () |> List.map (fun row -> row.Id)

    member _.ApplySelected() = applySelected ()

    member _.UndoSelected() = undoSelected ()

    member _.ApplyCommand = applyCommand

    member _.UndoCommand = undoCommand

    member _.Categories = categories

    member _.SelectedScript = selectedScript

    member _.ScriptOutput = scriptOutput

    member _.SelectedScriptName =
        match selectedScript with
        | Some script -> script.Name
        | None -> ""

    member _.SelectedScriptDescription =
        match selectedScript with
        | Some script -> script.Description
        | None -> ""

    member _.SelectedScriptCategory =
        match selectedScript with
        | Some script -> script.Category
        | None -> ""

    member _.SelectedScriptFile =
        match selectedScript with
        | Some script -> script.Script
        | None -> ""

    member _.CanRunScript = selectedScript.IsSome && not isScriptRunning

    member _.IsScriptRunning = isScriptRunning

    member _.SelectScriptCommand = selectScriptCommand

    member _.RunScriptCommand = runScriptCommand

    member _.Title = "MacUtil"
