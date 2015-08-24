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
    let private voidMethodsClassName = className "FluentVoidMethods"

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

        let selfParameter = Parameter.mk md.self.Value "self"

        let parameters = 
            selfParameter :: md.parameters
            |> List.map string
            |> Syntax.join ", "

        let typeParameters = 
            md.typeParameters
            |> Syntax.formatTypeArguments

        Format.block [
            md.attributes
            sprintf "public static %s %s%s(this %s)"
                md.self.Value.name md.name typeParameters parameters
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
            (fun name -> sprintf "self.%s += %s;" name handlerCode) 
            (event :> MemberInfo)

    let private fluentVoidMethodExtensionMethod (vm: MethodInfo) = 

        let parameters = 
            vm.GetParameters()
            |> Seq.map (fun p -> Parameter.mk (Syntax.typeName p.ParameterType) p.Name)
            |> Seq.toList

        let argumentList = 
            parameters
            |> List.map (fun p -> p.name)
            |> Syntax.join ", "

        fluentExtensionMethod 
            (fun name -> "Do" + name)
            parameters
            (fun name -> sprintf "self.%s(%s);" name argumentList) 
            (vm :> MemberInfo)
    
    let private mkFluentPropertiesClass (t: Type) (properties: Property list) = 
        properties
        |> List.map fluentPropertyExtensionMethod
        |> staticClass (propertiesClassName t)

    let private mkFluentEventsClass (t: Type) (events: Event list) = 
        events
        |> List.map fluentEventExtensionMethod
        |> staticClass (eventsClassName t)

    let private mkFluentVoidMethodsClass (t: Type) (methods: Method list) = 
        methods
        |> List.map fluentVoidMethodExtensionMethod
        |> staticClass (voidMethodsClassName t)

    let private classesForEachType (generator: Type -> 't list -> Format.Code) (members: 't list when 't :> MemberInfo)  = 
        members
        |> List.groupBy (fun p -> p.DeclaringType)
        |> List.filter (snd >> List.isEmpty >> not)
        |> List.map (fun (t, m) -> generator t m)

    let mkFluent 
        (map: Type -> 't list when 't :> MemberInfo) 
        (filter: 't -> bool) 
        (generator: Type -> 't list -> Format.Code) (assembly: Assembly) = 
        
        let properties = 
            assembly
            |> Assembly.types
            |> Seq.filter(Type.isStatic >> not)
            |> Seq.map map
            |> Seq.collect id
            |> Seq.filter filter
            |> Seq.toList

        let mkClassesForEachType = classesForEachType generator

        let mkNamespace (name: string option) (members: 't list) = 
            let classes = 
                members |> mkClassesForEachType

            match name with
            | None -> Format.Block classes
            | Some name ->

            Format.block [
                sprintf "namespace %s" name
                [
                    yield! classes
                ]
            ]

        properties
            |> List.groupBy (fun p -> p.DeclaringType.ns)
            |> List.map (fun (ns, events) -> mkNamespace ns events)
            |> Format.Block

    let fluentEvents = 
        mkFluent (fun t -> t.events) Event.canAddHandler mkFluentEventsClass
    let fluentProperties = 
        mkFluent (fun t -> t.properties) Property.isWritable mkFluentPropertiesClass
    let fluentVoidMethods = 
        mkFluent (fun t -> t.methods) (fun m -> m.ReturnType = Syntax.voidType && (not m.IsSpecialName)) mkFluentVoidMethodsClass

    let fluentTypeProperties (t: Type) = 
        let properties = t.properties
        match properties with
        | [] -> Format.Block []
        | _ -> mkFluentPropertiesClass t properties

    let fluentTypeEvents (t: Type) = 
        let events = t.events
        match events with
        | [] -> Format.Block []
        | _ -> mkFluentEventsClass t events

    let fluentAssembly (assembly: Assembly) =
        let properties = fluentProperties assembly
        let events = fluentEvents assembly
        let voidMethods = fluentVoidMethods assembly
        Format.Block [properties; events; voidMethods]
        

        

