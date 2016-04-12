#r @"packages/FAKE/tools/FakeLib.dll"

open Fake

RestorePackages()

// Properties
let buildDir = "./build/"
let testDir  = "./test/"
let deployDir = "./deploy/"
let toolPath="""C:\Program Files (x86)\NUnit 2.6.4\bin\"""

// version info
let version = "0.1"  // or retrieve from CI server

// Targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir; testDir; deployDir]
)

Target "BuildApp" (fun _ ->
   !! "src/**/*.csproj" 
     |> MSBuildRelease buildDir "Build"
     |> Log "AppBuild-Output: "
)

Target "BuildTest" (fun _ ->
    !! "tests/**/*.csproj"
      |> MSBuildDebug testDir "Build"
      |> Log "TestBuild-Output: "
)

Target "Test" (fun _ ->
    !! (testDir + "/CRG.Integration.Testing.Tests.dll")
      |> NUnit (fun p ->
          {p with ToolPath=toolPath;
                  DisableShadowCopy = true;
                  OutputFile = testDir + "TestResults.xml" })
)

Target "Zip" (fun _ ->
    !! (buildDir + "/**/*.*")
        -- "*.zip"
        |> Zip buildDir (deployDir + "CRG.Test." + version + ".zip")
)

Target "Default" (fun _ ->
    trace "Hello World from FAKE"
)


// Dependencies
"Clean"
  ==> "BuildApp"
  ==> "BuildTest"
//==> "Test"
  ==> "Zip"
  ==> "Default"

// start build
RunTargetOrDefault "Default"
