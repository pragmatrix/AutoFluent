namespace AutoFluent

open System
open System.Reflection

module AutoFluent = 
    
    type FluentAssembly =
        { t: Assembly; types: FluentTypeProperties list }

    and FluentTypeProperties =
        { t: Type; properties: PropertyInfo list }

    let typeProperties (t: Type) =
        let canProcess (p : PropertyInfo) = 
            let setMethod = p.GetSetMethod()
            let t = p.PropertyType
            let isGeneric = t.IsGenericType
            (not isGeneric) && (not p.IsSpecialName) && setMethod <> null && setMethod.GetParameters().Length = 1

        let properties =
            t.GetProperties(BindingFlags.Public ||| BindingFlags.Instance)
            |> Array.filter canProcess
            |> Array.toList
        
        { t = t; properties = properties }

    let assemblyProperties (assembly: Assembly) =

        let canProcess (t: Type) = 
            let isStatic = t.IsSealed && t.IsAbstract
            t.IsPublic && (not isStatic) && (not t.IsGenericType)

        let types = 
            assembly.GetTypes()
            |> Array.filter(canProcess)
            |> Array.toList

        let fluentProperties = 
            types
            |> List.map typeProperties
            |> List.filter (fun tp -> tp.properties <> [])

        { t = assembly; types = fluentProperties }

