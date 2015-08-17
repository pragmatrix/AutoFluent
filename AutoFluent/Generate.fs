namespace AutoFluent

open AutoFluent

open System
open System.Reflection

module Generate =

    [<AutoOpen>]
    module Helper = 
        open RoslynHelper

        let returnType (t: Type) = typeName t

        let parameter (t: Type) name = 
            sprintf "%s %s" (typeName t) name

        type Code = 
            | Scope of Code
            | Block of Code list
            | Line of string

        type Block = Code list

        let rec toCode (o : obj) = 
            match o with
            | :? string as s -> Line s
            | :? (obj list) as objs -> objs |> List.map toCode |> Block |> Scope
            | :? (string list) as block -> block |> List.map Line |> Block |> Scope
            | :? (Code list) as block -> block |> Block |> Scope
            | _ -> failwithf "invalid type %s in code" (o.GetType().Name)

        let block (block: obj list) =
            block
            |> List.map toCode
            |> Block

        let scope (nested: obj list) =
            nested

        let replaceTypeName = replaceTypeName  
        
        let staticClass (name: string) (blocks: Code list) =
            block [
                sprintf "public static class %s" name
                [ 
                    yield! blocks 
                ]
            ]

    let fluentPropertyExtensionMethod (t: Type) (property: PropertyInfo) = 
        let constraints = 
            let c = RoslynHelper.typeConstraints t
            if c <> "" then " " + c else ""
        
        block [
            sprintf "public static %s %s(this %s, %s)%s"
                (returnType t) (replaceTypeName property.Name t) (parameter t "self") (parameter property.PropertyType  "value") constraints
            [ 
                sprintf "self.%s = value;" property.Name
                sprintf "return self;"
            ]
        ]
    
    let typeProperties (properties: FluentTypeProperties) =
        let methods = 
            properties.properties
            |> List.map (fluentPropertyExtensionMethod properties.t)

        let name = 
            (RoslynHelper.Helper.typeNameWithoutFuzz properties.t) + "FluentProperties"
            
        staticClass name methods

    let assembly (assembly: FluentAssembly) =
        
        let mkNamespace (name: string) (types: FluentTypeProperties list) = 
            let generatedClasses = 
                types 
                |> List.map typeProperties

            block [
                sprintf "namespace %s" name
                [
                    yield! generatedClasses
                ]
            ]
        
        assembly.types
        |> List.groupBy (fun tp -> tp.t.Namespace)
        |> List.map (fun (ns, tp) -> mkNamespace ns tp)
        |> Block

    // inserts empty lines in between blocks.
    let rec format (c: Code) = 
        
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
        | Scope scope -> format scope |> Scope
        | Block blocks ->
            blocks
            |> List.map format
            |> List.mapFold mapFoldBlock false
            |> fst
            |> List.collect id
            |> Block

        | Line l -> c

    let sourceLines (c: Code) =
        
        let c = format c

        let rec lines indent (c: Code) =
            match c with
            | Line l -> Seq.singleton (indent + l)
            | Block block -> 
                block
                |> Seq.map (lines indent)
                |> Seq.collect id
            | Scope scope ->
                let l = 
                    scope |> lines (indent + "\t")

                seq {
                    yield indent + "{"
                    yield! l
                    yield indent + "}"
                }

        lines "" c

        

 

