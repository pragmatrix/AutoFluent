namespace AutoFluent

open System
open System.Reflection

module Reflection = 

    type SystemType = System.Type
    type SystemAssembly = System.Reflection.Assembly

    type Type = Type of SystemType
        with
        member this.value = let (Type v) = this in v
        member this.ns = this.value.Namespace
        member this.properties = 
            this.value.GetProperties(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly) 
            |> Seq.map Property 
            |> Seq.toList
        member this.attribute() : 't option = this.value.GetCustomAttribute<'t>() |> Option.ofObj

    and Property = Property of PropertyInfo
        with
        member this.sys = let (Property value) = this in value
        member this.valueType = this.sys.PropertyType |> Type
        member this.name = this.sys.Name
        member this.declaringType = this.sys.DeclaringType |> Type
        member this.declaringNamespace = this.declaringType.ns
        member this.attribute() : 't option = this.sys.GetCustomAttribute<'t>() |> Option.ofObj

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Type =
        let isSealed (Type t) = t.IsSealed
        let isAbstract (Type t) = t.IsAbstract
        let isStatic t = (isSealed t) && (isAbstract t)

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Property = 
        let isReadable (Property p) = 
            let gm = p.GetGetMethod()
            gm <> null && gm.GetParameters().Length = 0
        let isWritable (Property p) = 
            let sm = p.GetSetMethod() 
            sm <> null && sm.GetParameters().Length = 1

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Assembly = 
        let types (assembly: SystemAssembly) = 
            assembly.GetTypes()
            |> Seq.filter (fun t -> t.IsPublic)
            |> Seq.map Type
            |> Seq.toList
