namespace AutoFluent.Command

open AutoFluent

open Generate
open CompilationHelper

open System
open System.IO
open System.Reflection

module Program =

    let autoFluentDLL filename =
        printfn "Loading assembly %s" filename
        let fullPath = Path.GetFullPath filename
        let assembly = Assembly.LoadFile fullPath
        printfn "Generating code"
        let code = assembly |> Generate.fluentAssembly
        printfn "Formatting source"
        let source = code |> Format.sourceLines
        printfn "Compiling"
        let generatedAssemblyName = assembly.GetName().Name + ".AutoFluent"
        let assembly = compileToAssembly [filename] generatedAssemblyName source

        let extension = Path.GetExtension filename
        let outputFilename = 
            Path.GetFileNameWithoutExtension filename
            |> fun s -> s + ".AutoFluent"
            |> fun s -> s + extension

        printfn "Moving assembly to %s" outputFilename
        let codeBase = assembly.CodeBase
        let uri = Uri(codeBase, UriKind.Absolute)
        File.Move(uri.AbsolutePath, outputFilename)

    let protectedMain (argv : string list) = 
        match argv with
        | [str] ->
            autoFluentDLL str
        | _ ->
            failwith "AutoFluent requires one command line argument, the file name of the assembly to process."

    [<EntryPoint>]
    let main (argv : string array) = 

        try
            protectedMain(argv |> Array.toList)
            0
        with e ->
            // the message could also be an informative message, like that the number
            // of parameters is wrong
            Console.WriteLine("AutoFluent: " + e.Message)
            5
