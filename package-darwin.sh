#!/bin/bash
# macOS-only packaging script - includes only osx-arm64 platform files

echo "Cleaning for macOS-only package..."

# Remove all IKVM platforms except osx-arm64
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/android-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/linux-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/osx-x64
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/win-*

# Remove all runtimes except osx and osx-arm64
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

echo "Packaging macOS-only extension..."
npx @vscode/vsce package --target darwin-arm64

echo ""
echo "✓ macOS-only package created (osx-arm64 only)"
echo "  Check the file size and count above."
