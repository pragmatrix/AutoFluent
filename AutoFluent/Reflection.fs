namespace AutoFluent

open System
open System.Reflection

module Reflection = 

    type Type 
        with
        member this.properties = 
            this.GetProperties(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly) 
            |> Seq.toList
        member this.events = 
            this.GetEvents(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly)
            |> Seq.toList

    type Event = EventInfo
    type Property = PropertyInfo
    type Type = System.Type

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Assembly = 
        let types (assembly: Assembly) = 
            assembly.GetTypes()
            |> Seq.filter (fun t -> t.IsPublic)
            |> Seq.toList

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Type =
        let isSealed (t: Type) = t.IsSealed
        let isAbstract (t: Type) = t.IsAbstract
        let isStatic t = (isSealed t) && (isAbstract t)

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Property = 
        let isReadable (p: Property) = 
            let gm = p.GetGetMethod()
            gm <> null && gm.GetParameters().Length = 0
        let isWritable (p: Property) = 
            let sm = p.GetSetMethod() 
            sm <> null && sm.GetParameters().Length = 1

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Event = 
        let canAddHandler (e: Event) =
            let am = e.GetAddMethod()
            am <> null && am.GetParameters().Length = 1
        let canRemoveHandler (e: Event) =
            let rm = e.GetRemoveMethod()
            rm <> null && rm.GetParameters().Length = 1
