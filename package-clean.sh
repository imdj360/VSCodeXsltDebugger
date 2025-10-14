#!/bin/bash
# Clean packaging script - removes unwanted platform-specific files before packaging

echo "Cleaning unwanted IKVM platforms..."
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/android-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/linux-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/osx-x64
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/win-arm64
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/win-x86

echo "Cleaning unwanted runtime platforms..."
# Remove non-specific platforms (keep only win-x64 and osx-arm64)
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/android-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/linux-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/linux
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/osx-x64
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/osx
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/win-arm64
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/win-x86
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/win
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/freebsd*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/illumos*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/ios*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/solaris*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/tvos*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/tizen*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/runtimes/unix

echo "Packaging extension..."
npx @vscode/vsce package

echo ""
echo "✓ Package created with only win-x64 and osx-arm64 support (English only)"
echo "  Check the file size and count above."
