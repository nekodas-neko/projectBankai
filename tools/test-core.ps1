$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
dotnet test (Join-Path $root 'tests\Game.Core.DotNetTests\Game.Core.DotNetTests.csproj') --nologo -v q
exit $LASTEXITCODE
