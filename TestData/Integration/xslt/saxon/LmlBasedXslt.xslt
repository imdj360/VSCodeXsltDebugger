<xsl:stylesheet xmlns:xs="http://www.w3.org/2001/XMLSchema"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:math="http://www.w3.org/2005/xpath-functions/math"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xmlns:dm="http://azure.workflow.datamapper"
    xmlns:ef="http://azure.workflow.datamapper.extensions"
    exclude-result-prefixes="xsl xs math dm ef"
    version="3.0" expand-text="yes">
    <xsl:output indent="yes" media-type="text/xml" method="xml" />
    <xsl:template match="/">
        <xsl:apply-templates select="." mode="azure.workflow.datamapper" />
    </xsl:template>
    <xsl:template match="/" mode="azure.workflow.datamapper">
        <TransportConfirmations>
            <xsl:for-each select="/ShipmentConfirmation/Orders/OrderItems">
                <Confirmation>
                    <CompanyName>{../CustomerName}</CompanyName>
                    <Reference>{../../Reference}</Reference>
                    <OrderNumber>{concat(../Number, '/', Sequence)}</OrderNumber>
                    <Quantity>{if (../../Net castable as xs:decimal) then
                        format-number(xs:decimal(../../Net),
                        '0.00') else '0.00'}</Quantity>
                    <Date>{ef:toUtcDateTime(/ShipmentConfirmation/Orders/Date)}</Date>
                    <LicensePlate>{../../LicensePlate}</LicensePlate>
                    <OperationCode>{OperationCode}</OperationCode>
                </Confirmation>
            </xsl:for-each>
        </TransportConfirmations>
    </xsl:template>
    <xsl:function name="ef:toUtcDateTime" as="xs:dateTime?">
        <xsl:param name="inputDate" as="xs:string" />
        <xsl:variable name="trim" select="normalize-space($inputDate)" />
        <xsl:choose>
            <xsl:when test="$trim = ''">
                <xsl:sequence select="()" />
            </xsl:when>
            <xsl:otherwise>
                <xsl:variable name="norm" select="replace($trim, '\s+', 'T')" />
                <xsl:variable name="dt">
                    <xsl:choose>
                        <xsl:when test="contains($norm,'T')">
                            <xsl:sequence select="xs:dateTime($norm)" />
                        </xsl:when>
                        <xsl:otherwise>
                            <xsl:sequence select="xs:dateTime(concat($norm, 'T00:00:00'))" />
                        </xsl:otherwise>
                    </xsl:choose>
                </xsl:variable>
                <xsl:sequence select="adjust-dateTime-to-timezone($dt, xs:dayTimeDuration('PT0H'))" />
            </xsl:otherwise>
        </xsl:choose>
    </xsl:function>
</xsl:stylesheet>