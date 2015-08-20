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

    type Parameter =  
        {
            typeName: Syntax.TypeName;
            name: string
        }
        with
        override this.ToString() = Format.parameter this.typeName this.name
        static member mk tn n = { typeName = tn; name = n}


    type MethodDef =
        { 
            attributes: string[]
            name: string
            parameters: Parameter list
          
            // generics
            typeParameters: string list
            self: Syntax.TypeName option
            constraints: Syntax.ConstraintsClause list

            // body
            propertyToAssign: string
        }
        static member mk attributes name parameters =
            {
                attributes = attributes
                name = name
                parameters = parameters

                // this (extension method)
                self = None

                // generics
                typeParameters = []
                constraints = []
               
                // body
                propertyToAssign = ""
            }

    let private extensionMethod (em: MethodDef) = 

        assert(em.self.IsSome)

        let parameters = 
            em.parameters
            |> List.map string
            |> Syntax.join ", "

        let typeParameters = 
            em.typeParameters
            |> Syntax.formatTypeArguments

        Format.block [
            em.attributes
            sprintf "public static %s %s%s(this %s, %s)"
                em.self.Value.name em.name typeParameters (Format.parameter em.self.Value "self") parameters
            Format.indent (em.constraints |> List.map (string >> box))
            [ 
                sprintf "self.%s = value;" em.propertyToAssign
                sprintf "return self;"
            ]
        ]

    let private fluentPropertyExtensionMethod (property: Property) = 

        let self = property.declaringType
        let selfTypeParameterName = "SelfT"
    
        let selfTypeName = Syntax.typeName self

        let isSealed = Type.isSealed self
        
        let attributes = 
            let obsoleteAttribute = property.attribute<ObsoleteAttribute>()
            match obsoleteAttribute with
            | None -> [||]
            | Some attr ->
                [| 
                    sprintf "[System.Obsolete(%s)]" (Format.literal attr.Message) 
                |]

        let m = MethodDef.mk attributes property.name [Parameter.mk (Syntax.typeName property.valueType) "value"]
        let m = { m with propertyToAssign = property.name }

        let m = 
            let constraints = Syntax.typeConstraints self 
            let selfTypeParameters = selfTypeName.allParameters
            if isSealed then
                { m with 
                    self = Some selfTypeName
                    typeParameters = selfTypeParameters
                    constraints = constraints 
                }
            else
                let selfConstraints = 
                    let c = Syntax.TypeConstraint selfTypeName
                    Syntax.ConstraintsClause(selfTypeParameterName, [c])
                    
                { m with 
                    self = Syntax.TypeParameter selfTypeParameterName |> Some
                    typeParameters = selfTypeParameterName :: selfTypeParameters
                    constraints = selfConstraints :: constraints 
                }
        m |> extensionMethod
        
    
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

        
        

        

