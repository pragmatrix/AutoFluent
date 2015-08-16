namespace Melt.Tests

open System.Reflection
open NUnit.Framework
open FsUnit

open AutoFluent

open Generate

[<TestFixture>]
type Tests() =

    [<Test>] 
    member this.loadAssembly() = 
        let assemblyToLoad = "Xamarin.Forms.Core" |> Assembly.Load
        let assembly = AutoFluent.assemblyProperties assemblyToLoad
        assembly
        |> Generate.assembly
        |> Generate.code
        |> System.Console.Write
        
    [<Test>]
    member this.formatInsertsEmptyLineBetweenBlocks() = 
    
        let c = 
            Block [
                Block [Line "a"]
                Block [Line "b"]
            ]

        let formatted = Generate.format c
        formatted |> should equal (Block [Block[Line "a"]; Line ""; Block[Line "b"]])
