namespace MacUtilGUI.ViewModels

open System.Collections.ObjectModel

type AppGroupViewModel(name: string, rows: AppRowViewModel seq) =
    inherit ViewModelBase()

    let apps = ObservableCollection<AppRowViewModel>(rows)

    member _.Name = name

    member _.Apps = apps

    member this.AllSelected
        with get () = apps.Count > 0 && Seq.forall (fun (row: AppRowViewModel) -> row.IsChecked) apps
        and set v =
            for row in apps do
                row.IsChecked <- v

            this.OnPropertyChanged("AllSelected")
