namespace AutoFluent.Tests

open System
open System.IO
open System.Reflection

open NUnit.Framework
open FsUnit

open AutoFluent
open Generate

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

[<AutoOpen>]
module Helper = 
    open Microsoft.CSharp
    open System.CodeDom.Compiler

    let loadLines fn = 
        File.ReadAllLines(fn)

    let sourceForPropertiesOfType t =
        t
        |> AutoFluent.propertiesOfType
        |> Generate.typeProperties
        |> Generate.sourceLines

    let defaultDLLs = 
        [|
            "System.Runtime.dll"
            "System.ObjectModel.dll"
            "System.dll"
            "System.IO.dll"
            "System.Threading.Tasks.dll"
        |]

    let compileAndDumpSource (assembly: Assembly) (dependentDlls : string list) source = 
        let source = Syntax.join "\n" (source |> Seq.toList)
        use codeProvider = new CSharpCodeProvider()
        let parameters = CompilerParameters()
        // /filealign:512"
        // filealign seems to be already set to 512
        parameters.CompilerOptions <- "/optimize" 
        parameters.WarningLevel <- 4
        let refs = parameters.ReferencedAssemblies
        refs.Add(assembly.GetName().Name + ".dll") |> ignore
        refs.AddRange(defaultDLLs)
        refs.AddRange(dependentDlls |> List.toArray)
        let results = codeProvider.CompileAssemblyFromSource(parameters, [|source|])
        if results.Errors.Count <> 0 then
            for err in results.Errors do
                printfn "ERROR: Line %d, Error %s: %s" err.Line err.ErrorNumber err.ErrorText
            System.Console.Write source
            failwith "COMPILATION ERROR"
        else
        let assembly = results.CompiledAssembly
        let filepath = Uri(assembly.CodeBase).AbsolutePath
        System.Console.Write source
        FileInfo(filepath).Length

[<TestFixture>]
type AutoFluentTests() =

    [<Test; Category("LongRunning")>] 
    member this.xamarinForms() = 
        let assembly = "Xamarin.Forms.Core" |> Assembly.Load
        AutoFluent.propertiesOfAssembly assembly
        |> Generate.assembly
        |> Generate.sourceLines
        |> compileAndDumpSource assembly []
        |> should equal 30208


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
        |> AutoFluent.propertiesOfAssembly
        |> Generate.assembly
        |> Generate.sourceLines
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
            ]
        |> should equal 130560
    
    [<Test>]
    member this.formatInsertsEmptyLineBetweenBlocks() = 
    
        let c = 
            Block [
                Block [Line "a"]
                Block [Line "b"]
            ]

        let formatted = Generate.format c
        formatted |> should equal (Block [Block[Line "a"]; Line ""; Block[Line "b"]])

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
