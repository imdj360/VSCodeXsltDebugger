<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet
    version="2.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:xs="http://www.w3.org/2001/XMLSchema"
    xmlns:ns0="http://www.example.com/ShipmentConfv2"
    xmlns:local="urn:local"
    exclude-result-prefixes="xs ns0 local">

  <xsl:output method="xml" indent="yes" encoding="UTF-8"/>

  <!-- Local helpers -->
  <xsl:function name="local:to-utc-datetime" as="xs:dateTime?">
    <xsl:param name="s" as="xs:string?"/>
    <xsl:variable name="trim" select="normalize-space($s)"/>
    <xsl:choose>
      <xsl:when test="$trim = ''">
        <xsl:sequence select="()"/>
      </xsl:when>
      <xsl:otherwise>
        <xsl:variable name="norm" select="replace($trim, '\s+', 'T')"/>
        <xsl:variable name="dt" as="xs:dateTime">
          <xsl:choose>
            <xsl:when test="contains($norm,'T')">
              <xsl:sequence select="xs:dateTime($norm)"/>
            </xsl:when>
            <xsl:otherwise>
              <xsl:sequence select="xs:dateTime(concat($norm,'T00:00:00'))"/>
            </xsl:otherwise>
          </xsl:choose>
        </xsl:variable>
        <xsl:sequence select="adjust-dateTime-to-timezone($dt, xs:dayTimeDuration('PT0H'))"/>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:function>

  <xsl:template match="/">
    <TransportConfirmations>
      <xsl:for-each select="/ns0:ShipmentConfirmation/ns0:Orders/ns0:OrderItems">
        <Confirmation>
          <CompanyName>COMPANY-REDACTED</CompanyName>

          <Reference>
            <xsl:value-of select="../../ns0:Reference"/>
          </Reference>

          <OrderNumber>
            <xsl:value-of select="../ns0:Number"/>
            <xsl:text>/</xsl:text>
            <xsl:value-of select="ns0:Sequence"/>
          </OrderNumber>

          <Quantity>
            <xsl:variable name="net-str" select="normalize-space(../../ns0:Net)"/>
            <xsl:message>hhh:
    <xsl:value-of select="if ($net-str) then $net-str else '(empty)'"/>
</xsl:message>
            <xsl:value-of
              select="if ($net-str castable as xs:decimal)
                      then format-number(xs:decimal($net-str), '0.00')
                      else ''"/>
          </Quantity>

          <Unit>KG</Unit>

          <Date>
            <xsl:variable name="dates-utc" as="xs:dateTime*">
              <xsl:for-each select="ns0:OperationReports/ns0:ReportInfo/ns0:OperationReportDate">
                <xsl:variable name="d" select="local:to-utc-datetime(string(.))"/>
                <xsl:if test="exists($d)">
                  <xsl:sequence select="$d"/>
                </xsl:if>
              </xsl:for-each>
            </xsl:variable>

            <xsl:value-of
              select="if (exists($dates-utc))
                      then format-date(xs:date(min($dates-utc)), '[D01].[M01].[Y0001]')
                      else ''"/>
          </Date>

          <LicensePlate>
            <xsl:value-of select="../../ns0:LicensePlate"/>
          </LicensePlate>
        </Confirmation>
      </xsl:for-each>
    </TransportConfirmations>
  </xsl:template>
</xsl:stylesheet>