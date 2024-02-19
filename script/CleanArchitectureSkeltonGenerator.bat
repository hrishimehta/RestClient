@echo off
set SolutionName=RestClient
set SrcDir=src
set TestsDir=tests

set APIProjectName=%SolutionName%.API
set ClientProjectName=%SolutionName%.Client
set MongoProjectName=%SolutionName%.Mongo

echo Creating solution %SolutionName%...
dotnet new sln -n %SolutionName%

echo Creating source and test project directories...
mkdir %SrcDir%
mkdir %TestsDir%

echo Creating projects...
dotnet new webapi -n %APIProjectName% -o %SrcDir%\%APIProjectName% --no-restore
dotnet new classlib -n %SolutionName%.Domain -o %SrcDir%\%SolutionName%.Domain --no-restore
dotnet new classlib -n %SolutionName%.Infrastructure -o %SrcDir%\%SolutionName%.Infrastructure --no-restore
dotnet new classlib -n %SolutionName%.Shared -o %SrcDir%\%SolutionName%.Shared --no-restore
dotnet new classlib -n %ClientProjectName% -o %SrcDir%\%ClientProjectName% --no-restore
dotnet new classlib -n %MongoProjectName% -o %SrcDir%\%MongoProjectName% --no-restore

dotnet new xunit -n %SolutionName%.API.Tests -o %TestsDir%\%SolutionName%.API.Tests --no-restore
dotnet new xunit -n %SolutionName%.Domain.Tests -o %TestsDir%\%SolutionName%.Domain.Tests --no-restore
dotnet new xunit -n %SolutionName%.Infrastructure.Tests -o %TestsDir%\%SolutionName%.Infrastructure.Tests --no-restore
dotnet new xunit -n %SolutionName%.Shared.Tests -o %TestsDir%\%SolutionName%.Shared.Tests --no-restore
dotnet new xunit -n %SolutionName%.Client.Tests -o %TestsDir%\%SolutionName%.Client.Tests --no-restore
dotnet new xunit -n %SolutionName%.Mongo.Tests -o %TestsDir%\%SolutionName%.Mongo.Tests --no-restore

echo Adding projects to solution...
dotnet sln %SolutionName%.sln add %SrcDir%\%APIProjectName%\%APIProjectName%.csproj
dotnet sln %SolutionName%.sln add %SrcDir%\%SolutionName%.Domain\%SolutionName%.Domain.csproj
dotnet sln %SolutionName%.sln add %SrcDir%\%SolutionName%.Infrastructure\%SolutionName%.Infrastructure.csproj
dotnet sln %SolutionName%.sln add %SrcDir%\%SolutionName%.Shared\%SolutionName%.Shared.csproj
dotnet sln %SolutionName%.sln add %SrcDir%\%ClientProjectName%\%ClientProjectName%.csproj
dotnet sln %SolutionName%.sln add %SrcDir%\%MongoProjectName%\%MongoProjectName%.csproj

dotnet sln %SolutionName%.sln add %TestsDir%\%SolutionName%.API.Tests\%SolutionName%.API.Tests.csproj
dotnet sln %SolutionName%.sln add %TestsDir%\%SolutionName%.Domain.Tests\%SolutionName%.Domain.Tests.csproj
dotnet sln %SolutionName%.sln add %TestsDir%\%SolutionName%.Infrastructure.Tests\%SolutionName%.Infrastructure.Tests.csproj
dotnet sln %SolutionName%.sln add %TestsDir%\%SolutionName%.Shared.Tests\%SolutionName%.Shared.Tests.csproj
dotnet sln %SolutionName%.sln add %TestsDir%\%SolutionName%.Client.Tests\%SolutionName%.Client.Tests.csproj
dotnet sln %SolutionName%.sln add %TestsDir%\%SolutionName%.Mongo.Tests\%SolutionName%.Mongo.Tests.csproj

echo Adding references between projects...
dotnet add %SrcDir%\%APIProjectName%\%APIProjectName%.csproj reference %SrcDir%\%SolutionName%.Domain\%SolutionName%.Domain.csproj
dotnet add %SrcDir%\%APIProjectName%\%APIProjectName%.csproj reference %SrcDir%\%SolutionName%.Infrastructure\%SolutionName%.Infrastructure.csproj
dotnet add %SrcDir%\%APIProjectName%\%APIProjectName%.csproj reference %SrcDir%\%SolutionName%.Shared\%SolutionName%.Shared.csproj

dotnet add %SrcDir%\%SolutionName%.Domain\%SolutionName%.Domain.csproj reference %SrcDir%\%SolutionName%.Shared\%SolutionName%.Shared.csproj

dotnet add %SrcDir%\%SolutionName%.Infrastructure\%SolutionName%.Infrastructure.csproj reference %SrcDir%\%SolutionName%.Shared\%SolutionName%.Shared.csproj
dotnet add %SrcDir%\%SolutionName%.Infrastructure\%SolutionName%.Infrastructure.csproj package MongoDB.Driver

dotnet add %SrcDir%\%ClientProjectName%\%ClientProjectName%.csproj reference %SrcDir%\%SolutionName%.Shared\%SolutionName%.Shared.csproj
dotnet add %SrcDir%\%ClientProjectName%\%ClientProjectName%.csproj reference %SrcDir%\%APIProjectName%\%APIProjectName%.csproj

dotnet add %TestsDir%\%SolutionName%.API.Tests\%SolutionName%.API.Tests.csproj reference %SrcDir%\%APIProjectName%\%APIProjectName%.csproj
dotnet add %TestsDir%\%SolutionName%.Domain.Tests\%SolutionName%.Domain.Tests.csproj reference %SrcDir%\%SolutionName%.Domain\%SolutionName%.Domain.csproj
dotnet add %TestsDir%\%SolutionName%.Infrastructure.Tests\%SolutionName%.Infrastructure.Tests.csproj reference %SrcDir%\%SolutionName%.Infrastructure\%SolutionName%.Infrastructure.csproj
dotnet add %TestsDir%\%SolutionName%.Shared.Tests\%SolutionName%.Shared.Tests.csproj reference %SrcDir%\%SolutionName%.Shared\%SolutionName%.Shared.csproj
dotnet add %TestsDir%\%SolutionName%.Client.Tests\%SolutionName%.Client.Tests.csproj reference %SrcDir%\%ClientProjectName%\%ClientProjectName%.csproj
dotnet add %TestsDir%\%SolutionName%.Mongo.Tests\%SolutionName%.Mongo.Tests.csproj reference %SrcDir%\%MongoProjectName%\%MongoProjectName%.csproj

echo Adding Serilog dependency...
dotnet add %SrcDir%\%APIProjectName%\%APIProjectName%.csproj package Serilog.AspNetCore

echo Setting IIS Express as the default launch profile for API project...
mkdir %SrcDir%\%APIProjectName%\.vs
mkdir %SrcDir%\%APIProjectName%\.vs\config
echo ^{^"profiles^": ^{^"IIS Express^": ^{^"commandName^": ^"IISExpress^"^^}^^}^} > %SrcDir%\%APIProjectName%\.vs\config\applicationhost.config

echo Creating necessary files in the projects...
REM Add your necessary files like Controllers, Services, Repositories, Models, etc. in each project.
REM ...

echo Script completed.
