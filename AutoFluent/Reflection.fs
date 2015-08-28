namespace AutoFluent

open System
open System.Reflection

module Reflection = 

    let private defaultBindingFlags = 
        // we need to get inherited members, too. Type constraints do not add to the
        // extension method signature, which is used for the overload resolution.
        BindingFlags.Public ||| BindingFlags.Instance // ||| BindingFlags.DeclaredOnly

    type Type 
        with
        member this.properties = 
            this.GetProperties(defaultBindingFlags)
            |> Array.toList
        member this.events = 
            this.GetEvents(defaultBindingFlags)
            |> Array.toList
        member this.methods = 
            this.GetMethods(defaultBindingFlags)
            |> Array.toList
        member this.ns = 
            this.Namespace |> Option.ofObj

    type Type = System.Type
    type Event = EventInfo
    type Property = PropertyInfo
    type Method = MethodInfo

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
