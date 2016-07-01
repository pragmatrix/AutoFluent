namespace AutoFluent.Tests

open System
open NUnit.Framework
open FsUnit
open AutoFluent
open Reflection
open CompilationHelper

// Test-Types

type TypeWithGenericProperty() = 
    member val Property : System.Action<bool> = null with get, set

type GenericTypeWithProperty<'T>() = 
    member val Property : bool = false with get, set

type GenericTypeWithConstraintAndProperty<'T when 'T :> Exception>() = 
    member val Property : bool = false with get, set

[<Sealed>]
type SealedTypeWithProperty() = 
    member val Property : bool = false with get, set


[<TestFixture>]
type AutoFluentTests() =

    [<Test; Category("LongRunning")>] 
    member this.xamarinForms() = 
        let assembly = "Xamarin.Forms.Core" |> Assembly.Load
        assembly
        |> Generate.fluentAssembly
        |> Format.sourceLines
        |> compileAndDumpSource assembly ["System.Xml.dll"; "System.Xml.ReaderWriter.dll"]
        |> should equal 227328
        // ~61952 (without inherited members)


(*
    // hmm, our way of loading assemblies must be refined here, we load the
    // 4.6er assemblies, but actually want the reference assemblies for the
    // platform we are compiling for and not we are running on
    [<Test; Category("LongRunning")>] 
    member this.WPFPresentationCore() = 
        let assembly = "PresentationCore" |> Assembly.Load
        printfn "%A" assembly.FullName
        printfn "%A" assembly.CodeBase
        assembly 
        |> AutoFluent.propertiesOfAssembly
        |> Generate.assembly
        |> Generate.sourceLines
        |> compileAndDumpSource assembly 
            ["WindowsBase.dll"; "System.Xaml.dll"]
        |> should equal 0
*)      
    [<Test; Category("LongRunning")>] 
    member this.WPFPresentationFramework() = 
        let assembly = "PresentationFramework" |> Assembly.Load
        assembly
        |> Generate.fluentAssembly
        |> Format.sourceLines
        |> compileAndDumpSource assembly 
            [
                "System.Printing.dll"
                "UIAutomationProvider.dll"
                "System.Xml.dll"
                "PresentationCore.dll"
                "PresentationFramework.dll"
                "WindowsBase.dll"
                "System.Xaml.dll"
                "ReachFramework.dll"
                "UIAutomationTypes.dll"
            ]
        |> should equal 2808320
        // 300032 (without inherited members)

    [<Test>]
    member this.canHandleGenericProperties() =
        let code = sourceForPropertiesOfType typeof<TypeWithGenericProperty>
        let file = loadLines "TypeWithGenericProperty.cs"
        code |> should equal file
        
    [<Test>]
    member this.canHandlePropertyInGenericType() = 
        let code = sourceForPropertiesOfType typedefof<GenericTypeWithProperty<_>>
        let file = loadLines "GenericTypeWithProperty.cs"
        code |> should equal file

    [<Test>]
    member this.canHandlePropertyInGenericTypeWithConstraints() = 
        let code = sourceForPropertiesOfType typedefof<GenericTypeWithConstraintAndProperty<_>>
        let file = loadLines "GenericTypeWithConstraintAndProperty.cs"
        code |> should equal file

    [<Test>]
    member this.canHandleSealedClasses() =
        let code = sourceForPropertiesOfType typeof<SealedTypeWithProperty>
        let file = loadLines "SealedTypeWithProperty.cs"
        code |> should equal file

open AutoFluent.Format

[<TestFixture>]
type FormatterTests() =
    
    [<Test>]
    member this.formatInsertsEmptyLineBetweenBlocks() = 

        let c = 
            Block [
                Block [Line "a"]
                Block [Line "b"]
            ]

        let formatted = Format.separateBlocks c
        formatted |> should equal (Block [Block[Line "a"]; Line ""; Block[Line "b"]])

    [<Test>]
    member this.formatDoesNotInsertLinesBetweenEmptyBlocks() = 

        let c = 
            Block [
                Block []
                Block []
            ]

        let formatted = Format.sourceLines c |> Seq.toArray
        formatted |> should equal [||]


