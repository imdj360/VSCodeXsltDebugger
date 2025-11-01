<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:msxsl="urn:schemas-microsoft-com:xslt"
                xmlns:user="urn:my-scripts">

  <msxsl:script language="C#" implements-prefix="user">
    <![CDATA[
using System;
using System.Globalization;

public class MathHelper {
    public int Add(int a, int b) {
        return a + b;
    }

    public int Multiply(int a, int b) {
        int result = a * b;
        return result;
    }

    public string FormatNumber(int num) {
        return num.ToString("N0", CultureInfo.InvariantCulture);
    }
}
]]>
  </msxsl:script>

  <xsl:template match="/">
    <html>
      <head>
        <title>Auto-Instrumentation Test</title>
      </head>
      <body>
        <h1>C# Method Auto-Instrumentation Test</h1>
        <p>Add 5 + 3 = <xsl:value-of select="user:Add(5, 3)"/></p>
        <p>Multiply 4 * 7 = <xsl:value-of select="user:Multiply(4, 7)"/></p>
        <p>Formatted: <xsl:value-of select="user:FormatNumber(1000000)"/></p>
      </body>
    </html>
  </xsl:template>

</xsl:stylesheet>
