namespace MacUtilGUI.Views

open System.IO
open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Markup.Xaml
open Avalonia.Interactivity
open Avalonia.Platform.Storage
open MacUtilGUI.ViewModels

type MainWindow() as this =
    inherit Window()

    do this.InitializeComponent()

    member private this.InitializeComponent() = AvaloniaXamlLoader.Load(this)

    member private this.OnCloseButtonClick(_: obj, _: RoutedEventArgs) = this.Close()

    member private this.OnMinimizeButtonClick(_: obj, _: RoutedEventArgs) =
        this.WindowState <- WindowState.Minimized

    member private this.OnMaximizeButtonClick(_: obj, _: RoutedEventArgs) =
        this.WindowState <-
            if this.WindowState = WindowState.FullScreen then
                WindowState.Normal
            else
                WindowState.FullScreen

    member private this.TitleBar_PointerPressed(_: obj, e: PointerPressedEventArgs) = this.BeginMoveDrag(e)

    member private this.JsonFileType() =
        let jsonType = FilePickerFileType("JSON")
        jsonType.Patterns <- [| "*.json" |]
        jsonType

    member private this.ExportPreset(vm: MainWindowViewModel) =
        task {
            try
                match TopLevel.GetTopLevel(this) with
                | null -> ()
                | top ->
                    let! file =
                        top.StorageProvider.SaveFilePickerAsync(
                            FilePickerSaveOptions(
                                Title = "Export",
                                SuggestedFileName = "preset.json",
                                DefaultExtension = "json",
                                FileTypeChoices = [| this.JsonFileType() |]
                            )
                        )

                    if not (isNull file) then
                        File.WriteAllText(file.Path.LocalPath, vm.ExportSelected())
            with ex ->
                vm.SetStatus(Tweaks, ex.Message)
        }

    member private this.ImportPreset(vm: MainWindowViewModel) =
        task {
            try
                match TopLevel.GetTopLevel(this) with
                | null -> ()
                | top ->
                    let! files =
                        top.StorageProvider.OpenFilePickerAsync(
                            FilePickerOpenOptions(
                                Title = "Import",
                                AllowMultiple = false,
                                FileTypeFilter = [| this.JsonFileType() |]
                            )
                        )

                    if not (isNull files) && files.Count > 0 then
                        let json = File.ReadAllText(files.[0].Path.LocalPath)
                        vm.ImportJson json |> ignore
            with ex ->
                vm.SetStatus(Tweaks, ex.Message)
        }

    member private this.OnExportClick(_: obj, _: RoutedEventArgs) =
        match this.DataContext with
        | :? MainWindowViewModel as vm -> this.ExportPreset vm |> ignore
        | _ -> ()

    member private this.OnImportClick(_: obj, _: RoutedEventArgs) =
        match this.DataContext with
        | :? MainWindowViewModel as vm -> this.ImportPreset vm |> ignore
        | _ -> ()

    member private this.OnCopyUpdateClick(_: obj, _: RoutedEventArgs) =
        match this.DataContext with
        | :? MainWindowViewModel as vm ->
            vm.CopySystemSettings()

            match TopLevel.GetTopLevel(this) with
            | null -> ()
            | top when isNull top.Clipboard -> ()
            | top -> top.Clipboard.SetTextAsync(vm.SystemSettingsCommand) |> ignore
        | _ -> ()
