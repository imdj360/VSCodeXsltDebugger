<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

    <xsl:output method="xml" indent="yes"/>

    <!-- Main template that calls other templates -->
    <xsl:template match="/">
        <root>
            <xsl:text>Starting transformation</xsl:text>

            <!-- Call first template -->
            <xsl:call-template name="processOrder"/>

            <xsl:text>Completed transformation</xsl:text>
        </root>
    </xsl:template>

    <!-- Named template 1: processOrder -->
    <xsl:template name="processOrder">
        <order>
            <xsl:text>Processing order...</xsl:text>

            <!-- Call nested template -->
            <xsl:call-template name="calculateTotal"/>

            <xsl:text>Order processed</xsl:text>
        </order>
    </xsl:template>

    <!-- Named template 2: calculateTotal (nested call) -->
    <xsl:template name="calculateTotal">
        <total>
            <xsl:text>Calculating total...</xsl:text>

            <!-- Call another nested template -->
            <xsl:call-template name="formatOutput"/>

            <xsl:text>Total calculated</xsl:text>
        </total>
    </xsl:template>

    <!-- Named template 3: formatOutput (deeply nested) -->
    <xsl:template name="formatOutput">
        <formatted>
            <xsl:text>Output formatted</xsl:text>
        </formatted>
    </xsl:template>

</xsl:stylesheet>
