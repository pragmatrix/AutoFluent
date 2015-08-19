namespace AutoFluent

open System
open System.Reflection
open System.CodeDom.Compiler

open Reflection

module Generate =

    let private propertiesClassName (t: Type) =
        let tn = Syntax.typeName t
            
        let genericDiscriminator = 
            match tn.arguments with
            | [] -> ""
            | args -> List.length args |> string
               
        tn.localName + "FluentProperties" + genericDiscriminator

    let private staticClass (name: string) (blocks: Format.Code list) =
        Format.block [
            sprintf "public static class %s" name
            [ 
                yield! blocks 
            ]
        ]

    let private fluentPropertyExtensionMethod (property: Property) = 

        let self = property.declaringType
        let selfTypeParameterName = "SelfT"
    
        let selfTypeName = Syntax.typeName self

        let isSealed = Type.isSealed self
        
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
        
        let attributes = 
            let obsoleteAttribute = property.attribute<ObsoleteAttribute>()
            match obsoleteAttribute with
            | None -> [||]
            | Some attr ->
                [| 
                    sprintf "[System.Obsolete(%s)]" (Format.literal attr.Message) 
                |]

        Format.block [
            attributes
            sprintf "public static %s %s%s(this %s, %s)"
                selfTypeParameter.name property.name typeParameters (Format.parameter selfTypeParameter "self") (Format.parameter (Syntax.typeName property.valueType) "value")
            Format.indent (constraints |> List.map (string >> box))
            [ 
                sprintf "self.%s = value;" property.name
                sprintf "return self;"
            ]
        ]
    
    let private fluentPropertiesClass (properties: Property list) = 
        let t = List.head properties |> fun p -> p.declaringType
        let methods = 
            properties
            |> List.map fluentPropertyExtensionMethod

        staticClass (propertiesClassName t) methods

    let fluentProperties (assembly: Assembly) = 
        let properties = 
            assembly
            |> Assembly.types
            |> Seq.filter(Type.isStatic >> not)
            |> Seq.map (fun t -> t.properties)
            |> Seq.collect id
            |> Seq.filter Property.isWritable
            |> Seq.toList

        let mkTypeProperties (properties: Property list) =
            properties
            |> List.groupBy (fun p -> p.declaringType)
            |> List.map snd
            |> List.filter (List.isEmpty >> not)
            |> List.map fluentPropertiesClass

        let mkNamespace (name: string) (properties: Property list) = 
            let classes = 
                properties |> mkTypeProperties

            Format.block [
                sprintf "namespace %s" name
                [
                    yield! classes
                ]
            ]

        properties
            |> List.groupBy (fun p -> p.declaringNamespace)
            |> List.map (fun (ns, properties) -> mkNamespace ns properties)
            |> Format.Block

    let fluentTypeProperties (t: Type) = 
        let properties = t.properties
        match properties with
        | [] -> Format.Block []
        | _ -> fluentPropertiesClass properties

        
        

        

