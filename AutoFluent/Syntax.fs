namespace AutoFluent

open System
open System.Reflection

open Reflection

module Syntax =

    let join sep (strs: string seq) = String.Join(sep, strs)
    
    let formatTypeArguments(args: string list) =
        match args with
        | [] -> ""
        | _ -> "<" + (args |> List.map string |> join ", ") + ">"

    type TypeName = 
        | TypeName of string * TypeName list
        | TypeParameter of string
        | TypeArray of TypeName * int // rank
        with
        member this.name = 
            match this with
            | TypeName (name, _) -> name
            | TypeParameter name -> name
            | TypeArray _ -> failwithf "no name for %s" (string this)
        member this.localName = 
            match this with
            | TypeName (name, _) ->
                let i = name.LastIndexOf "."
                if i = -1 then name else name.Substring(i+1)
            | TypeParameter name -> name
            | TypeArray _ -> failwithf "no localName for %s" (string this)
        member this.arguments = 
            match this with
            | TypeName (_, args) -> args
            | TypeParameter _ -> []
            | TypeArray _ -> failwith "no arguments for %s" (string this)
        member this.allParameters = 
            match this with
            | TypeName (_, args) -> 
                args 
                |> List.map (fun tn -> tn.allParameters) 
                |> List.collect id
            | TypeParameter p -> [p]
            | TypeArray (tn, _) -> tn.allParameters
        override this.ToString() = 
            match this with
            | TypeName (name, []) -> name
            | TypeName (name, args) -> name + formatTypeArguments (args |> List.map string)
            | TypeParameter name -> name
            | TypeArray (tn, rank) -> (string tn) + "[" + String(',', rank-1) + "]"

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
            
            // also note that Namespace can be null, too, this is then the global
            // namespace

            match t.ns with
            | None -> t.Name
            | Some ns -> ns + "." + t.Name

        let rec typeName (t: Type) = 
            if t.IsGenericParameter then
                TypeParameter t.Name
            else
            if t.IsArray then
                TypeArray (typeName (t.GetElementType()), t.GetArrayRank())
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
                if not t.IsGenericParameter then [] else
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
    let voidType = typeof<Void>
    
    let tryPromoteEventHandler (promoted: TypeName) (t: Type) = 
        let invoker = t.GetMethod("Invoke", BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly)
        if invoker = null || invoker.ReturnType <> voidType then None else
        let param = invoker.GetParameters()
        if (param.Length < 1) then None else
        let first = param.[0]
        if first.Name <> "sender" then None else
        if first.ParameterType <> objType then None else
        let parameterTypeNames = 
            param 
            |> Seq.skip 1
            |> Seq.map (fun p -> p.ParameterType |> typeName)
            |> Seq.toList

        TypeName (actionTypeName, promoted :: parameterTypeNames)
        |> Some



