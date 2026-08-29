namespace MacUtilGUI.ViewModels

open MacUtilGUI.Models

type AppRowViewModel(app: AppEntry, isChecked: bool) =
    inherit ViewModelBase()

    let mutable isChecked = isChecked

    member _.Id = app.Id
    member _.Content = app.Content
    member _.Description = app.Description
    member _.Category = app.Category
    member _.App = app

    member this.IsChecked
        with get () = isChecked
        and set v =
            if isChecked <> v then
                isChecked <- v
                this.OnPropertyChanged("IsChecked")
