// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r @"packages/FAKE/tools/FakeLib.dll"

open System
open System.Collections.Generic
open System.IO

open Fake
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
 
// The name of the project
let project = "Shed"

// Short summary of the project
let summary = "A .NET runtime inspector."

// Longer description of the project
let description = summary

// List of author names (for NuGet package)
let authors = [ "Enkomio" ]

// File system information
let solutionFile  = "ShedSln.sln"

// Build dir
let buildDir = "./build"

// Package dir
let deployDir = "./deploy"

// Read additional information from the release notes document
let releaseNotesData = 
    let changelogFile = "RELEASE_NOTES.md"
    File.ReadAllLines(changelogFile)
    |> parseAllReleaseNotes

let releaseVersion = (List.head releaseNotesData)
trace("Build release: " + releaseVersion.AssemblyVersion)

let stable = 
    match releaseNotesData |> List.tryFind (fun r -> r.NugetVersion.Contains("-") |> not) with
    | Some stable -> stable
    | _ -> releaseVersion

let genFSAssemblyInfo (projectPath) =
    let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
    let folderName = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(projectPath))
    let fileName = folderName @@ "AssemblyInfo.fs"
    CreateFSharpAssemblyInfo fileName
      [ Attribute.Title (projectName)
        Attribute.Product project
        Attribute.Company (authors |> String.concat ", ")
        Attribute.Description summary
        Attribute.Version (releaseVersion.AssemblyVersion + ".*")
        Attribute.FileVersion (releaseVersion.AssemblyVersion + ".*")
        Attribute.InformationalVersion (releaseVersion.NugetVersion + ".*") ]
        
Target "Clean" (fun _ ->
    CleanDir buildDir
    ensureDirectory buildDir

    CleanDir deployDir
    ensureDirectory deployDir
)

Target "AssemblyInfo" (fun _ ->
  let fsProjs =  !! "*/**/*.fsproj"
  fsProjs |> Seq.iter genFSAssemblyInfo
)

Target "Compile" (fun _ ->
    let build(project: String, buildDir: String) =
        trace("Compile: " + project)
        let fileName = Path.GetFileNameWithoutExtension(project)
        let buildAppDir = Path.Combine(buildDir, fileName)
        ensureDirectory buildAppDir
        MSBuildRelease buildAppDir "Build" [project] |> Log "Build Output: "

    // build Shed
    build(Path.Combine("Shed", "Shed.fsproj"), buildDir)

    // build Tests
    let buildTestDir = Path.Combine(buildDir, "Examples")
    ensureDirectory buildTestDir
    [        
        Path.Combine("Tests", "ConfigurationSample", "ConfigurationSample.csproj")
        Path.Combine("Tests", "HelloWorld", "HelloWorld.csproj")
        Path.Combine("Tests", "LoadViaReflection", "LoadViaReflection.csproj")
    ]
    |> List.iter(fun project -> build(project, buildTestDir))
)

Target "Release" (fun _ ->
    let forbidden = [".pdb"]    
    !! (buildDir + "/**/*.*")         
    |> Seq.filter(fun f -> 
        forbidden 
        |> List.contains (Path.GetExtension(f).ToLowerInvariant())
        |> not
    )
    |> Zip buildDir (Path.Combine(deployDir, "Shed." + releaseVersion.AssemblyVersion + ".zip"))
)

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target "All" DoNothing

"Clean"
  ==> "AssemblyInfo"
  ==> "Compile"
  ==> "Release"
  ==> "All"

RunTargetOrDefault "All"