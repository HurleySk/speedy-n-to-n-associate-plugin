@echo off
echo Building Speedy N:N Associate Plugin...
echo.
echo Restoring NuGet packages...
dotnet restore
echo.
echo Building project...
dotnet build --configuration Release
echo.
echo Build complete!
echo.
echo To install the plugin:
echo 1. Copy the DLL from bin\Release\net48\ to your XRM ToolBox plugins folder
echo 2. Restart XRM ToolBox
echo 3. Look for "Speedy N:N Associate" in the plugins list
echo.
pause
