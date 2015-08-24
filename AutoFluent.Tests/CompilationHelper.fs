namespace AutoFluent.Tests

module CompilationHelper = 
    open System.IO
    open System.Reflection
    open AutoFluent
    open Microsoft.CSharp
    open System.CodeDom.Compiler
    open System

    let loadLines fn = 
        File.ReadAllLines(fn)

    let sourceForPropertiesOfType t =
        t
        |> Generate.fluentTypeProperties
        |> Format.sourceLines
        |> Seq.toArray

    let defaultDLLs = 
        [|
            "System.Runtime.dll"
            "System.ObjectModel.dll"
            "System.dll"
            "System.IO.dll"
            "System.Threading.Tasks.dll"
        |]

    let compileToAssembly (dependentDlls : string list) source = 
        let source = Syntax.join "\n" source
        use codeProvider = new CSharpCodeProvider()
        let parameters = CompilerParameters()
        // /filealign:512"
        // filealign seems to be already set to 512
        parameters.CompilerOptions <- "/optimize" 
        parameters.WarningLevel <- 4
        let refs = parameters.ReferencedAssemblies
        refs.AddRange(defaultDLLs)
        refs.AddRange(dependentDlls |> List.toArray)
        let results = codeProvider.CompileAssemblyFromSource(parameters, [|source|])
        if results.Errors.Count <> 0 then
            for err in results.Errors do
                printfn "ERROR: Line %d, Error %s: %s" err.Line err.ErrorNumber err.ErrorText
            System.Console.Write source
            failwith "COMPILATION ERROR"
        else
        results.CompiledAssembly

    let compileAndDumpSource (assembly: Assembly) (dependentDlls : string list) (source: string seq) = 
        let dependencies = 
            [
                yield assembly.GetName().Name + ".dll"
                yield! dependentDlls
            ]

        let assembly = 
            source
            |> compileToAssembly dependencies

        let filepath = Uri(assembly.CodeBase).AbsolutePath
        System.Console.Write (Syntax.join "\n" source)
        FileInfo(filepath).Length

