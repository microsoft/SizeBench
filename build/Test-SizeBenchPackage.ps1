### Liberally copied from Windows Terminal here: https://github.com/microsoft/terminal/blob/main/build/scripts/Test-WindowsTerminalPackage.ps1

[CmdletBinding()]
Param(
    [Parameter(Mandatory=$true, ValueFromPipeline=$true,
      HelpMessage="Path to the .appx/.msix to validate")]
    [string]
    $Path,

    [Parameter(HelpMessage="Path to Windows Kit")]
    [ValidateScript({Test-Path $_ -Type Leaf})]
    [string]
    $WindowsKitPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0"
)

$ErrorActionPreference = "Stop"

If ($null -Eq (Get-Item $WindowsKitPath -EA:SilentlyContinue)) {
    Write-Error "Could not find a windows SDK at at `"$WindowsKitPath`".`nMake sure that WindowsKitPath points to a valid SDK."
    Exit 1
}

$makeAppx = "$WindowsKitPath\x86\MakeAppx.exe"

Function Expand-ApplicationPackage {
    Param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [string]
        $Path
    )

    $sentinelFile = New-TemporaryFile
    $directory = New-Item -Type Directory "$($sentinelFile.FullName)_Package"
    Remove-Item $sentinelFile -Force -EA:Ignore

    & $makeAppx unpack /p $Path /d $directory /nv /o

    If ($LastExitCode -Ne 0) {
        Throw "Failed to expand AppX"
    }

    $directory
}

Write-Verbose "Expanding $Path"
$AppxPackageRoot = Expand-ApplicationPackage $Path
$AppxPackageRootPath = $AppxPackageRoot.FullName

Write-Verbose "Expanded to $AppxPackageRootPath"

Try {
    If ($null -eq (Get-Item "$AppxPackageRootPath\SizeBench.GUI\CONTRIBUTORS" -EA:Ignore)) {
        Throw "Failed to find CONTRIBUTORS file -- check the WAP packaging project and the way Content files get added.  This is important to credit people's work and have a good About box."
    }

    If (($null -eq (Get-Item "$AppxPackageRootPath\SizeBench.GUI\msdia140.dll" -EA:Ignore)) -Or
        ($null -eq (Get-Item "$AppxPackageRootPath\SizeBench.GUI\Dia2Lib.dll" -EA:Ignore))){
        Throw "Failed to find msdia140.dll or Dia2Lib.dll -- check the WAP packaging project and the way Content files get added.  Without DIA, SizeBench can't do anything."
    }

    If (($null -eq (Get-Item "$AppxPackageRootPath\SizeBench.GUI\amd64\EngHost.exe" -EA:Ignore)) -Or
        ($null -eq (Get-Item "$AppxPackageRootPath\SizeBench.GUI\DbgX.dll" -EA:Ignore))) {
        Throw "Failed to find DbgX binaries -- check the WAP packaging project.  Without this, Template Foldability and disassembling will fail."
    }

} Finally {
    Remove-Item -Recurse -Force $AppxPackageRootPath
}