@echo off
title Miya Island Build
echo Building Miya Island. Please wait...
where dotnet >nul 2>nul
if errorlevel 1 goto nodotnet
dotnet publish "%~dp0MiyaIsland.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
if errorlevel 1 goto failed
echo.
echo Build completed successfully.
explorer "%~dp0bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"
pause
exit /b 0

:nodotnet
echo.
echo .NET 8 SDK was not found. Install the .NET desktop development workload in Visual Studio.
pause
exit /b 1

:failed
echo.
echo Build failed. Please take a screenshot of this window.
pause
exit /b 1
