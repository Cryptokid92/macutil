namespace MacUtilGUI.ViewModels

open System.Collections.ObjectModel

type TweakGroupViewModel(name: string, rows: TweakRowViewModel seq) =
    inherit ViewModelBase()

    let tweaks = ObservableCollection<TweakRowViewModel>(rows)

    member _.Name = name

    member _.Tweaks = tweaks

    member this.AllSelected
        with get () =
            tweaks.Count > 0
            && Seq.forall (fun (row: TweakRowViewModel) -> row.IsChecked) tweaks
        and set v =
            for row in tweaks do
                row.IsChecked <- v

            this.OnPropertyChanged("AllSelected")
