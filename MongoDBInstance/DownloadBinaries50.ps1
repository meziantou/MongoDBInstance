﻿$ErrorActionPreference = 'Stop'

$Version = "5.0.6"
$binariesFolder = "$PSScriptRoot/binaries/";

$WindowsDownloadUrl = "https://fastdl.mongodb.org/windows/mongodb-windows-x86_64-$Version.zip"
$Ubuntu1804DownloadUrl = "https://fastdl.mongodb.org/linux/mongodb-linux-x86_64-ubuntu1804-$Version.tgz"
$Ubuntu2004DownloadUrl = "https://fastdl.mongodb.org/linux/mongodb-linux-x86_64-ubuntu2004-$Version.tgz"
$MacOSDownloadUrl = "https://fastdl.mongodb.org/osx/mongodb-macos-x86_64-$Version.tgz"

$WindowsFileName = "mongod-$Version-win-x64.exe";
$Ubuntu1804FileName = "mongod-$Version-ubuntu1804-x64";
$Ubuntu2004FileName = "mongod-$Version-ubuntu2004-x64";
$MacOSFileName = "mongod-$Version-macos-x64";

function DownloadWindowsVersion($uri, $outputFileName) {
  $outputFile = Join-Path $binariesFolder $outputFileName

  $archiveFile = New-TemporaryFile
  $extractionFolder = $archiveFile.FullName + "extracted"
  Invoke-WebRequest -Uri $uri -OutFile $archiveFile
  Expand-Archive $archiveFile -DestinationPath $extractionFolder -Force
  
  Move-Item (Get-ChildItem $extractionFolder -Recurse -Filter "mongod.exe") -Destination $outputFile -Force
  
  Remove-Item $archiveFile -Force
  Remove-Item $extractionFolder -Recurse -Force
}

function DownloadUnixVersion($uri, $outputFileName) {
  if ([System.OperatingSystem]::IsWindows()) {
    if (-not (Get-Command Expand-7Zip -ErrorAction Ignore)) {
      Install-Package -Scope CurrentUser -PackageManagementProvider PowerShellGet -Force 7Zip4PowerShell > $null
    }
  }

  $outputFile = Join-Path $binariesFolder $outputFileName
  $archiveFile = New-TemporaryFile
  $extractionFolder = $archiveFile.FullName + "extracted"
  Invoke-WebRequest -Uri $uri -OutFile $archiveFile

  if ([System.OperatingSystem]::IsWindows()) {
    Expand-7Zip -ArchiveFileName $archiveFile -TargetPath $extractionFolder                      # gzip
    Expand-7Zip -ArchiveFileName (Get-ChildItem $extractionFolder) -TargetPath $extractionFolder # tar
  }
  else {
    New-Item -ItemType Directory -Force -Path $extractionFolder
    & tar -xvf $archiveFile --directory $extractionFolder
  }

  Move-Item (Get-ChildItem $extractionFolder -Recurse -Filter "mongod") -Destination $outputFile -Force

  Remove-Item $archiveFile -Force
  Remove-Item $extractionFolder -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $binariesFolder

DownloadWindowsVersion $WindowsDownloadUrl $WindowsFileName
DownloadUnixVersion $Ubuntu1804DownloadUrl $Ubuntu1804FileName
DownloadUnixVersion $Ubuntu2004DownloadUrl $Ubuntu2004FileName
DownloadUnixVersion $MacOSDownloadUrl $MacOSFileName

# Write the version to a file
Set-Content -Path $binariesFolder/../MongoDBInstance.path.cs -Value ("
// <auto-generated />
#nullable enable
namespace MongoDBInstances;
partial class MongoDBInstance
{
    private const string DefaultMongoDBVersion = """ + $Version + """;
    private const string MongoDBWindowsFileName = """ + $WindowsFileName + """;
    private const string? MongoDBUbuntu1604Filename = null;
    private const string MongoDBUbuntu1804Filename = """ + $Ubuntu1804FileName + """;
    private const string MongoDBUbuntu2004Filename = """ + $Ubuntu2004FileName + """;
    private const string MongoDBMacOSFilename = """ + $MacOSFileName + """;
}
")
