/// [omit]
module Paket.Program

open System
open Nessos.UnionArgParser
open Paket.Logging
open System.Diagnostics
open System.Reflection
open System.IO

let private stopWatch = new Stopwatch()
stopWatch.Start()

let assembly = Assembly.GetExecutingAssembly()
let fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
tracefn "Paket version %s" fvi.FileVersion

type Command =
    | Init
    | Add
    | Remove
    | Install
    | Restore
    | Update
    | Outdated
    | ConvertFromNuget
    | InitAutoRestore
    | Simplify
    | FindRefs
    | Unknown

type CLIArguments =
    | [<First>][<NoAppSettings>][<CustomCommandLine("init")>] Init
    | [<First>][<NoAppSettings>][<CustomCommandLine("add")>] Add
    | [<First>][<NoAppSettings>][<CustomCommandLine("remove")>] Remove
    | [<First>][<NoAppSettings>][<CustomCommandLine("install")>] Install
    | [<First>][<NoAppSettings>][<CustomCommandLine("restore")>] Restore
    | [<First>][<NoAppSettings>][<CustomCommandLine("update")>] Update
    | [<First>][<NoAppSettings>][<CustomCommandLine("outdated")>] Outdated
    | [<First>][<NoAppSettings>][<CustomCommandLine("convert-from-nuget")>] ConvertFromNuget
    | [<First>][<NoAppSettings>][<CustomCommandLine("init-auto-restore")>] InitAutoRestore
    | [<First>][<NoAppSettings>][<CustomCommandLine("simplify")>] Simplify
    | [<First>][<NoAppSettings>][<Rest>][<CustomCommandLine("find-refs")>] FindRefs of string
    | [<AltCommandLine("-v")>] Verbose
    | [<AltCommandLine("-i")>] Interactive
    | [<AltCommandLine("-f")>] Force
    | Hard
    | [<CustomCommandLine("nuget")>] Nuget of string
    | [<CustomCommandLine("version")>] Version of string
    | [<Rest>]References_Files of string
    | No_Install
    | Ignore_Constraints
    | [<AltCommandLine("--pre")>] Include_Prereleases
    | No_Auto_Restore
    | Creds_Migration of string
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Init -> "creates dependencies file."
            | Add -> "adds a package to the dependencies."
            | Remove -> "removes a package from the dependencies."
            | Install -> "installs all packages."
            | Restore -> "restores all packages."
            | Update -> "updates the paket.lock file and installs all packages."
            | References_Files _ -> "allows to specify a list of references file names."
            | Outdated -> "displays information about new packages."
            | ConvertFromNuget -> "converts all projects from NuGet to Paket."
            | InitAutoRestore -> "enables automatic restore for Visual Studio."
            | Simplify -> "analyzes dependencies and removes unnecessary indirect dependencies."
            | Verbose -> "displays verbose output."
            | Force -> "forces the download of all packages."
            | Interactive -> "interactive process."
            | Hard -> "overwrites manual package references."
            | No_Install -> "omits install --hard after convert-from-nuget."
            | Ignore_Constraints -> "ignores the version requirements when searching for outdated packages."
            | Include_Prereleases -> "includes prereleases when searching for outdated packages."
            | No_Auto_Restore -> "omits init-auto-restore after convert-from-nuget."
            | Nuget _ -> "allows to specify a nuget package."
            | Version _ -> "allows to specify a package version."
            | Creds_Migration _ -> "allows to specify credentials migration mode for convert-from-nuget."
            | FindRefs _ -> "finds all references to the given packages."

let parser = UnionArgParser.Create<CLIArguments>("USAGE: paket [add|remove|install|update|outdated|convert-from-nuget|init-auto-restore|simplify|find-refs] ... options")
 
