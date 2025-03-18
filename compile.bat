@echo off

echo Compiling Unturned Skins Mod Tool...

:: Check if Rust/Cargo is installed
where cargo >nul 2>nul
if %errorlevel% NEQ 0 (
	echo ERROR: Rust/Cargo is not installed or not in your PATH.
	echo Please install Rust from https://www.rust-lang.org/tools/install
	pause
	exit /b 1
)

:: Compile the program in release mode
echo Building release version...
cargo build --release

if %errorlevel% NEQ 0 (
	echo ERROR: Compilation failed.
	pause
	exit /b %errorlevel%
)

:: Copy necessary files to the target release directory
echo Copying required files...

:: Copy bin directory if it exists
if exist bin (
	if not exist target\release\bin mkdir target\release\bin
	xcopy /E /I /Y bin target\release\bin >nul
) else (
	echo WARNING: bin directory not found. Make sure to create it with SkinsMod.dll and 0Harmony.dll before running the program.
)

echo.
echo Build completed successfully!
echo The executable and required files are in the "target\release" directory.
echo.
echo To run the program: 
echo 1. Navigate to the "target\release" directory
echo 2. Run "SkinsMod.exe" as administrator
echo.

pause
exit /b 0
