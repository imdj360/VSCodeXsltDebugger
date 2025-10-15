<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="3.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:local="urn:local" xmlns:dbg="urn:xslt-debugger">
  <!-- declare the prefix used below -->
  <xsl:output method="xml" indent="yes" encoding="UTF-8" />
  <!-- Parse mixed date/time strings and normalize to UTC -->
  <xsl:function name="local:to-utc-datetime" as="xs:dateTime?">
    <xsl:param name="s" as="xs:string?" />
    <xsl:variable name="trim" select="normalize-space($s)" />
    <xsl:value-of select="dbg:break(14, .)" />
    <xsl:sequence select="       if ($trim = '') then ()       else         let $dt :=           if (contains($trim, 'T'))              then xs:dateTime($trim)              else xs:dateTime(concat($trim, 'T00:00:00'))         return adjust-dateTime-to-timezone($dt, xs:dayTimeDuration('PT0H'))     " />
  </xsl:function>
  <xsl:template match="/">
    <xsl:value-of select="dbg:break(26, .)" />
    <xsl:value-of select="dbg:break(27, .)" />
    <TransportConfirmations>
      <xsl:value-of select="dbg:break(28, .)" />
      <Confirmation />
      <xsl:value-of select="dbg:break(30, .)" />
      <xsl:for-each select="/ShipmentConfirmation/Orders/OrderItems">
        <xsl:value-of select="dbg:break(31, .)" />
        <Confirmation>
          <xsl:value-of select="dbg:break(32, .)" />
          <CompanyName>ACME AGRI BV (NL)</CompanyName>
          <xsl:value-of select="dbg:break(34, .)" />
          <Reference>
            <xsl:value-of select="dbg:break(35, .)" />
            <xsl:value-of select="/ShipmentConfirmation/Reference" />
          </Reference>
          <xsl:value-of select="dbg:break(38, .)" />
          <OrderNumber>
            <xsl:value-of select="dbg:break(39, .)" />
            <xsl:value-of select="/ShipmentConfirmation/Orders/Number" />
            <xsl:value-of select="dbg:break(40, .)" />
            <xsl:text>/</xsl:text>
            <xsl:value-of select="dbg:break(41, .)" />
            <xsl:value-of select="Sequence" />
          </OrderNumber>
          <xsl:value-of select="dbg:break(44, .)" />
          <Quantity>
            <xsl:value-of select="dbg:break(45, .)" />
            <xsl:value-of select="format-number(number(/ShipmentConfirmation/Net), '0.00')" />
          </Quantity>
          <xsl:value-of select="dbg:break(49, .)" />
          <Unit>KG</Unit>
          <xsl:value-of select="dbg:break(51, .)" />
          <Date>
            <xsl:variable name="dates-utc" as="xs:dateTime*" select="OperationReports/ReportInfo/OperationReportDate                         ! local:to-utc-datetime(string(.))" />
            <xsl:value-of select="dbg:break(55, .)" />
            <xsl:value-of select="if (exists($dates-utc))                       then format-date(xs:date(min($dates-utc)), '[D01].[M01].[Y0001]')                       else ''" />
          </Date>
          <xsl:value-of select="dbg:break(61, .)" />
          <LicensePlate>
            <xsl:value-of select="dbg:break(62, .)" />
            <xsl:value-of select="/ShipmentConfirmation/LicensePlate" />
          </LicensePlate>
        </Confirmation>
      </xsl:for-each>
    </TransportConfirmations>
  </xsl:template>
</xsl:stylesheet>