<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="3.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:template match="/">
    <html>
      <body>
        <h2>Items (XSLT 3.0)</h2>
        <ul>
          <xsl:for-each select="root/item">
            <li><xsl:value-of select="concat('Item: ', ., ' (', position(), ')')"/></li>
          </xsl:for-each>
        </ul>
        <p>Total items: <xsl:value-of select="count(root/item)"/></p>
        <!-- XSLT 3.0 feature: xsl:iterate -->
        <h3>Using xsl:iterate (XSLT 3.0 feature)</h3>
        <xsl:iterate select="root/item">
          <xsl:param name="counter" select="0"/>
          <p>Item <xsl:value-of select="$counter + 1"/>: <xsl:value-of select="."/></p>
          <xsl:next-iteration>
            <xsl:with-param name="counter" select="$counter + 1"/>
          </xsl:next-iteration>
        </xsl:iterate>
      </body>
    </html>
  </xsl:template>
</xsl:stylesheet>
