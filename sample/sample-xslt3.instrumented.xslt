<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="3.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:dbg="urn:xslt-debugger">
  <xsl:template match="/">
    <xsl:value-of select="dbg:break(3, .)" />
    <xsl:value-of select="dbg:break(4, .)" />
    <html>
      <xsl:value-of select="dbg:break(5, .)" />
      <body>
        <xsl:value-of select="dbg:break(6, .)" />
        <h2>Items (XSLT 3.0)</h2>
        <xsl:value-of select="dbg:break(7, .)" />
        <ul>
          <xsl:value-of select="dbg:break(8, .)" />
          <xsl:for-each select="root/item">
            <xsl:value-of select="dbg:break(9, .)" />
            <li>
              <xsl:value-of select="dbg:break(9, .)" />
              <xsl:value-of select="concat('Item: ', ., ' (', position(), ')')" />
            </li>
          </xsl:for-each>
        </ul>
        <xsl:value-of select="dbg:break(12, .)" />
        <p>Total items: <xsl:value-of select="dbg:break(12, .)" /><xsl:value-of select="count(root/item)" /></p>
        <!-- XSLT 3.0 feature: xsl:iterate -->
        <xsl:value-of select="dbg:break(14, .)" />
        <h3>Using xsl:iterate (XSLT 3.0 feature)</h3>
        <xsl:value-of select="dbg:break(15, .)" />
        <xsl:iterate select="root/item">
          <xsl:param name="counter" select="0" />
          <xsl:value-of select="dbg:break(17, .)" />
          <p>Item <xsl:value-of select="dbg:break(17, .)" /><xsl:value-of select="$counter + 1" />: <xsl:value-of select="dbg:break(17, .)" /><xsl:value-of select="." /></p>
          <xsl:value-of select="dbg:break(18, .)" />
          <xsl:next-iteration>
            <xsl:with-param name="counter" select="$counter + 1" />
          </xsl:next-iteration>
        </xsl:iterate>
      </body>
    </html>
  </xsl:template>
</xsl:stylesheet>