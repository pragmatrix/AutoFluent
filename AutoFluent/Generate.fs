namespace AutoFluent

open System

open Reflection

module Generate =

    let private className (discriminator: string) (t: Type) = 
        let tn = Syntax.typeName t
            
        let genericDiscriminator = 
            match tn.arguments with
            | [] -> ""
            | args -> List.length args |> string
               
        tn.localName + discriminator + genericDiscriminator

    let private propertiesClassName = className "FluentProperties"
    let private eventsClassName = className "FluentEvents"

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

            code: string
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

                code = ""
            }

    let private extensionMethod (md: MethodDef) = 

        assert(md.self.IsSome)

        let parameters = 
            md.parameters
            |> List.map string
            |> Syntax.join ", "

        let typeParameters = 
            md.typeParameters
            |> Syntax.formatTypeArguments

        Format.block [
            md.attributes
            sprintf "public static %s %s%s(this %s, %s)"
                md.self.Value.name md.name typeParameters (Format.parameter md.self.Value "self") parameters
            Format.indent (md.constraints |> List.map (string >> box))
            [ 
                md.code
                sprintf "return self;"
            ]
        ]

    let private promoteAttributes (mi: MemberInfo) =
        let obsoleteAttribute = mi.GetCustomAttribute<ObsoleteAttribute>()
        match obsoleteAttribute with
        | null -> [||]
        | attr ->
            [| 
                sprintf "[System.Obsolete(%s)]" (Format.literal attr.Message) 
            |]

    let private selfTypeParameterName = "SelfT"
    let private selfTypeParameterTypeName = Syntax.TypeParameter selfTypeParameterName

    let private fluentExtensionMethod 
        (methodNameF: string -> string) 
        (parameters: Parameter list)
        (codeF: string -> string)
        (m: MemberInfo) = 

        let name = m.Name
        let self = m.DeclaringType
    
        let selfTypeName = Syntax.typeName self

        let isSealed = Type.isSealed self
        
        let attributes = promoteAttributes m

        let m = MethodDef.mk attributes (methodNameF name) parameters
        let m = { m with code = codeF name }

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
                    self = selfTypeParameterTypeName |> Some
                    typeParameters = selfTypeParameterName :: selfTypeParameters
                    constraints = selfConstraints :: constraints 
                }
        m |> extensionMethod
        
    let private fluentPropertyExtensionMethod (property: Property) = 
        let propertyType = property.PropertyType
        fluentExtensionMethod 
            id 
            [Parameter.mk (Syntax.typeName propertyType) "value"] 
            (sprintf "self.%s = value;") 
            (property :> MemberInfo)

    let private parameterNames =  
        seq {
            for c in 1..Int32.MaxValue do
                yield "arg" + (string c)
        }

    let private handlerForwarder numberOfArgs cast = 
        let parameterList = 
            parameterNames
            |> Seq.take numberOfArgs
            |> Syntax.join ", "

        sprintf "(%s) => handler((%s)%s)" parameterList cast parameterList

    let private fluentEventExtensionMethod (event: Event) = 

        let actualSenderTypeName = 
            if event.DeclaringType.IsSealed then
                Syntax.typeName event.DeclaringType
            else
                selfTypeParameterTypeName

        let handlerType = event.EventHandlerType
        let handlerTypeName, handlerCode = 
            let eventTypeName = Syntax.typeName handlerType
            match Syntax.tryPromoteEventHandler actualSenderTypeName handlerType with
            | None -> eventTypeName, "handler" 
            | Some t -> 
                t, handlerForwarder (List.length t.arguments) (actualSenderTypeName |> string)

        fluentExtensionMethod 
            (fun name -> "When" + name) 
            [Parameter.mk  handlerTypeName "handler"] 
            (fun value -> sprintf "self.%s += %s;" value handlerCode) 
            (event :> MemberInfo)
    
    let private mkFluentPropertiesClass (properties: Property list) = 
        let t = List.head properties |> fun p -> p.DeclaringType
        let methods = 
            properties
            |> List.map fluentPropertyExtensionMethod

        staticClass (propertiesClassName t) methods

    let private mkFluentEventsClass (events: Event list) = 
        let t = List.head events |> fun p -> p.DeclaringType
        let methods = 
            events
            |> List.map fluentEventExtensionMethod

        staticClass (eventsClassName t) methods

    let private classesForEachType (generator: 't list -> Format.Code) (members: 't list when 't :> MemberInfo)  = 
        members
        |> List.groupBy (fun p -> p.DeclaringType)
        |> List.map snd
        |> List.filter (List.isEmpty >> not)
        |> List.map generator

    let mkFluent 
        (map: Type -> 't list when 't :> MemberInfo) 
        (filter: 't -> bool) 
        (generator: 't list -> Format.Code) (assembly: Assembly) = 
        
        let properties = 
            assembly
            |> Assembly.types
            |> Seq.filter(Type.isStatic >> not)
            |> Seq.map map
            |> Seq.collect id
            |> Seq.filter filter
            |> Seq.toList

        let mkClassesForEachType = classesForEachType generator

        let mkNamespace (name: string) (members: 't list) = 
            let classes = 
                members |> mkClassesForEachType

            Format.block [
                sprintf "namespace %s" name
                [
                    yield! classes
                ]
            ]

        properties
            |> List.groupBy (fun p -> p.DeclaringType.Namespace)
            |> List.map (fun (ns, events) -> mkNamespace ns events)
            |> Format.Block

    let fluentEvents = mkFluent (fun t -> t.events) Event.canAddHandler mkFluentEventsClass
    let fluentProperties = mkFluent (fun t -> t.properties) Property.isWritable mkFluentPropertiesClass

    let fluentTypeProperties (t: Type) = 
        let properties = t.properties
        match properties with
        | [] -> Format.Block []
        | _ -> mkFluentPropertiesClass properties

    let fluentTypeEvents (t: Type) = 
        let events = t.events
        match events with
        | [] -> Format.Block []
        | _ -> mkFluentEventsClass events
        
    let fluentAssembly (assembly: Assembly) =
        let properties = fluentProperties assembly
        let events = fluentEvents assembly
        Format.Block [properties; events]
        

        

