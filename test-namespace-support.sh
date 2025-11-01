#!/bin/bash

echo "=================================="
echo "XSLT Namespace Support Tests"
echo "=================================="
echo ""

# Test 1: LmlBasedXslt (Logic Apps generated)
echo "Test 1: LmlBasedXslt.xslt (Logic Apps, XSLT 3.0 with namespaces)"
echo "----------------------------------------"
dotnet run --project XsltDebugger.ConsoleTest/XsltDebugger.ConsoleTest.csproj -- \
    LmlBasedXslt.xslt ShipmentConf-lml.xml --engine saxon 2>&1 | \
    grep -A 20 "REGISTERED NAMESPACES" | head -25
echo ""

# Test 2: AdvanceXslt2 (no namespaces in XML)
echo "Test 2: AdvanceXslt2.xslt (XSLT 2.0, XML without namespaces)"
echo "----------------------------------------"
dotnet run --project XsltDebugger.ConsoleTest/XsltDebugger.ConsoleTest.csproj -- \
    AdvanceXslt2.xslt AdvanceFile.xml --engine saxon 2>&1 | \
    grep -A 20 "REGISTERED NAMESPACES" | head -25
echo ""

# Test 3: ShipmentConf3 (XSLT 3.0)
echo "Test 3: ShipmentConf3.xslt (XSLT 3.0 with custom functions)"
echo "----------------------------------------"
dotnet run --project XsltDebugger.ConsoleTest/XsltDebugger.ConsoleTest.csproj -- \
    ShipmentConf3.xslt ShipmentConf-proper.xml --engine saxon 2>&1 | \
    grep -A 20 "REGISTERED NAMESPACES" | head -25
echo ""

# Test 4: VariableLoggingSampleV1 (XSLT 1.0)
echo "Test 4: VariableLoggingSampleV1.xslt (XSLT 1.0)"
echo "----------------------------------------"
dotnet run --project XsltDebugger.ConsoleTest/XsltDebugger.ConsoleTest.csproj -- \
    VariableLoggingSampleV1.xslt VariableLoggingSampleV1Input.xml --engine compiled 2>&1 | \
    grep -A 20 "REGISTERED NAMESPACES" | head -25
echo ""

echo "=================================="
echo "All tests complete!"
echo "=================================="
