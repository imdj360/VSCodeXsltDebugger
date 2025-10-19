#!/bin/bash
# Windows-only packaging script - includes only win-x64 platform files

set -e  # Exit on error

echo "=== Building Windows-only package ==="
echo ""

# Step 1: Compile TypeScript
echo "1. Compiling TypeScript extension..."
npm run compile

# Step 2: Clean, restore, and rebuild .NET Debug Adapter
echo ""
echo "2. Cleaning previous build..."
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/*

echo ""
echo "3. Restoring .NET packages (all platforms)..."
dotnet restore XsltDebugger.DebugAdapter/XsltDebugger.DebugAdapter.csproj

echo ""
echo "4. Building .NET Debug Adapter..."
dotnet build XsltDebugger.DebugAdapter/XsltDebugger.DebugAdapter.csproj --no-restore

# Step 5: Run tests before packaging
echo ""
echo "5. Running unit tests..."
dotnet test XsltDebugger.Tests/XsltDebugger.Tests.csproj -v minimal

# Step 5: Backup original package.json and update for Windows
echo ""
echo "6. Updating package.json for Windows..."
cp package.json package.json.backup
sed -i '' 's/"name": "xsltdebugger"/"name": "xsltdebugger-windows"/' package.json
sed -i '' 's/"displayName": "XSLT Debugger"/"displayName": "XSLT Debugger (Windows)"/' package.json

# Step 6: Remove all IKVM platforms except win-x64
echo ""
echo "7. Cleaning IKVM platforms (keeping only win-x64)..."
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/android-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/linux-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/osx-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/win-arm64
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/win-x86

# Step 7: Remove all runtimes except win and win-x64
echo ""
echo "7. Cleaning runtimes (keeping only win/win-x64)..."
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/android-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/linux-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/linux
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/osx-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/osx
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/win-arm64
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/win-x86
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/freebsd*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/illumos*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/ios*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/solaris*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/tvos*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/tizen*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/unix

# Step 8: Verify cleanup
echo ""
echo "9. Verifying platform cleanup..."
IKVM_COUNT=$(find XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm -mindepth 1 -maxdepth 1 -type d | wc -l)
echo "   IKVM platforms remaining: $IKVM_COUNT (should be 1)"

# Step 9: Package with --no-dependencies to skip prepublish
echo ""
echo "10. Packaging Windows extension (win32-x64)..."
npx @vscode/vsce package --target win32-x64 --no-dependencies

# Step 10: Restore original package.json
echo ""
echo "11. Restoring original package.json..."
mv package.json.backup package.json

echo ""
echo "✓ Windows package created successfully!"
echo "  Package name: xsltdebugger-windows"
echo "  Target: win32-x64"
echo ""
