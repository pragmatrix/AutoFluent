﻿namespace AutoFluent

open System
open System.Reflection

module Syntax =

    let join sep (strs: string list) = String.Join(sep, strs)
    
    let formatTypeArguments(args: string list) =
        match args with
        | [] -> ""
        | _ -> "<" + (args |> List.map string |> join ", ") + ">"

    type TypeName = 
        | TypeName of string * TypeName list
        | TypeParameter of string
        with
        member this.name = 
            match this with
            | TypeName (name, _) -> name
            | TypeParameter name -> name
        member this.localName = 
            match this with
            | TypeName (name, _) ->
                let i = name.LastIndexOf "."
                if i = -1 then name else name.Substring(i+1)
            | TypeParameter name -> name
        member this.arguments = 
            match this with
            | TypeName (_, args) -> args
            | TypeParameter _ -> []
        member this.allParameters = 
            match this with
            | TypeName (_, args) -> 
                args 
                |> List.map (fun tn -> tn.allParameters) 
                |> List.collect id
            | TypeParameter p -> [p]
        override this.ToString() = 
            match this with
            | TypeName (name, []) -> name
            | TypeName (name, args) -> name + formatTypeArguments (args |> List.map string)
            | TypeParameter name -> name


    type ConstraintsClause = ConstraintsClause of string * Constraint list
        with
        member this.parameter = 
            match this with
            | ConstraintsClause(p, _) -> p
        member this.constraints =
            match this with
            | ConstraintsClause(_, c) -> c
        override this.ToString() = 
            match this with
            | ConstraintsClause(tp, cl) -> sprintf "where %s : %s" tp (cl |> List.map(string) |> join ", ")

    and Constraint =
        | TypeConstraint of TypeName
        | ReferenceTypeConstraint
        | ValueTypeConstraint
        | DefaultConstructorConstraint
        with
        override this.ToString() = 
            match this with
            | TypeConstraint t -> t |> string
            | ReferenceTypeConstraint -> "class"
            | ValueTypeConstraint -> "struct"
            | DefaultConstructorConstraint -> "new()"

    [<AutoOpen>]
    module Helper = 

#if false

        let workspace = MSBuildWorkspace.Create()
        let sg = SyntaxGenerator.GetGenerator(workspace, "C#")


        let typeNameOfNonGenericType(t: Type) : string = 
            assert(not t.IsGenericType)
            // even if it is not a generic type, it may be a generic parameter
            if (t.IsGenericParameter) then t.Name
            else t.FullName

        let typeNameWithoutFuzz(gt: Type) : string = 
            if not gt.IsGenericType then gt.Name
            else
            let name = gt.GetGenericTypeDefinition().Name
            let i = name.IndexOf('`');
            if (i = -1) then name else name.Remove(i)

        let rec syntaxOfTypeName (t: Type) : SyntaxNode = 

            if (not t.IsGenericType) then
                sg.IdentifierName(typeNameOfNonGenericType(t))
            else

            let name = typeNameWithoutFuzz(t)

            let arguments = 
                t.GetGenericArguments()
                |> Array.map syntaxOfTypeName
            
            let ns = sg.IdentifierName(t.Namespace)
            let tn = sg.GenericName(name, arguments)
            sg.QualifiedName(ns, tn)

        let typeName (t: Type) = 
            syntaxOfTypeName t |> string

#endif

        let rec typeName (t: Type) = 
            if t.IsGenericParameter then
                TypeParameter t.Name
            else
            if not t.IsGenericType then
                TypeName (t.FullName, [])
            else
            let args = 
                t.GetGenericArguments()
                |> Array.map typeName
                |> Array.toList

            let name = 
                let name = t.FullName
                let i = name.IndexOf('`');
                if i = -1 then name
                else name.Remove i

            TypeName (name, args)

        // https://msdn.microsoft.com/en-us/library/d5x73970.aspx
        // https://msdn.microsoft.com/en-us/library/system.type.getgenericparameterconstraints.aspx

        let typeConstraints (t: Type) : ConstraintsClause list =

            if not t.IsGenericType then []
            else

            let ofArgument (t: Type) = 
                let constraintTypes = t.GetGenericParameterConstraints()
                let attributes = t.GenericParameterAttributes
                let variance = attributes &&& GenericParameterAttributes.VarianceMask
                let specialConstraints = attributes &&& GenericParameterAttributes.SpecialConstraintMask

                if variance <> GenericParameterAttributes.None then
                    failwith "a type constraint with co or contravariance is not yet supported"

                let typeConstraints =
                    constraintTypes 
                    |> Array.map (typeName >> TypeConstraint)

                [
                    if specialConstraints.HasFlag GenericParameterAttributes.ReferenceTypeConstraint then
                        yield ReferenceTypeConstraint
                    if specialConstraints.HasFlag GenericParameterAttributes.NotNullableValueTypeConstraint then
                        yield ValueTypeConstraint
                    if specialConstraints.HasFlag GenericParameterAttributes.DefaultConstructorConstraint then
                        yield DefaultConstructorConstraint
                    yield! typeConstraints
                ]


            assert(t.IsGenericType)
            t.GetGenericArguments()
            |> Seq.map (fun arg -> arg, ofArgument arg)
            |> Seq.filter (fun (arg, constraints) -> constraints <> [])
            |> Seq.map (fun (arg, constraints) -> ConstraintsClause ((typeName arg |> string), constraints))
            |> Seq.toList

    let typeName = typeName 
    let typeConstraints = typeConstraints
