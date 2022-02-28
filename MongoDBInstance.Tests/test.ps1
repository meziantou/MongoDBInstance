# Clear package from global cache
Remove-Item $PSScriptRoot/../local_packages/MongoDBInstance -Recurse -Force

# build package
dotnet pack ../MongoDBInstance/MongoDBInstance.csproj /p:Version=9999.9.9

# Build test project and ensure it uses the latest package version
dotnet build --force --no-cache
dotnet test