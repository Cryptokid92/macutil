namespace MacUtilGUI.ViewModels

open MacUtilGUI.Services

type AppleUpdateRowViewModel(update: AppleUpdate) =
    inherit ViewModelBase()

    let isMajor = update.Kind = AppleUpdateKind.Major
    let mutable isChecked = false

    member _.Label = update.Label
    member _.Title = update.Title
    member _.Version = update.Version
    member _.Kind = update.Kind
    member _.Content = update.Title
    member _.IsMajor = isMajor
    member _.IsEnabled = not isMajor
    member _.Update = update

    member _.KindLabel =
        match update.Kind with
        | AppleUpdateKind.Major -> "Major"
        | AppleUpdateKind.Recommended -> "Recommended"

    member _.Description =
        match update.Kind with
        | AppleUpdateKind.Major -> "Major. Install from System Settings."
        | AppleUpdateKind.Recommended ->
            if update.Version = "" then
                update.Label
            else
                sprintf "%s. %s" update.Version update.Label

    member this.IsChecked
        with get () = isChecked
        and set v =
            let next = if isMajor then false else v

            if isChecked <> next then
                isChecked <- next
                this.OnPropertyChanged("IsChecked")

type BrewOutdatedRowViewModel(pkg: BrewOutdated) =
    inherit ViewModelBase()

    let mutable isChecked = false

    member _.Name = pkg.Name
    member _.Content = pkg.Name
    member _.Package = pkg

    member _.Description =
        if pkg.Detail = pkg.Name then
            "Outdated Homebrew package."
        else
            pkg.Detail

    member this.IsChecked
        with get () = isChecked
        and set v =
            if isChecked <> v then
                isChecked <- v
                this.OnPropertyChanged("IsChecked")
