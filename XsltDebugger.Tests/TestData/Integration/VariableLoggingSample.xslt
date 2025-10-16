<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="3.0">
  <xsl:output method="xml" indent="yes"/>
  <xsl:template match="/">
    <results>
      <xsl:variable name="itemCount" select="count(/items/item)"/>
      <xsl:variable name="firstName" select="/items/item[1]/name"/>
      <xsl:for-each select="/items/item">
        <xsl:variable name="currentName" select="name"/>
        <item>
          <position><xsl:value-of select="position()"/></position>
          <name><xsl:value-of select="$currentName"/></name>
        </item>
      </xsl:for-each>
      <summary>
        <count><xsl:value-of select="$itemCount"/></count>
        <first><xsl:value-of select="$firstName"/></first>
      </summary>
    </results>
  </xsl:template>
</xsl:stylesheet>
