namespace AutoFluent.Tests

open System
open AutoFluent.RoslynHelper

open NUnit.Framework
open FsUnit

type SimpleTypeConstraint<'T when 'T :> Exception>() =
    class end

[<TestFixture>]
type CodeGenerationTests() =

    [<Test>] 
    member this.simpleTypeConstraint() =
        let c = typeConstraints typedefof<SimpleTypeConstraint<_>>
        c |> should equal "where T : System.Exception"
         


