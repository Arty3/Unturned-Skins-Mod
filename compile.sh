#!/bin/bash

echo "Compiling Unturned Skins Mod Tool..."

# Check if Rust/Cargo is installed
if ! command -v cargo &> /dev/null
then
	echo "ERROR: Rust/Cargo is not installed or not in your PATH."
	echo "Please install Rust from https://www.rust-lang.org/tools/install"
	read -p "Press any key to continue..."
	exit 1
fi

# Compile the program in release mode
echo "Building release version..."
cargo build --release

if [ $? -ne 0 ]
then
	echo "ERROR: Compilation failed."
	read -p "Press any key to continue..."
	exit $?
fi

# Copy necessary files to the target release directory
echo "Copying required files..."

# Copy bin directory if it exists
if [ -d "bin" ]
then
	if [ ! -d "target/release/bin" ]
	then
		mkdir -p target/release/bin
	fi
	cp -r bin/* target/release/bin/ 2>/dev/null
else
	echo "WARNING: bin directory not found. Make sure to create it with SkinsMod.dll and 0Harmony.dll before running the program."
fi

echo
echo "Build completed successfully!"
echo "The executable and required files are in the \"target/release\" directory."
echo
echo "To run the program:"
echo "1. Navigate to the \"target/release\" directory"
echo "2. Run \"./SkinsMod\" (or \"./SkinsMod.exe\" if using Wine)"
echo

read -p "Press any key to continue..."
exit 0
