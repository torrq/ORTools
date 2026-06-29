@echo off
REM dotnet clean ORTools.sln
REM dotnet build ORTools.sln
dotnet publish ORTools.UI\ORTools.UI.csproj -c Release /p:PublishSingleFile=true /p:SelfContained=false
pause
