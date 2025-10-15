#!/bin/bash
# Windows-only packaging script - includes only win-x64 platform files

echo "Cleaning for Windows-only package..."

# Remove all IKVM platforms except win-x64
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/android-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/linux-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/osx-*
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/win-arm64
rm -rf XsltDebugger.DebugAdapter/bin/Debug/net8.0/ikvm/win-x86

# Remove all runtimes except win and win-x64
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

echo "Packaging Windows-only extension..."
npx @vscode/vsce package --target win32-x64

echo ""
echo "✓ Windows-only package created (win-x64 only)"
echo "  Check the file size and count above."
