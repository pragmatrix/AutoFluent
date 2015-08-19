namespace AutoFluent

open AutoFluent

open System
open System.Reflection

module Generate =

    [<AutoOpen>]
    module Helper = 
        open Syntax

        let returnType (t: Type) = typeName t

        let parameter (t: TypeName) name = 
            sprintf "%s %s" (t |> string) name

        type Code = 
            | Scope of Code
            | Block of Code list
            | Indent of Code
            | Line of string

        type Block = Code list

        let rec toCode (o : obj) = 
            match o with
            | :? string as s -> Line s
            | :? (obj list) as objs -> objs |> List.map toCode |> Block |> Scope
            | :? (string list) as block -> block |> List.map Line |> Block |> Scope
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
        
        let propertiesClassName (t: Type) =
            let tn = typeName t
            
            let genericDiscriminator = 
                match tn.arguments with
                | [] -> ""
                | args -> List.length args |> string
               
            tn.localName + "FluentProperties" + genericDiscriminator

        let staticClass (name: string) (blocks: Code list) =
            block [
                sprintf "public static class %s" name
                [ 
                    yield! blocks 
                ]
            ]

    let fluentPropertyExtensionMethod (self: Type) (property: PropertyInfo) = 

        let selfTypeParameterName = "SelfT"
    
        let selfTypeName = Syntax.typeName self

        let isSealed = self.IsSealed

        let selfTypeParameter = 
            if isSealed then
                selfTypeName
            else
                Syntax.TypeParameter selfTypeParameterName

        let constraints = 
            let typeConstraints = Syntax.typeConstraints self

            if isSealed then
                typeConstraints
            else
            let selfConstraint = 
                let c = Syntax.TypeConstraint selfTypeName
                Syntax.ConstraintsClause(selfTypeParameterName, [c])

            selfConstraint :: Syntax.typeConstraints self

        let typeParameters =
            if isSealed then
                selfTypeName.allParameters
            else
                selfTypeParameterName :: selfTypeName.allParameters
            |> Syntax.formatTypeArguments
        
        block [
            sprintf "public static %s %s%s(this %s, %s)"
                selfTypeParameter.name property.Name typeParameters (parameter selfTypeParameter "self") (parameter (Syntax.typeName property.PropertyType) "value")
            indent (constraints |> List.map (string >> box))
            [ 
                sprintf "self.%s = value;" property.Name
                sprintf "return self;"
            ]
        ]
    
    let typeProperties (properties: FluentTypeProperties) =
        let methods = 
            properties.properties
            |> List.map (fluentPropertyExtensionMethod properties.t)

        staticClass (propertiesClassName properties.t) methods

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
        | Indent indent -> format indent |> Indent

    let sourceLines (c: Code) =
        
        let c = format c

        let rec lines indent (c: Code) =
            match c with
            | Line l -> Seq.singleton (indent + l)
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

        

 

