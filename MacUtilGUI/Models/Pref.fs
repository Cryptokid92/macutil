namespace MacUtilGUI.Models

type PrefValue =
    | Bool of bool
    | Int of int
    | Float of float
    | Text of string

type Risk =
    | Safe
    | Caution
