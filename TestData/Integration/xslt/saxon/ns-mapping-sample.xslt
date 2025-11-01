<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet
    version="2.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:src="http://www.example.com/ShipmentConfv2"
    xmlns:ship="urn:shipment-summary"
    exclude-result-prefixes="src">

  <xsl:output method="xml" indent="yes" />
  <xsl:strip-space elements="*" />

  <!-- Produce a small summary document that remaps the source namespace to a new output namespace. -->
  <xsl:template match="/">
    <ship:ShipmentSummary>
      <ship:Reference>
        <xsl:value-of select="src:ShipmentConfirmation/src:Reference" />
      </ship:Reference>
      <ship:Totals>
        <ship:OrderCount>
          <xsl:value-of select="count(src:ShipmentConfirmation/src:Orders/src:OrderItems)" />
        </ship:OrderCount>
        <ship:LastReportDate>
          <xsl:value-of select="src:ShipmentConfirmation/src:Orders/src:OrderItems/src:OperationReports/src:ReportInfo[last()]/src:OperationReportDate" />
        </ship:LastReportDate>
      </ship:Totals>
      <ship:Orders>
        <xsl:for-each select="src:ShipmentConfirmation/src:Orders/src:OrderItems">
          <ship:Order>
            <ship:Sequence>
              <xsl:value-of select="src:Sequence" />
            </ship:Sequence>
            <ship:Operation>
              <xsl:value-of select="src:OperationName" />
            </ship:Operation>
            <ship:LatestReport>
              <xsl:value-of select="src:OperationReports/src:ReportInfo[last()]/src:OperationReportDate" />
            </ship:LatestReport>
          </ship:Order>
        </xsl:for-each>
      </ship:Orders>
    </ship:ShipmentSummary>
  </xsl:template>

  <!-- Prevent stray text nodes from leaking into the result. -->
  <xsl:template match="text()" />

</xsl:stylesheet>
