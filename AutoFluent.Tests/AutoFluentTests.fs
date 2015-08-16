namespace Melt.Tests

open System.Reflection
open NUnit.Framework
open AutoFluent

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
        
