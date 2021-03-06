﻿namespace AutoFluent

open System
open System.Reflection
open System.Runtime.Serialization
open AutoFluent.Reflection

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
            | TypeArray _ -> failwithf "no arguments for %s" (string this)
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

    type ConstraintsClause =
        | ConstraintsClause of string * Constraint list
        member this.parameter = 
            this |> function ConstraintsClause(p, _) -> p
        member this.constraints =
            this |> function ConstraintsClause(_, c) -> c
        override this.ToString() = 
            this |> function ConstraintsClause(tp, cl) -> sprintf "where %s : %s" tp (cl |> List.map(string) |> join ", ")

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

        let rec qualifiedName (t: Type) = 

            let typeName =
                if t.IsByRef
                then 
                    if not (t.Name.EndsWith("&")) 
                    then failwithf "internal error: type's name '%s' is byref, but does not end with '&'" t.Name
                    else t.Name.Substring(0, t.Name.Length-1)
                else t.Name

            // For some events, t.FullName is null, even though
            // namespace and name is properly set, so we use
            // Namespace and the Name as the "full" name instead.
            
            // also note that Namespace can be null, too, this is then the global
            // namespace
            if t.IsNested then
                (qualifiedName t.DeclaringType) + "." + typeName
            else
            match t.ns with
            | None -> typeName
            | Some ns -> ns + "." + typeName

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

        let typeConstraintOfArgument (t: Type) = 
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

        let typeConstraintsOfGenericArguments (arguments: Type[]) =
            arguments
            |> Seq.map (fun arg -> arg, typeConstraintOfArgument arg)
            |> Seq.filter (fun (_, constraints) -> constraints <> [])
            |> Seq.map (fun (arg, constraints) -> ConstraintsClause ((typeName arg |> string), constraints))
            |> Seq.toList

        let typeConstraints (t: Type) : ConstraintsClause list =
            if not t.IsGenericType then [] else
            t.GetGenericArguments()
            |> typeConstraintsOfGenericArguments

        let methodConstraints (m: MethodInfo) : ConstraintsClause list =
            if not m.IsGenericMethod then [] else
            m.GetGenericArguments()
            |> typeConstraintsOfGenericArguments

    let typeName (t: Type) = Helper.typeName t
    let typeConstraints (t: Type) = Helper.typeConstraints t

    let memberGenericArguments (m: MemberInfo) =
        match m with
        | :? MethodInfo as mi ->
            mi.GetGenericArguments()
            |> Seq.map typeName
            |> Seq.map (fun tn -> tn.allParameters)
            |> Seq.collect id
            |> Seq.toList
        | _ -> []

    let memberConstraints (m: MemberInfo) = 
        match m with
        | :? MethodInfo as mi ->
            Helper.methodConstraints mi
        | _ -> []

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

    let private disposableInterface = typeof<IDisposable>        
        
    let tryGetDisposeMember (t: Type) : MethodInfo = 
        match t.GetInterfaces() |> Array.tryFind ((=) disposableInterface) with
        | None -> null
        | _ ->
        let map = t.GetInterfaceMap(disposableInterface)
        assert(map.TargetMethods.Length = 1)
        map.TargetMethods.[0]

    let private serializableInterface = typeof<ISerializable>        
    
    let tryGetGetObjectDataMember (t: Type) : MethodInfo = 
        match t.GetInterfaces() |> Array.tryFind ((=) serializableInterface) with
        | None -> null
        | _ ->
        let map = t.GetInterfaceMap(serializableInterface)
        assert(map.TargetMethods.Length = 1)
        map.TargetMethods.[0]






