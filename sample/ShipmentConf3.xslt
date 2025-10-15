<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet
    version="3.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:xs="http://www.w3.org/2001/XMLSchema"
    xmlns:local="urn:local">  <!-- declare the prefix used below -->

    <xsl:output method="xml" indent="yes" encoding="UTF-8" />

    <!-- Parse mixed date/time strings and normalize to UTC -->
    <xsl:function name="local:to-utc-datetime" as="xs:dateTime?">
        <xsl:param name="s" as="xs:string?" />
        <xsl:variable name="trim" select="normalize-space($s)" />
        <xsl:sequence
            select="
      if ($trim = '') then ()
      else
        let $dt :=
          if (contains($trim, 'T'))
             then xs:dateTime($trim)
             else xs:dateTime(concat($trim, 'T00:00:00'))
        return adjust-dateTime-to-timezone($dt, xs:dayTimeDuration('PT0H'))
    " />
    </xsl:function>

    <xsl:template match="/">
        <TransportConfirmations>
            <Confirmation />

            <xsl:for-each select="/ShipmentConfirmation/Orders/OrderItems">
                <Confirmation>
                    <CompanyName>ACME AGRI BV (NL)</CompanyName>

                    <Reference>
                        <xsl:value-of select="/ShipmentConfirmation/Reference" />
                    </Reference>

                    <OrderNumber>
                        <xsl:value-of select="/ShipmentConfirmation/Orders/Number" />
                        <xsl:text>/</xsl:text>
                        <xsl:value-of select="Sequence" />
                    </OrderNumber>

                    <Quantity>
                        <xsl:value-of
                            select="format-number(number(/ShipmentConfirmation/Net), '0.00')" />
                    </Quantity>

                    <Unit>KG</Unit>

                    <Date>
                        <xsl:variable name="dates-utc" as="xs:dateTime*"
                            select="OperationReports/ReportInfo/OperationReportDate
                        ! local:to-utc-datetime(string(.))" />
                        <xsl:value-of
                            select="if (exists($dates-utc))
                      then format-date(xs:date(min($dates-utc)), '[D01].[M01].[Y0001]')
                      else ''" />
                    </Date>

                    <LicensePlate>
                        <xsl:value-of select="/ShipmentConfirmation/LicensePlate" />
                    </LicensePlate>
                </Confirmation>
            </xsl:for-each>
        </TransportConfirmations>
    </xsl:template>
</xsl:stylesheet>