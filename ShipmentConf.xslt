<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:template match="/">
    <html>
      <body>
        <h2>Shipment Confirmation</h2>
        <div>
          <h3>Shipment Details</h3>
          <p>Shipment ID: <xsl:value-of select="shipment/@id"/></p>
          <p>Date: <xsl:value-of select="shipment/date"/></p>
          <p>Status: <xsl:value-of select="shipment/status"/></p>
        </div>

        <div>
          <h3>Items</h3>
          <table border="1">
            <tr>
              <th>Item ID</th>
              <th>Description</th>
              <th>Quantity</th>
              <th>Price</th>
            </tr>
            <xsl:for-each select="shipment/items/item">
              <tr>
                <td><xsl:value-of select="@id"/></td>
                <td><xsl:value-of select="description"/></td>
                <td><xsl:value-of select="quantity"/></td>
                <td><xsl:value-of select="price"/></td>
              </tr>
            </xsl:for-each>
          </table>
        </div>
      </body>
    </html>
  </xsl:template>
</xsl:stylesheet>