build:
    dotnet build ngo.sln

test:
    dotnet test ngo.sln

test-unit:
    dotnet test tests/Ngo.Compiler.Tests/Ngo.Compiler.Tests.csproj

test-build:
    dotnet test tests/Ngo.BuildTests/Ngo.BuildTests.csproj

test-runtime:
    dotnet test tests/Ngo.Runtime.Tests/Ngo.Runtime.Tests.csproj
