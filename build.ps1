# Command-line builds on Windows. Usage: ./build.ps1 android-dev|android-release|ios|tests|validate|setup
param([Parameter(Mandatory=$true)][string]$Target)
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Version = (Select-String -Path "$Root/ProjectSettings/ProjectVersion.txt" -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value
$Unity = if ($env:UNITY) { $env:UNITY } else { "C:\Program Files\Unity\Hub\Editor\$Version\Editor\Unity.exe" }
if (-not (Test-Path $Unity)) { throw "Unity $Version not found; set `$env:UNITY" }
New-Item -ItemType Directory -Force -Path "$Root/Logs","$Root/TestResults" | Out-Null
function Run($log, [string[]]$extra) {
  & $Unity -batchmode -nographics -projectPath $Root -logFile "$Root/Logs/$log.log" @extra | Out-Default
  if ($LASTEXITCODE -ne 0) { throw "Unity exited with $LASTEXITCODE (see Logs/$log.log)" }
}
switch ($Target) {
  "setup"           { Run "setup" @("-quit","-executeMethod","Game.EditorTools.ProjectSetup.RunAllBatch") }
  "validate"        { Run "validate" @("-quit","-executeMethod","Game.EditorTools.ConfigValidatorMenu.ValidateBatch") }
  "tests"           { Run "tests-edit" @("-runTests","-testPlatform","EditMode","-testResults","$Root/TestResults/editmode.xml")
                      Run "tests-play" @("-runTests","-testPlatform","PlayMode","-testResults","$Root/TestResults/playmode.xml") }
  "android-dev"     { Run "build-android-dev" @("-quit","-executeMethod","Game.EditorTools.BuildScript.BuildAndroidDev") }
  "android-release" { Run "build-android-release" @("-quit","-executeMethod","Game.EditorTools.BuildScript.BuildAndroidRelease") }
  "ios"             { Run "build-ios" @("-quit","-executeMethod","Game.EditorTools.BuildScript.BuildIos") }
  default           { throw "usage: build.ps1 setup|validate|tests|android-dev|android-release|ios" }
}
