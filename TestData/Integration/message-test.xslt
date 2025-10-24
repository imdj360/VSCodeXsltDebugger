<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="3.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="xml" indent="yes"/>

  <xsl:template match="/">
    <root>
      <xsl:variable name="price" select="123"/>
      <xsl:message select="('[DBG]', 'price', string($price))"/>
      <value><xsl:value-of select="$price"/></value>
    </root>
  </xsl:template>
</xsl:stylesheet>
