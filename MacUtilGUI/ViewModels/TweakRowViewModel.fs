namespace MacUtilGUI.ViewModels

open MacUtilGUI.Models

type TweakRowViewModel(tweak: Tweak, isChecked: bool) =
    inherit ViewModelBase()

    let mutable isChecked = isChecked

    member _.Id = tweak.Id
    member _.Content = tweak.Content
    member _.Description = tweak.Description
    member _.Tweak = tweak

    member this.IsChecked
        with get () = isChecked
        and set v =
            if isChecked <> v then
                isChecked <- v
                this.OnPropertyChanged("IsChecked")
