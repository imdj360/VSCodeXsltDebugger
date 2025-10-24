<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="xml" indent="yes" />
  <xsl:template match="/">
    <results>
      <xsl:variable name="count" select="count(/items/item)" />
      <xsl:variable name="firstName" select="/items/item[1]/name" />
      <summary>
        <count>
          <xsl:value-of select="$count" />
        </count>
        <first>
          <xsl:value-of select="$firstName" />
        </first>
      </summary>
    </results>
  </xsl:template>
</xsl:stylesheet>