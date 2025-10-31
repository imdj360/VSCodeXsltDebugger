<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="3.0">
  <xsl:output method="xml" indent="yes"/>
  <xsl:template match="/">
    <results>
      <xsl:value-of select="unknown-function(/items)"/>
    </results>
  </xsl:template>
</xsl:stylesheet>
