﻿namespace AutoFluent.Tests

open NUnit.Framework
open FsUnit

[<AutoOpen>]
module TestMethods = 

    open System.IO
    open CompilationHelper
    open AutoFluent

    let separationMarker = "//--"

    let splitInputOutput (lines : string seq) = 
        
        let lines = lines |> Seq.toArray
        let si = 
            lines
            |> Array.findIndex(fun s -> s.Trim() = separationMarker);

        (lines.[0..si-1], lines.[(si+1)..(lines.Length-1)])

    let testClassFile name = 
        let csFilename = name + ".cs"
        let lines = 
            File.ReadAllLines(csFilename)
            |> Seq.filter (fun l -> l.Trim() <> "")

        let (input, output) = splitInputOutput lines

        let assembly = compileToAssembly [] input
        let result = 
            Generate.fluentAssembly assembly
            |> Format.sourceLines
            |> Seq.filter (fun l -> l.Trim() <> "")
            |> Seq.toArray

        result 
        |> Seq.iter (System.Console.WriteLine)

        result |> should equal output

[<TestFixture>]
type CompilationTests() =

    [<Test>]
    member this.voidMethodWithNoParameters() = 
        testClassFile("VoidMethodWithNoParameters")

    [<Test>]
    member this.voidMethodWithOutParameter() =
        testClassFile("VoidMethodWithOutParameter")

    [<Test>]
    member this.voidMethodWithClassTAndArrayT() =
        testClassFile("VoidMethodWithClassTAndArrayT") 

    [<Test>]
    member this.voidMethodWithSealedClassT() =
        testClassFile("VoidMethodWithSealedClassT") 

    [<Test>]
    member this.derivedClassGetsExtensionMethodOfBaseProperty() =
        testClassFile("DerivedClassGetsExtensionMethodOfBaseProperty") 
    
    [<Test>]
    member this.propertyOfNestedClass() =
        testClassFile("PropertyOfNestedClass") 

    [<Test>]
    member this.genericInterfaceMethod() =
        testClassFile("GenericInterfaceMethod")