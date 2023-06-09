namespace PySharp

open System
open Microsoft.FSharp.Core
open FParsec

module internal LibraryFunc =
    let error (posFile: Position * string) (s: string) =
        let pos, file = posFile
        $"File {file}\nError in Ln: {pos.Line} Col: {pos.Column}\n{s}"

    let rec toStr value =
        let aux (x: value[]) =
            let rec f i : string =
                if i = x.Length - 1 then
                    toStr x[x.Length - 1]
                else
                    $"{toStr x[i]} , {f (i + 1)}"

            if x.Length = 0 then "" else f 0

        let funcToStr x (y: identifier list) =
            if y.Length = 0 then
                $"func {x} ()"
            else
                let y = List.map MpString y |> List.toArray
                $"func {x} ( {aux y} )"

        let classObject x (y: identifier list) t =
            if y.Length = 0 then
                $"{t} {x}" + "{}"
            else
                let y = List.map MpString y |> List.toArray
                let s1 = $"{t} {x}" + " { "
                let s2 = (aux y) + " }"
                s1 + s2

        match value with
        | MpInt x -> string x
        | MpDouble x -> string x
        | MpBool true -> "true"
        | MpBool false -> "false"
        | MpString x -> x
        | MpChar x -> string x
        | MpNull -> "null"
        | MpFuncStaticValue (x, y, _, _, _) -> funcToStr x y
        | MpFuncSelfValue (x, y, _, _, _, _) -> funcToStr x y
        | MpArrayValue x -> $"[ {aux x} ]"
        | MpTupleValue x -> $"( {aux x} )"
        | MpObjectValue (x, y, _) -> classObject x (List.ofSeq y.Keys) "object"
        | MpClassValue (x, y, _) -> classObject x y "class"
        | MpModuleValue (x, _) -> $"module {x}"


    let toChar posFile value =
        match value with
        | MpInt n -> MpChar(char n)
        | MpChar c -> MpChar c
        | _ -> raise (Exception(error posFile "Cannot convert to char"))

    let printL (value: value) : value =
        match value with
        | MpInt x -> printf $"%d{x}"
        | MpDouble x -> printf $"%f{x}"
        | MpBool x -> printf $"%b{x}"
        | MpString x -> printf $"%s{x}"
        | MpChar x -> printf $"%c{x}"
        | MpNull -> printf "null"
        | _ -> printf $"%s{toStr value}"

        MpNull

    let printLn (value: value) =
        let _ = printL value
        printf "\n"

        MpNull

    let input =
        let s = Console.ReadLine()

        MpString s

    let toInt posFile value =
        try
            match value with
            | MpInt x -> MpInt x
            | MpDouble x -> MpInt(int x)
            | MpBool true -> MpInt 1
            | MpBool false -> MpInt 0
            | MpString x -> MpInt(int x)
            | MpChar x -> MpInt(int x)
            | MpNull -> MpInt 0
            | _ -> raise (Exception())

        with _ ->
            raise (Exception(error posFile "Cannot convert from array to int"))

    let toDouble posFile value =
        try
            match value with
            | MpInt x -> MpDouble(double x)
            | MpDouble x -> MpDouble x
            | MpBool true -> MpDouble 1
            | MpBool false -> MpDouble 0
            | MpString x -> MpDouble(double x)
            | MpChar x -> MpDouble(double x)
            | MpNull -> MpDouble 0
            | _ -> raise (Exception())
        with _ ->
            raise (Exception(error posFile "Cannot convert to double"))

    let toArray value =
        match value with
        | MpArrayValue x -> x
        | MpTupleValue x -> x
        | MpString x -> Array.map MpChar (Array.ofSeq x)
        | _ -> List.toArray [ value ]

    let toType value =
        match value with
        | MpInt _ -> "int"
        | MpDouble _ -> "double"
        | MpBool _ -> "bool"
        | MpString _ -> "string"
        | MpChar _ -> "char"
        | MpNull -> "null"
        | MpFuncStaticValue _ -> "static function"
        | MpFuncSelfValue _ -> "self function"
        | MpArrayValue _ -> "array"
        | MpTupleValue _ -> "tuple"
        | MpObjectValue _ -> "object"
        | MpClassValue _ -> "class"
        | MpModuleValue _ -> "module"

    let size posFile value =
        match value with
        | MpArrayValue x -> MpInt x.Length
        | MpString x -> MpInt x.Length
        | _ -> raise (Exception(error posFile "The object do not have size property"))

    let funcLib0 s =
        match s with
        | "input" -> input
        | _ -> MpNull

    let funcLib1 pos s value =
        match s with
        | "printL" -> printL value
        | "printLn" -> printLn value
        | "int" -> toInt pos value
        | "double" -> toDouble pos value
        | "str" -> MpString(toStr value)
        | "char" -> toChar pos value
        | "array" -> MpArrayValue(toArray value)
        | "tuple" -> MpTupleValue(toArray value)
        | "size" -> size pos value
        | "type" -> MpString(toType value)
        | _ -> MpNull
