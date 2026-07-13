@echo off
setlocal

echo =====================================
echo Hermes Release Build
echo =====================================

set ROOT=%~dp0
cd /d "%ROOT%"

set OUTPUT=%ROOT%release
set PACKAGE_OUTPUT=%ROOT%release-package
set ARCHIVE=%PACKAGE_OUTPUT%\Hermes-release.zip
set CHECKSUM=%PACKAGE_OUTPUT%\Hermes-release.zip.sha256.txt


echo.
echo [1/5] Cleaning old release folder...

if exist "%OUTPUT%" (
    rmdir /s /q "%OUTPUT%"
)

mkdir "%OUTPUT%"


echo.
echo [2/5] Restoring packages...

call dotnet restore

if %errorlevel% neq 0 (
    echo.
    echo Restore failed!
    pause
    exit /b 1
)


echo.
echo [3/5] Building Release...

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
echo [4/5] Publishing...

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
echo [5/5] Packaging release and calculating SHA256...

if not exist "%PACKAGE_OUTPUT%" mkdir "%PACKAGE_OUTPUT%"
if exist "%ARCHIVE%" del /f /q "%ARCHIVE%"
if exist "%CHECKSUM%" del /f /q "%CHECKSUM%"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "Compress-Archive -LiteralPath '%OUTPUT%' -DestinationPath '%ARCHIVE%' -CompressionLevel Optimal; $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath '%ARCHIVE%').Hash.ToLowerInvariant(); Set-Content -LiteralPath '%CHECKSUM%' -Value ($hash + '  Hermes-release.zip') -Encoding UTF8"

if %errorlevel% neq 0 (
    echo.
    echo Packaging or checksum generation failed!
    pause
    exit /b 1
)


echo.
echo =====================================
echo Build completed successfully!
echo Output:
echo %OUTPUT%
echo Package:
echo %ARCHIVE%
echo Checksum:
echo %CHECKSUM%
echo =====================================

pause
