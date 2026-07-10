@echo off
setlocal

echo =====================================
echo Hermes Release Build
echo =====================================

set ROOT=%~dp0
cd /d "%ROOT%"

set OUTPUT=%ROOT%release


echo.
echo [1/4] Cleaning old release folder...

if exist "%OUTPUT%" (
    rmdir /s /q "%OUTPUT%"
)

mkdir "%OUTPUT%"


echo.
echo [2/4] Restoring packages...

call dotnet restore

if %errorlevel% neq 0 (
    echo.
    echo Restore failed!
    pause
    exit /b 1
)


echo.
echo [3/4] Building Release...

call dotnet build ^
    -c Release ^
    --no-restore

if %errorlevel% neq 0 (
    echo.
    echo Build failed!
    pause
    exit /b 1
)


echo.
echo [4/4] Publishing...

call dotnet publish ^
    -c Release ^
    -o "%OUTPUT%" ^
    --no-restore


if %errorlevel% neq 0 (
    echo.
    echo Publish failed!
    pause
    exit /b 1
)


echo.
echo =====================================
echo Build completed successfully!
echo Output:
echo %OUTPUT%
echo =====================================

pause