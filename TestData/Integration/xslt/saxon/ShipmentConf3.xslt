<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet
    version="2.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:xs="http://www.w3.org/2001/XMLSchema"
    xmlns:local="urn:local">

    <xsl:output method="xml" indent="yes" encoding="UTF-8" />

    <!-- Helper: convert mixed date/time string to UTC xs:dateTime.
       Notes:
       - If input has no timezone, PT0H simply tags it as Z (no shift).
       - Handles 'YYYY-MM-DD', 'YYYY-MM-DDTHH:MM(:SS)?', and 'YYYY-MM-DD HH:MM(:SS)?'. -->
    <xsl:function name="local:to-utc-datetime" as="xs:dateTime?">
        <xsl:param name="s" as="xs:string?" />
        <xsl:variable name="trim" select="normalize-space($s)" />
        <xsl:choose>
            <xsl:when test="$trim = ''">
                <xsl:sequence select="()" />
            </xsl:when>
            <xsl:otherwise>
                <!-- normalize any single/multiple spaces to 'T' for date+time -->
                <xsl:variable name="norm" select="replace($trim, '\s+', 'T')" />
                <xsl:variable name="dt" as="xs:dateTime">
                    <xsl:choose>
                        <xsl:when test="contains($norm,'T')">
                            <xsl:sequence select="xs:dateTime($norm)" />
                        </xsl:when>
                        <xsl:otherwise>
                            <xsl:sequence select="xs:dateTime(concat($norm,'T00:00:00'))" />
                        </xsl:otherwise>
                    </xsl:choose>
                </xsl:variable>
                <xsl:sequence select="adjust-dateTime-to-timezone($dt, xs:dayTimeDuration('PT0H'))" />
            </xsl:otherwise>
        </xsl:choose>
    </xsl:function>

    <xsl:template match="/">
        <TransportConfirmations>
            <xsl:for-each select="/ShipmentConfirmation/Orders/OrderItems">
                <Confirmation>
                    <CompanyName>COMPANY-REDACTED</CompanyName>

                    <Reference>
                        <xsl:value-of select="../../Reference" />
                    </Reference>

                    <OrderNumber>
                        <xsl:value-of select="../Number" />
                        <xsl:text>/</xsl:text>
                        <xsl:value-of select="Sequence" />
                    </OrderNumber>

                    <Quantity>
                        <xsl:variable name="net-str" select="normalize-space(../../Net)" />

                        <xsl:message>hhh: <xsl:value-of
                                select="if ($net-str) then $net-str else '(empty)'" />
                        </xsl:message>

                        <xsl:value-of
                            select="if ($net-str castable as xs:decimal)
                      then format-number(xs:decimal($net-str), '0.00')
                      else ''" />
                    </Quantity>

                    <Unit>KG</Unit>

                    <xsl:variable name="dates-utc3" as="xs:dateTime*"
                        select="OperationReports/ReportInfo/OperationReportDate
                      ! local:to-utc-datetime(string(.))[exists(.)]" />

                    <Date>
                        <!-- build sequence of UTC datetimes -->
                        <xsl:variable name="dates-utc" as="xs:dateTime*">
                            <xsl:for-each select="OperationReports/ReportInfo/OperationReportDate">
                                <xsl:variable name="d" select="local:to-utc-datetime(string(.))" />
                                <xsl:if test="exists($d)">
                                    <xsl:sequence select="$d" />
                                </xsl:if>
                            </xsl:for-each>
                        </xsl:variable>


                        <xsl:value-of
                            select="if (exists($dates-utc))
                      then format-date(xs:date(min($dates-utc)), '[D01].[M01].[Y0001]')
                      else ''" />


                    </Date>

                    <LicensePlate>
                        <xsl:value-of select="../../LicensePlate" />
                    </LicensePlate>
                </Confirmation>
            </xsl:for-each>
        </TransportConfirmations>
    </xsl:template>
</xsl:stylesheet>
