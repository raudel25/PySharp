﻿namespace PySharp

open FParsec
open System

module internal Parser =

    let reservedWords =
        [ "for"
          "while"
          "if"
          "else"
          "elif"
          "func"
          "true"
          "false"
          "return"
          "func"
          "break"
          "class"
          "self"
          "impl"
          "of"
          "in"
          "continue"
          "loop"
          "and"
          "or"
          "xor"
          "module"
          "import" ]

    let reservedFunctions0 = [ "input" ]

    let reservedFunctions1 =
        [ "printLn"
          "printL"
          "int"
          "double"
          "str"
          "char"
          "array"
          "tuple"
          "size"
          "type" ]

    let (>>%) p x = p |>> (fun _ -> x)

    let mpPosition mp =
        getPosition .>>. mp |>> (fun (x, y) -> (y, x))

    let mpNull: Parser<expr, unit> =
        pstring "null" >>% MpNull <?> "null" |>> MpLiteral |> mpPosition

    let mpBool: Parser<expr, unit> =
        let mpTrue = pstring "true" >>% MpBool true
        let mpFalse = pstring "false" >>% MpBool false

        mpTrue <|> mpFalse <?> "bool" |>> MpLiteral |> mpPosition

    let mpUnEscapedChar: Parser<char, unit> =
        satisfy (fun ch -> ch <> '\\' && ch <> '\"')

    let mpEscapedChar =
        [ ("\\\"", '\"')
          ("\\\\", '\\')
          ("\\/", '/')
          ("\\b", '\b')
          ("\\f", '\f')
          ("\\n", '\n')
          ("\\r", '\r')
          ("\\t", '\t') ]

        |> List.map (fun (toMatch, result) -> pstring toMatch >>% result)
        |> choice
        <?> "escaped char"

    let mpUnicodeChar =
        let backslash = pchar '\\'
        let uChar = pchar 'u'
        let hexDigit = anyOf ([ '0' .. '9' ] @ [ 'A' .. 'F' ] @ [ 'a' .. 'f' ])
        let fourHexDigits = hexDigit .>>. hexDigit .>>. hexDigit .>>. hexDigit

        let convertToChar (((h1, h2), h3), h4) =
            let str = $"%c{h1}%c{h2}%c{h3}%c{h4}"
            Int32.Parse(str, Globalization.NumberStyles.HexNumber) |> char

        backslash >>. uChar >>. fourHexDigits |>> convertToChar

    let quotedString =
        let quote = pchar '\"' <?> "quote"
        let jChar = mpUnEscapedChar <|> mpEscapedChar <|> mpUnicodeChar

        quote >>. manyChars jChar .>> quote

    let mpString: Parser<expr, unit> =
        quotedString <?> "quoted string" |>> MpString |>> MpLiteral |> mpPosition

    let quotedChar =
        let quote = pchar '\'' <?> "quote"
        let jChar = mpUnEscapedChar <|> mpEscapedChar <|> mpUnicodeChar

        quote >>. jChar .>> quote

    let mpChar: Parser<expr, unit> =
        quotedChar <?> "quoted char" |>> MpChar |>> MpLiteral |> mpPosition


    let mpNum: Parser<expr, unit> =
        let numberFormat = NumberLiteralOptions.AllowFraction

        numberLiteral numberFormat "number"
        |>> fun nl ->
                if nl.IsInteger then
                    MpLiteral(MpInt(int nl.String))
                else
                    MpLiteral(MpDouble(double nl.String))
        |> mpPosition

    let ws: Parser<unit, unit> =
        skipManySatisfy (fun c -> c = ' ' || c = '\t' || c = '\r')

    let wsl: Parser<unit, unit> =
        skipManySatisfy (fun c -> c = '\n' || c = ' ' || c = '\t' || c = '\r')

    let str_ws s = pstring s .>> ws
    let str_ws1 s = pstring s .>> spaces1
    let str_wsl s = pstring s .>> wsl
    let str_wsl1 s = pstring s .>> spaces1

    let mpIdentifier: Parser<string, unit> =
        let isIdentifierFirstChar c = isLetter c || c = '_'
        let isIdentifierChar c = isLetter c || isDigit c || c = '_'

        let reservedWord =
            choice (reservedWords @ reservedFunctions0 @ reservedFunctions1 |> List.map pstring)

        notFollowedBy reservedWord
        .>>. many1Satisfy2L isIdentifierFirstChar isIdentifierChar "identifier"
        |>> snd


    let mpIdentifier_ws = mpIdentifier .>> ws

    let mpVar: Parser<expr, unit> = mpIdentifier <?> "variable" |>> MpVar |> mpPosition

    let mpArray, mpArrayR = createParserForwardedToRef ()

    let mpTuple, mpTupleR = createParserForwardedToRef ()

    let mpClassConst, mpClassConstR = createParserForwardedToRef ()

    let mpIdentProp, mpIdentPropR = createParserForwardedToRef ()

    let mpInvoke, mpInvokeR = createParserForwardedToRef ()

    let mpReservedFunc, mpReservedFuncR = createParserForwardedToRef ()

    let mpSlice, mpSliceR = createParserForwardedToRef ()

    let mpTernary, mpTernaryR = createParserForwardedToRef ()

    let mpBlock, mpBlockR = createParserForwardedToRef ()

    let mpLambda, mpLambdaR = createParserForwardedToRef ()

    let mpValue =
        [ mpInvoke
          mpReservedFunc
          mpSlice
          mpIdentProp
          mpNum
          mpString
          mpNull
          mpVar
          mpBool
          mpChar
          mpArray
          mpTuple ]
        |> List.map attempt
        |> choice

    type Assoc = Associativity

    let oppA = OperatorPrecedenceParser<expr, unit, unit>()

    let rec mpArithmetic = oppA.ExpressionParser

    let termA = (mpValue .>> ws) <|> between (str_ws "(") (str_ws ")") mpArithmetic

    oppA.TermParser <- termA
    oppA.AddOperator(InfixOperator("+", ws, 1, Assoc.Left, (fun (x, p) y -> MpArithmetic((x, p), MpAdd, y), p)))
    oppA.AddOperator(InfixOperator("-", ws, 1, Assoc.Left, (fun (x, p) y -> MpArithmetic((x, p), MpSubtract, y), p)))
    oppA.AddOperator(InfixOperator("*", ws, 2, Assoc.Left, (fun (x, p) y -> MpArithmetic((x, p), MpMultiply, y), p)))
    oppA.AddOperator(InfixOperator("/", ws, 2, Assoc.Left, (fun (x, p) y -> MpArithmetic((x, p), MpDivide, y), p)))
    oppA.AddOperator(PrefixOperator("-", ws, 2, true, (fun (x, p) -> MpNeg(x, p), p)))
    oppA.AddOperator(InfixOperator("%", ws, 2, Assoc.Left, (fun (x, p) y -> MpArithmetic((x, p), MpRest, y), p)))

    oppA.AddOperator(
        InfixOperator("&", ws, 3, Assoc.Left, (fun (x, p) y -> MpArithmetic((x, p), MpArithmeticAnd, y), p))
    )

    oppA.AddOperator(
        InfixOperator("|", ws, 3, Assoc.Left, (fun (x, p) y -> MpArithmetic((x, p), MpArithmeticOr, y), p))
    )

    oppA.AddOperator(
        InfixOperator("^", ws, 3, Assoc.Left, (fun (x, p) y -> MpArithmetic((x, p), MpArithmeticXor, y), p))
    )

    let oppC = OperatorPrecedenceParser<expr, unit, unit>()
    let mpComparison = oppC.ExpressionParser

    let termC = (mpArithmetic .>> ws) <|> between (str_ws "(") (str_ws ")") mpComparison

    oppC.TermParser <- termC
    oppC.AddOperator(InfixOperator("==", ws, 1, Assoc.Left, (fun (x, p) y -> MpComparison((x, p), MpEq, y), p)))
    oppC.AddOperator(InfixOperator("!=", ws, 1, Assoc.Left, (fun (x, p) y -> MpComparison((x, p), MpNe, y), p)))
    oppC.AddOperator(InfixOperator("<=", ws, 2, Assoc.Left, (fun (x, p) y -> MpComparison((x, p), MpLe, y), p)))
    oppC.AddOperator(InfixOperator(">=", ws, 2, Assoc.Left, (fun (x, p) y -> MpComparison((x, p), MpGe, y), p)))
    oppC.AddOperator(InfixOperator("<", ws, 2, Assoc.Left, (fun (x, p) y -> MpComparison((x, p), MpLt, y), p)))
    oppC.AddOperator(InfixOperator(">", ws, 2, Assoc.Left, (fun (x, p) y -> MpComparison((x, p), MpGt, y), p)))

    let oppL = OperatorPrecedenceParser<expr, unit, unit>()
    let mpLogical = oppL.ExpressionParser

    let termL = (mpComparison .>> ws) <|> between (str_ws "(") (str_ws ")") mpLogical

    oppL.TermParser <- termL
    oppL.AddOperator(InfixOperator("and", ws, 1, Assoc.Left, (fun (x, p) y -> MpLogical((x, p), MpAnd, y), p)))
    oppL.AddOperator(InfixOperator("or", ws, 1, Assoc.Left, (fun (x, p) y -> MpLogical((x, p), MpOr, y), p)))
    oppL.AddOperator(InfixOperator("xor", ws, 1, Assoc.Left, (fun (x, p) y -> MpLogical((x, p), MpXor, y), p)))

    let mpIndexA = between (str_ws "[") (ws >>. pstring "]") mpArithmetic |>> MpIndexA

    let mpIndexT =
        getPosition .>>. (((pchar '.') .>>. pint32) |>> snd)
        |>> (fun (x, y) -> y, x)
        |>> MpIndexT

    let mpSelf = pstring "self"

    let mpSelfExpr = mpSelf |>> (fun _ -> MpSelf) |> mpPosition

    let mpProperty =
        getPosition .>>. ((pchar '.') .>>. mpIdentifier |>> snd)
        |>> (fun (x, y) -> y, x)
        |>> MpProperty

    mpIdentPropR.Value <-
        pipe2
            (attempt mpSelf <|> mpIdentifier)
            (many1 (attempt mpIndexA <|> attempt mpIndexT <|> attempt mpProperty))
            (fun x y -> MpIdentProp(x, y))
        |> mpPosition

    let mpExpr =
        [ mpLambda
          mpClassConst
          mpTernary
          mpLogical
          mpComparison
          mpArithmetic
          mpSelfExpr ]
        |> List.map attempt
        |> choice

    mpTernaryR.Value <-
        pipe5 mpLogical (str_ws "?") mpExpr (str_ws ":") mpExpr (fun (l, p) _ e1 _ e2 -> MpTernary((l, p), e1, e2), p)

    mpTupleR.Value <-
        (between (str_ws "(") (pstring ")") (sepBy (wsl >>. mpExpr .>> wsl) (pchar ',')))
        |>> List.toArray
        |>> MpTuple
        |> mpPosition

    let mpArrayL =
        (between (str_ws "[") (pstring "]") (sepBy (wsl >>. mpExpr .>> wsl) (pchar ',')))
        |>> List.toArray
        |>> MpArrayL
        |> mpPosition

    let mpArrayD =
        pipe5 (str_ws "[") (mpExpr .>> ws) (str_ws ";") (mpExpr .>> ws) (pstring "]") (fun _ x _ y _ -> MpArrayD(x, y))
        |> mpPosition

    mpArrayR.Value <- attempt mpArrayL <|> attempt mpArrayD


    let mpSetGet = (attempt mpIdentProp <|> mpVar)

    let mpSliceInd =
        (pipe3 mpArithmetic (str_ws ":") mpArithmetic (fun x _ y -> (x, y)))

    mpSliceR.Value <-
        pipe4 mpSetGet (str_ws "[") mpSliceInd (pstring "]") (fun s _ (x, y) _ -> MpSlice(s, x, y))
        |> mpPosition

    let mpClassExpr =
        between (str_wsl "{") (pstring "}") (sepBy (ws >>. mpExpr .>> ws) (pchar ','))

    mpClassConstR.Value <- mpSetGet .>>. mpClassExpr |>> MpClassConst |> mpPosition

    let mpInvokeVar =
        between (str_ws "(") (pstring ")") (sepBy (ws >>. mpExpr .>> ws) (pchar ','))

    mpInvokeR.Value <- pipe2 mpSetGet mpInvokeVar (fun x y -> MpInvoke(x, y)) |> mpPosition

    let mpReservedFuncIdentifier1: Parser<string, unit> =
        choice (reservedFunctions1 |> List.map pstring)

    let mpReservedFunc1 =
        (pipe2 mpReservedFuncIdentifier1 (between (str_ws "(") (pstring ")") (ws >>. mpExpr .>> ws)) (fun x y ->
            MpReservedFunc1(x, y)))
        |> mpPosition

    let mpReservedFuncIdentifier0: Parser<string, unit> =
        choice (reservedFunctions0 |> List.map pstring)

    let mpReservedFunc0 =
        (pipe2 mpReservedFuncIdentifier0 (between (str_ws "(") (pstring ")") ws) (fun x _ -> MpReservedFunc0 x))
        |> mpPosition

    mpReservedFuncR.Value <- mpReservedFunc0 <|> mpReservedFunc1

    let mpAssignAdd =
        pipe3 mpSetGet (ws >>. (str_ws "+") .>>. (str_ws "=")) mpExpr (fun (id, p) _ e ->
            MpAssign(Set((id, p), (MpArithmetic((id, p), MpAdd, e), p))))

    let mpAssignSubtract =
        pipe3 mpSetGet (ws >>. (str_ws "-") .>>. (str_ws "=")) mpExpr (fun (id, p) _ e ->
            MpAssign(Set((id, p), (MpArithmetic((id, p), MpSubtract, e), p))))

    let mpAssignMultiply =
        pipe3 mpSetGet (ws >>. (str_ws "*") .>>. (str_ws "=")) mpExpr (fun (id, p) _ e ->
            MpAssign(Set((id, p), (MpArithmetic((id, p), MpMultiply, e), p))))

    let mpAssignDivide =
        pipe3 mpSetGet (ws >>. (str_ws "/") .>>. (str_ws "=")) mpExpr (fun (id, p) _ e ->
            MpAssign(Set((id, p), (MpArithmetic((id, p), MpDivide, e), p))))

    let mpAssignRest =
        pipe3 mpSetGet (ws >>. (str_ws "%") .>>. (str_ws "=")) mpExpr (fun (id, p) _ e ->
            MpAssign(Set((id, p), (MpArithmetic((id, p), MpRest, e), p))))

    let mpAssignArithmeticAnd =
        pipe3 mpSetGet (ws >>. (str_ws "&") .>>. (str_ws "=")) mpExpr (fun (id, p) _ e ->
            MpAssign(Set((id, p), (MpArithmetic((id, p), MpArithmeticAnd, e), p))))

    let mpAssignArithmeticOr =
        pipe3 mpSetGet (ws >>. (str_ws "|") .>>. (str_ws "=")) mpExpr (fun (id, p) _ e ->
            MpAssign(Set((id, p), (MpArithmetic((id, p), MpArithmeticOr, e), p))))

    let mpAssignArithmeticXor =
        pipe3 mpSetGet (ws >>. (str_ws "^") .>>. (str_ws "=")) mpExpr (fun (id, p) _ e ->
            MpAssign(Set((id, p), (MpArithmetic((id, p), MpArithmeticXor, e), p))))

    let mpAssignAnd =
        pipe3 mpSetGet (ws >>. (str_ws "and") .>>. (str_ws "=")) mpExpr (fun (id, p) _ e ->
            MpAssign(Set((id, p), (MpLogical((id, p), MpAnd, e), p))))

    let mpAssignOr =
        pipe3 mpSetGet (ws >>. (str_ws "or") .>>. (str_ws "=")) mpExpr (fun (id, p) _ e ->
            MpAssign(Set((id, p), (MpLogical((id, p), MpOr, e), p))))

    let mpAssignXor =
        pipe3 mpSetGet (ws >>. (str_ws "xor") .>>. (str_ws "=")) mpExpr (fun (id, p) _ e ->
            MpAssign(Set((id, p), (MpLogical((id, p), MpXor, e), p))))

    let mpAssignV =
        pipe3 mpSetGet (ws >>. (str_ws "=")) mpExpr (fun id _ e -> MpAssign(Set(id, e)))

    let mpAssign =
        [ mpAssignAnd
          mpAssignOr
          mpAssignXor
          mpAssignArithmeticAnd
          mpAssignArithmeticOr
          mpAssignArithmeticXor
          mpAssignAdd
          mpAssignSubtract
          mpAssignMultiply
          mpAssignDivide
          mpAssignRest
          mpAssignV ]
        |> List.map attempt
        |> choice

    let mpExprInstr = mpExpr |>> MpExpr

    let mpRange3 =
        pipe5 mpArithmetic (str_ws ",") mpArithmetic (str_ws ",") mpArithmetic (fun x _ y _ z -> (x, y, z))

    let mpRange2 =
        pipe3 mpArithmetic (str_ws ",") mpArithmetic (fun (x, p) _ y -> (x, p), y, (MpLiteral(MpInt 1), p))

    let mpRange1 =
        mpArithmetic
        |>> (fun (x, p) -> ((MpLiteral(MpInt 0), p), (x, p), (MpLiteral(MpInt 1), p)))

    let mpRange = attempt mpRange3 <|> attempt mpRange2 <|> attempt mpRange1

    let mpLoop = pipe2 (str_ws "loop") mpBlock (fun _ -> MpLoop)

    let mpWhile =
        pipe5 (str_ws "while") (str_ws "(") mpLogical (str_ws ")") mpBlock (fun _ _ e _ b -> MpWhile(e, b))

    let mpFor =
        pipe5 (str_ws1 "for") (mpIdentifier_ws |> mpPosition) (str_ws1 "in") mpRange mpBlock (fun _ s _ (x, y, z) b ->
            MpFor(s, x, y, z, b))

    let mpIf =
        pipe5 (str_ws "if") (str_ws "(") mpLogical (str_ws ")") mpBlock (fun _ _ e _ b -> (e, b))

    let mpElIf =
        pipe5 (str_ws "elif") (str_ws "(") mpLogical (str_ws ")") mpBlock (fun _ _ e _ b -> (e, b))

    let mpElse = (pstring "else") >>. mpBlock

    let mpIfI = mpIf |>> MpIf

    let mpElseI = mpIf .>>. mpElse |>> (fun ((e1, b1), b2) -> MpElse(e1, b1, b2))

    let mpElIfI =
        mpIf .>>. mpElIf |>> (fun ((e1, b1), (e2, b2)) -> MpElIf(e1, b1, e2, b2))

    let mpElIfElseI =
        pipe3 mpIf mpElIf mpElse (fun (e1, b1) (e2, b2) b3 -> MpElIfElse(e1, b1, e2, b2, b3))

    let mpFuncVar =
        between (str_ws "(") (pstring ")") (sepBy (ws >>. (mpIdentifier |> mpPosition) .>> ws) (pchar ','))

    let mpFuncVarSelfC =
        pipe4
            (str_ws "(")
            (mpSelf .>>. ws .>>. str_ws ",")
            (sepBy (ws >>. (mpIdentifier |> mpPosition) .>> ws) (pchar ','))
            (pstring ")")
            (fun _ _ x _ -> x)

    let mpFuncVarSelfS =
        (between (str_ws "(") (pstring ")") (mpSelf .>>. ws)) |>> (fun _ -> [])

    let mpFuncVarSelf = (attempt mpFuncVarSelfC) <|> mpFuncVarSelfS

    let mpLambdaSimple = mpExpr |>> MpReturn |>> (fun x -> List.toArray [ x ])

    let mpFuncSimple = (ws >>. str_ws "=>") >>. mpLambdaSimple .>> wsl

    let mpFuncStatic =
        pipe4
            (str_ws1 "func")
            (mpIdentifier |> mpPosition)
            mpFuncVar
            (attempt mpBlock <|> mpFuncSimple)
            (fun _ x y b -> MpFuncStatic(x, y, b))

    let mpFuncSelf =
        pipe4
            (str_ws1 "func")
            (mpIdentifier |> mpPosition)
            mpFuncVarSelf
            (attempt mpBlock <|> mpFuncSimple)
            (fun _ x y b -> MpFuncSelf(x, y, b))

    let mpClassProp =
        between (str_wsl "{") (pstring "}") (sepBy (wsl >>. mpIdentifier .>> wsl) (pchar ','))

    let mpClass =
        pipe3 (str_ws1 "class") (mpIdentifier |> mpPosition) mpClassProp (fun _ x y -> MpClass(x, y))

    let mpImpl =
        pipe3 (str_ws1 "impl") (mpIdentifier |> mpPosition) mpBlock (fun _ x b -> MpImpl(x, b))

    let mpDeriving =
        pipe4
            (str_ws1 "impl")
            (mpIdentifier_ws |> mpPosition)
            (str_ws1 "of")
            (mpIdentifier_ws |> mpPosition)
            (fun _ x _ y -> (x, y))

    let mpImplDerivingS =
        mpDeriving |>> (fun (x, y) -> MpImplDeriving(x, y, Array.empty))

    let mpImplDerivingB =
        pipe2 mpDeriving mpBlock (fun (x, y) b -> MpImplDeriving(x, y, b))

    let mpImplDeriving = attempt mpImplDerivingB <|> mpImplDerivingS

    let mpModule =
        pipe3 (str_ws1 "module") (mpIdentifier |> mpPosition) mpBlock (fun _ m b -> MpModule(m, b))

    let mpImport =
        pipe3 (str_ws1 "import") (mpIdentifier |> mpPosition) wsl (fun _ m _ -> MpImport m)

    let mpReturnValue = pipe2 (str_ws1 "return") mpExpr (fun _ -> MpReturn)

    let mpReturnVoid =
        getPosition .>>. pstring "return"
        |>> (fun (p, _) -> MpReturn(MpLiteral MpNull, p))

    let mpReturn = attempt mpReturnValue <|> mpReturnVoid

    let mpSimpleBreak =
        getPosition .>>. pstring "break" |>> (fun (x, _) -> MpBreak(uint8 0, x))

    let mpComplexBreak =
        pipe3 getPosition (str_ws1 "break") puint8 (fun x _ y -> MpBreak(y, x))

    let mpBreak = (attempt mpComplexBreak) <|> mpSimpleBreak

    let mpSimpleContinue: Parser<instruction, unit> =
        getPosition .>>. pstring "continue" |>> (fun (x, _) -> MpContinue(uint8 0, x))

    let mpComplexContinue =
        pipe3 getPosition (str_ws1 "continue") puint8 (fun x _ y -> MpContinue(y, x))

    let mpContinue = (attempt mpComplexContinue) <|> mpSimpleContinue

    let mpInstruct =
        [ mpAssign; mpExprInstr; mpReturn; mpBreak; mpContinue ]
        |> List.map attempt
        |> choice

    let mpBlockInstruct =
        [ mpLoop
          mpWhile
          mpFor
          mpElIfElseI
          mpElIfI
          mpElseI
          mpIfI
          mpFuncStatic
          mpFuncSelf
          mpClass
          mpImplDeriving
          mpImpl
          mpModule
          mpImport ]
        |> List.map attempt
        |> choice

    let mpComment =
        pchar '#' >>. skipManySatisfy (fun c -> c <> '\n') >>. pchar '\n'
        |>> (fun _ -> MpComment)

    let mpEndInst: Parser<char, unit> = wsl >>. pchar ';'

    let mpInstruction =
        wsl >>. ((mpInstruct .>> mpEndInst) <|> mpBlockInstruct <|> mpComment) .>> wsl

    let mpSimpleBlock =
        (wsl >>. ((mpInstruct .>> mpEndInst) <|> mpBlockInstruct) .>> wsl)
        |>> (fun x -> List.toArray [ x ])

    let mpComplexBlock =
        between (wsl >>. str_wsl "{") (str_wsl "}") (many mpInstruction)
        |>> List.toArray

    mpBlockR.Value <- attempt mpComplexBlock <|> mpSimpleBlock

    mpLambdaR.Value <-
        pipe3 mpFuncVar (ws >>. str_ws "=>") (mpComplexBlock <|> mpLambdaSimple) (fun x _ y -> MpLambda(x, y))
        |> mpPosition

    let mpLines = many mpInstruction .>> eof