let results =
    try
        let results = parser.Parse()
        let command = 
            if results.Contains <@ CLIArguments.Init @> then Command.Init
            elif results.Contains <@ CLIArguments.Add @> then Command.Add
            elif results.Contains <@ CLIArguments.Remove @> then Command.Remove
            elif results.Contains <@ CLIArguments.Install @> then Command.Install
            elif results.Contains <@ CLIArguments.Restore @> then Command.Restore
            elif results.Contains <@ CLIArguments.Update @> then Command.Update
            elif results.Contains <@ CLIArguments.Outdated @> then Command.Outdated
            elif results.Contains <@ CLIArguments.ConvertFromNuget @> then Command.ConvertFromNuget
            elif results.Contains <@ CLIArguments.InitAutoRestore @> then Command.InitAutoRestore
            elif results.Contains <@ CLIArguments.Simplify @> then Command.Simplify
            elif results.Contains <@ CLIArguments.FindRefs @> then Command.FindRefs
            else Command.Unknown
        if results.Contains <@ CLIArguments.Verbose @> then
            verbose <- true

        Some(command,results)
    with
    | _ ->
        tracefn "%s %s%s" (String.Join(" ",Environment.GetCommandLineArgs())) Environment.NewLine (parser.Usage())
        None

try
    match results with
    | Some(command,results) ->
        let force = results.Contains <@ CLIArguments.Force @> 
        let interactive = results.Contains <@ CLIArguments.Interactive @> 
        let hard = results.Contains <@ CLIArguments.Hard @> 
        let noInstall = results.Contains <@ CLIArguments.No_Install @>
        let noAutoRestore = results.Contains <@ CLIArguments.No_Auto_Restore @>
        let includePrereleases = results.Contains <@ CLIArguments.Include_Prereleases @>

        match command with
        | Command.Init -> Dependencies.Create() |> ignore
        | Command.Add -> 
            let packageName = results.GetResult <@ CLIArguments.Nuget @>
            let version = 
                match results.TryGetResult <@ CLIArguments.Version @> with
                | Some x -> x
                | _ -> ""

            Dependencies.Locate().Add(packageName, version, force, hard, interactive, noInstall |> not)
        | Command.Remove -> 
            let packageName = results.GetResult <@ CLIArguments.Nuget @>            
            Dependencies.Locate().Remove(packageName,force,hard,interactive,noInstall |> not)
        | Command.Install -> Dependencies.Locate().Install(force,hard)
        | Command.Restore -> 
            let files = results.GetResults <@ CLIArguments.References_Files @> 
            Dependencies.Locate().Restore(force,files)
        | Command.Update -> 
            match results.TryGetResult <@ CLIArguments.Nuget @> with
            | Some packageName -> 
                let version = results.TryGetResult <@ CLIArguments.Version @>
                Dependencies.Locate().UpdatePackage(packageName, version, force, hard)
            | _ -> Dependencies.Locate().Update(force,hard)            
        | Command.Outdated ->         
            let strict = results.Contains <@ CLIArguments.Ignore_Constraints @> |> not
            Dependencies.Locate().ShowOutdated(strict,includePrereleases)
        | Command.InitAutoRestore -> Dependencies.Locate().InitAutoRestore()
        | Command.ConvertFromNuget -> 
            let credsMigrationMode = results.TryGetResult <@ CLIArguments.Creds_Migration @>
            Dependencies.ConvertFromNuget(force, noInstall |> not, noAutoRestore |> not, credsMigrationMode)
        | Command.Simplify -> Dependencies.Locate().Simplify(interactive)
        | Command.FindRefs ->
            let packages = results.GetResults <@ CLIArguments.FindRefs @>
            Dependencies.Locate().ShowReferencesFor(packages)
        | Command.Unknown -> traceErrorfn "no command given.%s" (parser.Usage())
        
        let elapsedTime = Utils.TimeSpanToReadableString stopWatch.Elapsed

        tracefn "%s - ready." elapsedTime
    | None -> ()
with
| exn -> 
    Environment.ExitCode <- 1
    traceErrorfn "Paket failed with:%s   %s" Environment.NewLine exn.Message

    if verbose then
        traceErrorfn "StackTrace:%s  %s" Environment.NewLine exn.StackTrace
