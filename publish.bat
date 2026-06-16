@echo off
dotnet publish ORTools.UI\ORTools.UI.csproj -c Release /p:PublishSingleFile=true /p:SelfContained=false
pause
