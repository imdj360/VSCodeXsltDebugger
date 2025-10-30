<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

    <xsl:output method="xml" indent="yes" />

    <xsl:template match="/">
        <root>
            <xsl:text>Starting transformation</xsl:text>
            <xsl:call-template name="processOrder" />
            <xsl:text>Completed transformation</xsl:text>
        </root>
    </xsl:template>

    <xsl:template name="processOrder">
        <order>
            <xsl:text>Processing order...</xsl:text>
            <xsl:call-template name="calculateTotal">
                <xsl:with-param name="pAmount" select="100" />
            </xsl:call-template>
            <xsl:text>Order processed</xsl:text>
        </order>
    </xsl:template>

    <xsl:template name="calculateTotal">
        <!-- params MUST be first children inside the template -->
        <xsl:param name="pAmount" />
        <!-- keep math in a local variable -->
        <xsl:variable name="vTotal" select="$pAmount * 1.1" />
        <total>
            <xsl:text>Calculating total...</xsl:text>
            <value>
                <xsl:value-of select="$vTotal" />
            </value>
            <xsl:call-template name="formatCurrency">
                <xsl:with-param name="pValue" select="$vTotal" />
            </xsl:call-template>
        </total>
    </xsl:template>

    <xsl:template name="formatCurrency">
        <xsl:param name="pValue" />
        <formatted>
            <xsl:text>$</xsl:text>
            <xsl:value-of select="format-number($pValue, '0.00')" />
        </formatted>
    </xsl:template>

    <xsl:template name="helperFunction">
        <helper>
            <xsl:text>This is a helper that should be skipped with step-over</xsl:text>
        </helper>
    </xsl:template>
</xsl:stylesheet>