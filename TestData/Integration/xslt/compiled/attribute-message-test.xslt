<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
    <xsl:output method="xml" indent="yes"/>

    <xsl:template match="/">
        <messages>
            <xsl:apply-templates select="//order"/>
        </messages>
    </xsl:template>

    <xsl:template match="order">
        <message>
            <xsl:attribute name="orderId">
                <xsl:value-of select="@id"/>
            </xsl:attribute>
            <xsl:attribute name="status">
                <xsl:choose>
                    <xsl:when test="@priority = 'high'">urgent</xsl:when>
                    <xsl:otherwise>normal</xsl:otherwise>
                </xsl:choose>
            </xsl:attribute>
            <xsl:attribute name="customer">
                <xsl:value-of select="customer"/>
            </xsl:attribute>
            <content>
                <xsl:text>Order for </xsl:text>
                <xsl:value-of select="item"/>
                <xsl:text> - Quantity: </xsl:text>
                <xsl:value-of select="quantity"/>
            </content>
        </message>
    </xsl:template>
</xsl:stylesheet>
