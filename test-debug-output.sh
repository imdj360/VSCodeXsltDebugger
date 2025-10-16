#!/bin/bash
cd XsltDebugger.DebugAdapter
echo "Starting XSLT Debug Adapter..."
dotnet run -- --test-engine saxon --xslt ../sample/message-test.xslt --xml ../sample/message-test.xml --line 9
