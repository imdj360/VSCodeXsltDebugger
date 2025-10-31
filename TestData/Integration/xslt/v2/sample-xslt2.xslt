<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:template match="/">
    <html>
      <body>
        <h2>Items (XSLT 2.0)</h2>
        <ul>
          <xsl:for-each select="root/item">
            <li><xsl:value-of select="concat('Item: ', ., ' (', position(), ')')"/></li>
          </xsl:for-each>
        </ul>
        <p>Total items: <xsl:value-of select="count(root/item)"/></p>
      </body>
    </html>
  </xsl:template>
</xsl:stylesheet>