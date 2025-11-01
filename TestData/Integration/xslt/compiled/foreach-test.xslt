<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0"
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

  <xsl:output method="xml" indent="yes" encoding="UTF-8" />

  <xsl:template match="/">
    <result>
      <!-- Simple for-each without sort -->
      <xsl:for-each select="/root/items/item">
        <item>
          <xsl:value-of select="." />
        </item>
      </xsl:for-each>

      <!-- For-each with sort (to test insertion after sort) -->
      <xsl:for-each select="/root/sorted/item">
        <xsl:sort select="@priority" order="descending" />
        <sorted-item>
          <xsl:value-of select="." />
        </sorted-item>
      </xsl:for-each>
    </result>
  </xsl:template>

</xsl:stylesheet>
