namespace AutoFluent

open System
open System.Reflection

open Reflection

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

    module Helper = 

        let qualifiedName (t: Type) = 
            // For some events, t.FullName is null, even though
            // namespace and name is properly set, so we use
            // Namespace and the Name as the "full" name instead.
            t.Namespace + "." + t.Name

        let rec typeName (t: Type) = 
            if t.IsGenericParameter then
                TypeParameter t.Name
            else
            let qName = qualifiedName t
            if not t.IsGenericType then
                TypeName (qName, [])
            else
            let args = 
                t.GetGenericArguments()
                |> Array.map typeName
                |> Array.toList

            let name = 
                let i = qName.IndexOf('`');
                if i = -1 then qName
                else qName.Remove i

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

    let typeName (t: Type) = Helper.typeName t
    let typeConstraints (t: Type) = Helper.typeConstraints t

    let private objType = typeof<Object>
    let private actionTypeName = (typeName typeof<Action>).name
    let private voidType = typeof<Void>
    
    let tryPromoteEventHandler (promoted: Type) (t: Type) = 
        let invoker = t.GetMethod("Invoke", BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly)
        if invoker = null || invoker.ReturnType <> voidType then None else
        let param = invoker.GetParameters()
        if (param.Length < 1) then None else
        let first = param.[0]
        if first.Name <> "sender" then None else
        if first.ParameterType <> objType then None else
        let _::parameterTypeNames = 
            param 
            |> Seq.map (fun p -> p.ParameterType |> typeName)
            |> Seq.toList

        TypeName (actionTypeName, (typeName promoted) :: parameterTypeNames)
        |> Some



