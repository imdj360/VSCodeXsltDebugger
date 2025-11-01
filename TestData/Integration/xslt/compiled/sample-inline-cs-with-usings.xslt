<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:msxsl="urn:schemas-microsoft-com:xslt"
                xmlns:user="urn:my-scripts">

  <msxsl:script language="C#" implements-prefix="user">
    <![CDATA[
using System;
using System.Globalization;

public class DateFormatter {
    public string FormatCurrentDate() {
        LogEntry();
        return LogReturn(DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    public string AddDays(string dateStr, int days) {
        LogEntry(new { dateStr, days });
        DateTime date = DateTime.Parse(dateStr);
        return LogReturn(date.AddDays(days).ToString("yyyy-MM-dd"));
    }
}
]]>
  </msxsl:script>

  <xsl:template match="/">
    <html>
      <head>
        <title>XSLT Debug Test - Inline C# with Using Statements</title>
      </head>
      <body>
        <h1>XSLT Debugging Test</h1>
        <p>Current date: <xsl:value-of select="user:FormatCurrentDate()"/></p>
        <p>Date plus 7 days: <xsl:value-of select="user:AddDays('2024-01-01', 7)"/></p>
        <h2>Data from XML:</h2>
        <xsl:apply-templates select="data/item"/>
      </body>
    </html>
  </xsl:template>

  <xsl:template match="item">
    <div>
      <strong><xsl:value-of select="name"/></strong>: <xsl:value-of select="value"/>
    </div>
  </xsl:template>

</xsl:stylesheet>
