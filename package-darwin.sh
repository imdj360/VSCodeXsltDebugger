#!/bin/bash
# macOS-only packaging script - includes only osx-arm64 platform files

set -e  # Exit on error

echo "=== Building macOS-only package ==="
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

echo ""
echo "5. Running unit tests..."
dotnet test XsltDebugger.Tests/XsltDebugger.Tests.csproj -v minimal

# Step 5: Backup original package.json
echo ""
echo "6. Updating package.json for macOS..."
cp package.json package.json.backup
sed -i '' 's/"name": "xsltdebugger"/"name": "xsltdebugger-darwin"/' package.json
sed -i '' 's/"displayName": "XSLT Debugger"/"displayName": "XSLT Debugger for macOS-arm64"/' package.json

# Step 6: Remove all IKVM platforms except osx-arm64
echo ""
echo "7. Cleaning IKVM platforms (keeping only osx-arm64)..."
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/android-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/linux-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/osx-x64
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/win-*

# Step 7: Remove all runtimes except osx and osx-arm64
echo ""
echo "8. Cleaning runtimes (keeping only osx/osx-arm64)..."
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/android-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/linux-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/linux
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/osx-x64
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/win-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/win
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
echo "10. Packaging macOS extension (darwin-arm64)..."
npx @vscode/vsce package --target darwin-arm64 --no-dependencies

# Step 10: Restore original package.json
echo ""
echo "11. Restoring original package.json..."
mv package.json.backup package.json

echo ""
echo "✓ macOS package created successfully!"
echo "  Package name: xsltdebugger-darwin"
echo "  Target: darwin-arm64"
echo ""
