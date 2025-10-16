#!/bin/bash
# Package both macOS and Windows versions

set -e  # Exit on error

echo "========================================"
echo "=== Building ALL Platform Packages ==="
echo "========================================"
echo ""

# Step 1: Compile TypeScript (once for both)
echo "1. Compiling TypeScript extension..."
npm run compile

# ========================================
# MACOS PACKAGE
# ========================================
echo ""
echo "========================================"
echo "=== Building macOS Package ==="
echo "========================================"
echo ""

# Step 2: Clean, restore, and rebuild .NET Debug Adapter
echo "2. Cleaning previous build..."
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/*

echo ""
echo "3. Restoring .NET packages (all platforms)..."
dotnet restore XsltDebugger.DebugAdapter/XsltDebugger.DebugAdapter.csproj

echo ""
echo "4. Building .NET Debug Adapter..."
dotnet build XsltDebugger.DebugAdapter/XsltDebugger.DebugAdapter.csproj --no-restore

# Step 5: Backup original package.json
echo ""
echo "5. Updating package.json for macOS..."
cp package.json package.json.backup
sed -i '' 's/"name": "xsltdebugger"/"name": "xsltdebugger-darwin"/' package.json
sed -i '' 's/"displayName": "XSLT Debugger"/"displayName": "XSLT Debugger for macOS-arm64"/' package.json

# Step 6: Remove all IKVM platforms except osx-arm64
echo ""
echo "6. Cleaning IKVM platforms (keeping only osx-arm64)..."
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/android-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/linux-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/osx-x64
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/win-*

# Step 7: Remove all runtimes except osx and osx-arm64
echo ""
echo "7. Cleaning runtimes (keeping only osx/osx-arm64)..."
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
echo "8. Verifying platform cleanup..."
IKVM_COUNT=$(find XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm -mindepth 1 -maxdepth 1 -type d | wc -l)
echo "   IKVM platforms remaining: $IKVM_COUNT (should be 1)"

# Step 9: Package with --no-dependencies to skip prepublish
echo ""
echo "9. Packaging macOS extension (darwin-arm64)..."
npx @vscode/vsce package --target darwin-arm64 --no-dependencies

# Step 10: Restore original package.json
echo ""
echo "10. Restoring original package.json..."
mv package.json.backup package.json

echo ""
echo "✓ macOS package created successfully!"
echo "  Package name: xsltdebugger-darwin"
echo "  Target: darwin-arm64"
echo ""

# ========================================
# WINDOWS PACKAGE
# ========================================
echo ""
echo "========================================"
echo "=== Building Windows Package ==="
echo "========================================"
echo ""

# Step 11: Clean and rebuild .NET Debug Adapter for Windows
echo "11. Cleaning previous build..."
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/*

echo ""
echo "12. Restoring .NET packages (all platforms)..."
dotnet restore XsltDebugger.DebugAdapter/XsltDebugger.DebugAdapter.csproj

echo ""
echo "13. Building .NET Debug Adapter..."
dotnet build XsltDebugger.DebugAdapter/XsltDebugger.DebugAdapter.csproj --no-restore

# Step 14: Backup original package.json and update for Windows
echo ""
echo "14. Updating package.json for Windows..."
cp package.json package.json.backup
sed -i '' 's/"name": "xsltdebugger"/"name": "xsltdebugger-windows"/' package.json
sed -i '' 's/"displayName": "XSLT Debugger"/"displayName": "XSLT Debugger (Windows)"/' package.json

# Step 15: Remove all IKVM platforms except win-x64
echo ""
echo "15. Cleaning IKVM platforms (keeping only win-x64)..."
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/android-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/linux-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/osx-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/win-arm64
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/win-x86

# Step 16: Remove all runtimes except win and win-x64
echo ""
echo "16. Cleaning runtimes (keeping only win/win-x64)..."
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

# Step 17: Verify cleanup
echo ""
echo "17. Verifying platform cleanup..."
IKVM_COUNT=$(find XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm -mindepth 1 -maxdepth 1 -type d | wc -l)
echo "   IKVM platforms remaining: $IKVM_COUNT (should be 1)"

# Step 18: Package with --no-dependencies to skip prepublish
echo ""
echo "18. Packaging Windows extension (win32-x64)..."
npx @vscode/vsce package --target win32-x64 --no-dependencies

# Step 19: Restore original package.json
echo ""
echo "19. Restoring original package.json..."
mv package.json.backup package.json

echo ""
echo "✓ Windows package created successfully!"
echo "  Package name: xsltdebugger-windows"
echo "  Target: win32-x64"
echo ""

# ========================================
# SUMMARY
# ========================================
echo "========================================"
echo "=== ALL PACKAGES COMPLETED ==="
echo "========================================"
echo ""
echo "✓ macOS package: xsltdebugger-darwin (darwin-arm64)"
echo "✓ Windows package: xsltdebugger-windows (win32-x64)"
echo ""
echo "Package files:"
ls -lh *.vsix 2>/dev/null || echo "No .vsix files found in current directory"
echo ""
