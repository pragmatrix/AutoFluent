namespace AutoFluent

open Syntax
open System.IO
open System.CodeDom
open System.CodeDom.Compiler

open Reflection

module Format =

    let returnType (t: Type) = typeName t

    let parameter (t: TypeName) name = 
        sprintf "%s %s" (t |> string) name

    type Code = 
        | Scope of Code
        | Block of Code list
        | Parts of Code list
        | Indent of Code
        | Line of string

    type Block = Code list

    let rec toCode (o : obj) = 
        match o with
        | :? string as s -> Line s
        | :? (obj list) as objs -> objs |> List.map toCode |> Block |> Scope
        | :? (string list) as block -> block |> List.map Line |> Block |> Scope
        | :? (string array) as lines -> lines |> Array.map Line |> Array.toList |> Parts
        | :? (Code list) as block -> block |> Block |> Scope
        | :? Code as c -> c
        | _ -> failwithf "invalid type %s in code" (o.GetType().Name)

    let block (block: obj list) =
        block
        |> List.map toCode
        |> Block

    let indent (indent: obj list) =
        indent
        |> block
        |> Indent

    let scope (nested: obj list) =
        nested
        
    // http://stackoverflow.com/questions/323640/can-i-convert-a-c-sharp-string-value-to-an-escaped-string-literal

    let private cSharpProvider = CodeDomProvider.CreateProvider("CSharp");

    let literal str = 
        use writer = new StringWriter()
        cSharpProvider.GenerateCodeFromExpression(CodePrimitiveExpression(str), writer, null)
        string writer
 
    // inserts empty lines in between blocks.
    let rec separateBlocks (c: Code) = 
        
        let isBlock = function 
            | Block _ -> true
            | _ -> false

        let mapFoldBlock blockBefore code = 
            let blockNow = isBlock code
            match blockBefore, blockNow with
            | true, true ->
                ([Line ""; code], blockNow)
            | _ -> 
                ([code], blockNow)
        
        match c with
        | Scope scope -> separateBlocks scope |> Scope
        | Block blocks ->
            blocks
            |> List.map separateBlocks
            |> List.mapFold mapFoldBlock false
            |> fst
            |> List.collect id
            |> Block

        | Line l -> c
        | Parts lines -> c
        | Indent indent -> separateBlocks indent |> Indent

    let rec trimList (l: Code list) : Code list option =
        l
        |> List.map trim
        |> List.choose id
        |> function [] -> None | l -> Some l

    and trim (c: Code) : Code option =
        match c with
        | Scope c -> c |> trim |> Option.map Scope
        | Indent c -> c |> trim |> Option.map Indent
        | Block cs -> cs |> trimList |> Option.map Block
        | Parts cs -> cs |> trimList |> Option.map Parts
        | Line _ -> Some c

    let sanitize (c: Code) =
        c
        |> trim
        |> Option.map separateBlocks

    let sourceLines (c: Code) =
        
        let c = sanitize c
        match c with
        | None -> Seq.empty
        | Some c ->

        let rec lines indent (c: Code) =
            match c with
            | Line l -> Seq.singleton (indent + l)
            | Parts parts -> 
                parts
                |> Seq.map (lines indent)
                |> Seq.collect id
            | Block block -> 
                block
                |> Seq.map (lines indent)
                |> Seq.collect id
            | Scope code ->
                let l = 
                    code |> lines (indent + "\t")

                seq {
                    yield indent + "{"
                    yield! l
                    yield indent + "}"
                }
            | Indent code ->
                code |> lines (indent + "\t")

        lines "" c
