<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
    <xsl:output method="xml" indent="yes"/>

    <xsl:template match="/">
        <root>
            <xsl:apply-templates select="//item"/>
        </root>
    </xsl:template>

    <xsl:template match="item">
        <element>
            <xsl:attribute name="id">
                <xsl:value-of select="@id"/>
            </xsl:attribute>
            <xsl:attribute name="type">
                <xsl:text>test-type</xsl:text>
            </xsl:attribute>
            <xsl:value-of select="text()"/>
        </element>
    </xsl:template>
</xsl:stylesheet>
