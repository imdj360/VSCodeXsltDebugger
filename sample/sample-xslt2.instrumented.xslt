<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:dbg="urn:xslt-debugger">
  <xsl:template match="/">
    <xsl:value-of select="dbg:break(3, .)" />
    <xsl:value-of select="dbg:break(4, .)" />
    <html>
      <xsl:value-of select="dbg:break(5, .)" />
      <body>
        <xsl:value-of select="dbg:break(6, .)" />
        <h2>Items (XSLT 2.0)</h2>
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
      </body>
    </html>
  </xsl:template>
</xsl:stylesheet>