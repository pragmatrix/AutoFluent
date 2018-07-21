namespace AutoFluent

open System
open System.Collections.Generic
open System.Reflection
open AutoFluent.Reflection

module Generate =

    type ExtensionSource<'t when 't :> MemberInfo> = { 
        Type: Type
        Source: 't
    }
    
    type 't source when 't :> MemberInfo = ExtensionSource<'t>

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

    type Parameter = {
        TypeName: Syntax.TypeName
        Name: string
        IsOut: bool
    } with
        override this.ToString() = Format.parameter this.TypeName this.Name this.IsOut
        static member mk tn n isOut = { TypeName = tn; Name = n; IsOut = isOut }

    module Format = 
        let parameterAsArgument p = 
            let prefix = 
                match p.IsOut with
                | true -> "out "
                | false -> ""
            sprintf "%s%s" prefix (Format.name p.Name)

    type MethodDef = { 
        Attributes: string[]
        Name: string
        Parameters: Parameter list
          
        // generics
        TypeArguments: string list
        Self: Syntax.TypeName option
        Constraints: Syntax.ConstraintsClause list

        Code: string
    } with
        static member mk attributes name parameters = {
            Attributes = attributes
            Name = name
            Parameters = parameters

            // this (extension method)
            Self = None

            // generics
            TypeArguments = []
            Constraints = []

            Code = ""
        }

    let private extensionMethod (md: MethodDef) = 

        match md.Self with
        | None -> failwith "can only generate extension methods yet"
        | Some self ->

        let selfParameter = Parameter.mk md.Self.Value "self" false

        let parameters = 
            selfParameter :: md.Parameters
            |> List.map string
            |> Syntax.join ", "

        let typeParameters = 
            md.TypeArguments
            |> Syntax.formatTypeArguments

        Format.block [
            md.Attributes
            sprintf "public static %s %s%s(this %s)"
                (self |> string) (Format.name md.Name) typeParameters parameters
            Format.indent (md.Constraints |> List.map (string >> box))
            [ 
                md.Code
                "return self;"
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
        (t: Type)
        (methodNameF: string -> string) 
        (parameters: Parameter list)
        (codeF: string -> string)
        (m: MemberInfo) = 

        let memberTypeParameters = Syntax.memberGenericArguments m
        let memberConstraints = Syntax.memberConstraints m

        let name = m.Name
        let self = t
    
        let selfTypeName = Syntax.typeName self

        let explicitSelfType = true // Type.isSealed self
        
        let attributes = promoteAttributes m

        let m = MethodDef.mk attributes (methodNameF name) parameters
        let m = { m with Code = codeF (Format.name name) }

        let m = 
            let constraints = Syntax.typeConstraints self 
            let selfTypeParameters = selfTypeName.allParameters
            if explicitSelfType then
                { m with 
                    Self = Some selfTypeName
                    TypeArguments = selfTypeParameters @ memberTypeParameters
                    Constraints = constraints @ memberConstraints
                }
            else
                let selfConstraints = 
                    let c = Syntax.TypeConstraint selfTypeName
                    Syntax.ConstraintsClause(selfTypeParameterName, [c])
                    
                { m with 
                    Self = selfTypeParameterTypeName |> Some
                    TypeArguments = selfTypeParameterName :: selfTypeParameters
                    Constraints = selfConstraints :: constraints 
                }
        m |> extensionMethod
        
    let private fluentPropertyExtensionMethod (t: Type) (property: Property) = 
        let propertyType = property.PropertyType
        fluentExtensionMethod 
            t
            id 
            [Parameter.mk (Syntax.typeName propertyType) "value" false] 
            (sprintf "self.%s = value;") 
            (property :> MemberInfo)

    let private handlerForwarder eventTypeName = 
        sprintf "new %s(handler.DemoteSender())" eventTypeName

    let private fluentEventExtensionMethod (t: Type) (event: Event) = 

        let explicitSelfT = true // event.DeclaringType.IsSealed

        let actualSenderTypeName = 
            if explicitSelfT then
                Syntax.typeName t
            else
                selfTypeParameterTypeName

        let handlerType = event.EventHandlerType
        let handlerTypeName, handlerCode = 
            let eventTypeName = Syntax.typeName handlerType
            match Syntax.tryPromoteEventHandler actualSenderTypeName handlerType with
            | None -> eventTypeName, "handler" 
            | Some t -> 
                t, handlerForwarder (eventTypeName |> string)

        fluentExtensionMethod 
            t
            (fun name -> "When" + name) 
            [Parameter.mk  handlerTypeName "handler" false] 
            (fun name -> sprintf "self.%s += %s;" name handlerCode) 
            (event :> MemberInfo)

    let private fluentVoidMethodExtensionMethod (t: Type) (vm: MethodInfo) = 

        let parameters = 
            vm.GetParameters()
            |> Seq.map (fun p -> Parameter.mk (Syntax.typeName p.ParameterType) p.Name p.IsOut)
            |> Seq.toList

        let genericArguments =
            Syntax.memberGenericArguments vm
            |> Syntax.formatTypeArguments

        let argumentList = 
            parameters
            |> List.map Format.parameterAsArgument
            |> Syntax.join ", "

        fluentExtensionMethod 
            t
            (fun name -> "Do" + name)
            parameters
            (fun name -> sprintf "self.%s%s(%s);" name genericArguments argumentList) 
            (vm :> MemberInfo)
    
    let private classesForEachType 
        (generator: Type -> 't list -> Format.Code) 
        (members: 't source list)  = 
        members
        |> List.groupBy (fun p -> p.Type)
        |> List.filter (snd >> List.isEmpty >> not)
        |> List.map (fun (t, m) -> generator t (m |> List.map (fun s -> s.Source)))

    let mkFluent 
        (map: Type -> 't source list) 
        (generator: Type -> 't list -> Format.Code) (assembly: Assembly) = 
        
        let properties = 
            assembly
            |> Assembly.types
            |> Seq.filter(Type.isStatic >> not)
            |> Seq.map map
            |> Seq.collect id
            |> Seq.toList

        let mkClassesForEachType = classesForEachType generator

        let mkNamespace (name: string option) (members: 't source list) = 
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
            |> List.groupBy (fun s -> s.Type.ns)
            |> List.map (fun (ns, events) -> mkNamespace ns events)
            |> Format.Block

    let private mkFluentPropertiesClass (t: Type) (properties: Property list) = 
        properties
        |> List.map (fluentPropertyExtensionMethod t)
        |> staticClass (propertiesClassName t)

    let private mkFluentEventsClass (t: Type) (events: Event list) = 
        events
        |> List.map (fluentEventExtensionMethod t)
        |> staticClass (eventsClassName t)

    let private mkFluentVoidMethodsClass (t: Type) (methods: Method list) = 
        methods
        |> List.map (fluentVoidMethodExtensionMethod t)
        |> staticClass (voidMethodsClassName t)

    let private mkSource t mi = 
        { Type = t; Source = mi }

    let fluentEvents = 
        mkFluent 
            (fun t -> t.events |> List.filter Event.canAddHandler |> List.map (mkSource t)) 
            mkFluentEventsClass
    
    let fluentProperties = 
        mkFluent 
            (fun t -> t.properties |> List.filter Property.isWritable |> List.map (mkSource t)) 
            mkFluentPropertiesClass
    
    let fluentVoidMethods = 
        let filter (t: Type) = 
            let isDelegate = typeof<Delegate>.IsAssignableFrom t
            let disposeMethod = Syntax.tryGetDisposeMember t
            let getObjectDataMethod = Syntax.tryGetGetObjectDataMember t
            fun (m: MethodInfo) ->
                not isDelegate &&
                m.ReturnType = Syntax.voidType && 
                not m.IsSpecialName && 
                m <> disposeMethod &&
                m <> getObjectDataMethod

        let query (t: Type) = 
            t.methods
            |> List.filter (filter t)
            |> List.map (mkSource t)

        mkFluent query mkFluentVoidMethodsClass

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
        

        

