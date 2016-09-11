echo "build: Build started"

Push-Location $PSScriptRoot

if(Test-Path .\artifacts) {
        echo "build: Cleaning .\artifacts"
        Remove-Item .\artifacts -Force -Recurse
}

& dotnet restore --no-cache

$branch = @{ $true = $env:APPVEYOR_REPO_BRANCH; $false = $(git symbolic-ref --short -q HEAD) }[$env:APPVEYOR_REPO_BRANCH -ne $NULL];
$branch = $branch.Substring(0, [math]::Min(5, $branch.Length))
$revision = @{ $true = "{0:00000}" -f [convert]::ToInt32("0" + $env:APPVEYOR_BUILD_NUMBER, 10); $false = "l" }[$env:APPVEYOR_BUILD_NUMBER -ne $NULL];
$suffix = @{ $true = ""; $false = "$($branch.Substring(0, [math]::Min(10,$branch.Length)))-$revision"}[$branch -eq "master" -and $revision -ne "l"]

echo "build: Version suffix is $suffix"

foreach ($src in ls src/*) {
    Push-Location $src

        echo "build: Packaging project in $src"

    & dotnet pack -c Release -o ..\..\artifacts --version-suffix=$suffix
    if($LASTEXITCODE -ne 0) { exit 1 }

    Pop-Location
}

foreach ($test in ls test/*.Tests) {
    Push-Location $test

        echo "build: Testing project in $test"

    & dotnet test -c Release
    if($LASTEXITCODE -ne 0) { exit 3 }

    Pop-Location
}

Pop-Location