namespace AutoFluent.Tests

open System
open AutoFluent.Syntax

open NUnit.Framework
open FsUnit

type SimpleTypeConstraint<'T when 'T :> Exception>() =
    class end

[<TestFixture>]
type SyntaxTests() =

    [<Test>] 
    member this.simpleTypeConstraint() =
        let r = typeConstraints typedefof<SimpleTypeConstraint<_>> |> List.map string
        r |> should equal ["where T : System.Exception"]
         
    [<Test>]
    member this.genericTypeName() = 
        let r = typeName typedefof<List<_>> |> string
        r |> should equal "Microsoft.FSharp.Collections.FSharpList<T>"

