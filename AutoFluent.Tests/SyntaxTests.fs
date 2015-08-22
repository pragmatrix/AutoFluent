namespace AutoFluent.Tests

open System

open AutoFluent.Syntax
open AutoFluent.Reflection

open NUnit.Framework
open FsUnit

type SimpleTypeConstraint<'T when 'T :> Exception>() =
    class end

[<TestFixture>]
type SyntaxTests() =

    [<Test>] 
    member this.simpleTypeConstraint() =
        let r = 
            typedefof<SimpleTypeConstraint<_>>
            |> typeConstraints  
            |> List.map string
        r |> should equal ["where T : System.Exception"]
         
    [<Test>]
    member this.genericTypeName() = 
        let r = 
            typedefof<List<_>> 
            |> typeName |> string
        r |> should equal "Microsoft.FSharp.Collections.FSharpList<T>"

