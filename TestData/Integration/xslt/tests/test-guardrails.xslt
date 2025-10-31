<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="xml" indent="yes"/>

  <xsl:template match="/">
    <root>
      <!-- Safe: normal variable in template -->
      <xsl:variable name="safe1" select="123"/>

      <!-- Unsafe: variable inside xsl:attribute -->
      <elem1>
        <xsl:attribute name="attr1">
          <xsl:variable name="unsafe1" select="'test'"/>
          <xsl:value-of select="$unsafe1"/>
        </xsl:attribute>
      </elem1>

      <!-- Unsafe: variable inside xsl:sequence -->
      <xsl:sequence>
        <xsl:variable name="unsafe2" select="456"/>
        <value><xsl:value-of select="$unsafe2"/></value>
      </xsl:sequence>

      <!-- Safe: variable before xsl:value-of -->
      <elem2>
        <xsl:variable name="safe2" select="789"/>
        <xsl:value-of select="$safe2"/>
      </elem2>
    </root>
  </xsl:template>
</xsl:stylesheet>
