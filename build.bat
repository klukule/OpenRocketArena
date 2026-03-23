@echo off
setlocal enabledelayedexpansion

set ROOT=%~dp0
set OUTPUT=%ROOT%Output
set VCPKG_ROOT=C:\vcpkg

echo ============================================
echo  OpenRocketArena - Build All
echo ============================================
echo.

:: Clean output
if exist "%OUTPUT%" rmdir /s /q "%OUTPUT%"
mkdir "%OUTPUT%"
mkdir "%OUTPUT%\Backend"

:: ----------------------------------------
:: 1. Install vcpkg packages
:: ----------------------------------------
echo [1/5] Installing vcpkg packages...
if not exist "%VCPKG_ROOT%\vcpkg.exe" (
    echo ERROR: vcpkg not found at %VCPKG_ROOT%
    echo Set VCPKG_ROOT environment variable or install vcpkg to C:\vcpkg
    exit /b 1
)
"%VCPKG_ROOT%\vcpkg" install detours:x64-windows-static zydis:x64-windows-static
if errorlevel 1 (
    echo ERROR: vcpkg install failed
    exit /b 1
)
echo.

:: ----------------------------------------
:: 2. Build ClientDLL
:: ----------------------------------------
echo [2/5] Building MarinerClient.dll...
if exist "%ROOT%Client\ClientDLL\build" rmdir /s /q "%ROOT%Client\ClientDLL\build"
mkdir "%ROOT%Client\ClientDLL\build"
pushd "%ROOT%Client\ClientDLL\build"
cmake .. -G "Visual Studio 17 2022" -A x64 -DCMAKE_TOOLCHAIN_FILE="%VCPKG_ROOT%\scripts\buildsystems\vcpkg.cmake" -DVCPKG_TARGET_TRIPLET=x64-windows-static
if errorlevel 1 ( echo ERROR: CMake configure failed for ClientDLL & exit /b 1 )
cmake --build . --config Release
if errorlevel 1 ( echo ERROR: Build failed for ClientDLL & exit /b 1 )
popd
copy "%ROOT%Client\ClientDLL\build\bin\Release\MarinerClient.dll" "%OUTPUT%\MarinerClient.dll"
echo.

:: ----------------------------------------
:: 3. Build ServerDLL
:: ----------------------------------------
echo [3/5] Building MarinerServer.dll...
if exist "%ROOT%Client\ServerDLL\build" rmdir /s /q "%ROOT%Client\ServerDLL\build"
mkdir "%ROOT%Client\ServerDLL\build"
pushd "%ROOT%Client\ServerDLL\build"
cmake .. -G "Visual Studio 17 2022" -A x64 -DCMAKE_TOOLCHAIN_FILE="%VCPKG_ROOT%\scripts\buildsystems\vcpkg.cmake" -DVCPKG_TARGET_TRIPLET=x64-windows-static
if errorlevel 1 ( echo ERROR: CMake configure failed for ServerDLL & exit /b 1 )
cmake --build . --config Release
if errorlevel 1 ( echo ERROR: Build failed for ServerDLL & exit /b 1 )
popd
copy "%ROOT%Client\ServerDLL\build\bin\Release\MarinerServer.dll" "%OUTPUT%\MarinerServer.dll"
echo.

:: ----------------------------------------
:: 4. Build Bootstrapper (self-contained, trimmed)
:: ----------------------------------------
echo [4/5] Building Bootstrapper...
pushd "%ROOT%Client\Bootstrapper"
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%ROOT%Client\Bootstrapper\publish"
if errorlevel 1 ( echo ERROR: Bootstrapper build failed & exit /b 1 )
popd
copy "%ROOT%Client\Bootstrapper\publish\Mariner.exe" "%OUTPUT%\Mariner.exe"
copy "%ROOT%Client\Bootstrapper\publish\Mariner.exe" "%OUTPUT%\Launch_RocketArena.exe"
echo.

:: ----------------------------------------
:: 5. Build Backend (self-contained, single-file, NO trimming)
:: ----------------------------------------
echo [5/5] Building Backend...
pushd "%ROOT%Server"
dotnet publish OpenRocketArena.Server.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "%ROOT%Server\publish"
if errorlevel 1 ( echo ERROR: Backend build failed & exit /b 1 )
popd
xcopy "%ROOT%Server\publish\*" "%OUTPUT%\Backend\" /s /e /y /q
:: Copy wwwroot for CMS data
xcopy "%ROOT%Server\wwwroot\*" "%OUTPUT%\Backend\wwwroot\" /s /e /y /q
echo.

:: ----------------------------------------
:: 6. Copy config files
:: ----------------------------------------
echo Copying config files...
copy "%ROOT%Client\Configs\Overrides.ini" "%OUTPUT%\Overrides.ini"
copy "%ROOT%Client\Configs\ClientOverrides.ini" "%OUTPUT%\ClientOverrides.ini"
copy "%ROOT%Client\Configs\ServerOverrides.ini" "%OUTPUT%\ServerOverrides.ini"
copy "%ROOT%Client\Configs\LaunchClient.bat" "%OUTPUT%\LaunchClient.bat"
copy "%ROOT%Client\Configs\LaunchServer.bat" "%OUTPUT%\LaunchServer.bat"
copy "%ROOT%Client\Configs\LaunchBackend.bat" "%OUTPUT%\LaunchBackend.bat"
echo.

:: ----------------------------------------
:: Done
:: ----------------------------------------
echo ============================================
echo  Build complete! Output: %OUTPUT%
echo ============================================
echo.
echo Contents:
dir /b "%OUTPUT%"
echo.
echo Backend:
dir /b "%OUTPUT%\Backend\*.exe"
