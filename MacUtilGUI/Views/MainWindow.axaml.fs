namespace MacUtilGUI.Views

open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Markup.Xaml
open Avalonia.Interactivity

type MainWindow() as this =
    inherit Window()

    do this.InitializeComponent()

    member private this.InitializeComponent() = AvaloniaXamlLoader.Load(this)

    member private this.OnCloseButtonClick(sender: obj, e: RoutedEventArgs) =
        this.Close()

    member private this.OnMinimizeButtonClick(sender: obj, e: RoutedEventArgs) =
        this.WindowState <- WindowState.Minimized

    member private this.OnMaximizeButtonClick(sender: obj, e: RoutedEventArgs) =
        this.WindowState <-
            if this.WindowState = WindowState.FullScreen then
                WindowState.Normal
            else
                WindowState.FullScreen

    member private this.TitleBar_PointerPressed(sender: obj, e: PointerPressedEventArgs) =
        this.BeginMoveDrag(e)
