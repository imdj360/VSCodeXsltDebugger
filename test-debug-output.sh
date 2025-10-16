#!/bin/bash
cd XsltDebugger.DebugAdapter
echo "Starting XSLT Debug Adapter..."
dotnet run -- --test-engine saxon --xslt ../XsltDebugger.ConsoleTest/sample/message-test.xslt --xml ../XsltDebugger.ConsoleTest/sample/message-test.xml --line 9
