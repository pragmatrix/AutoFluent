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


[<AutoOpen>]
module Helper = 
    let loadLines fn = 
        File.ReadAllLines(fn)

[<TestFixture>]
type Tests() =

    [<Test>] 
    member this.loadAssembly() = 
        let assemblyToLoad = "Xamarin.Forms.Core" |> Assembly.Load
        let assembly = AutoFluent.propertiesOfAssembly assemblyToLoad
        assembly
        |> Generate.assembly
        |> Generate.sourceLines
        |> Seq.map System.Console.WriteLine
        
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
        let t = typeof<TypeWithGenericProperty>
        let fluent = AutoFluent.propertiesOfType t
        let code = Generate.typeProperties fluent
        let code = Generate.sourceLines code
        let file = loadLines "TypeWithGenericProperty.cs"
        code |> System.Console.WriteLine
        ()
        

