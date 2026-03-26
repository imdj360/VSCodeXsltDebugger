<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:msxsl="urn:schemas-microsoft-com:xslt"
    xmlns:cs="urn:cs-ext">

    <msxsl:script language="C#" implements-prefix="cs">
        public string Hello() { return "hello-from-csharp"; }
    </msxsl:script>

    <xsl:output method="xml" indent="yes"/>

    <xsl:template match="/">
        <root>
            <xsl:value-of select="cs:Hello()"/>
        </root>
    </xsl:template>

</xsl:stylesheet>
