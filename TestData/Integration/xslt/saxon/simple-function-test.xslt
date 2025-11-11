<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:xs="http://www.w3.org/2001/XMLSchema"
  xmlns:my="urn:my-functions">

  <xsl:output method="xml" indent="yes"/>

  <!-- Simple function that doubles a number -->
  <xsl:function name="my:double" as="xs:integer">
    <xsl:param name="n" as="xs:integer"/>
    <xsl:sequence select="$n * 2"/>
  </xsl:function>

  <!-- Function with multiple parameters -->
  <xsl:function name="my:add" as="xs:integer">
    <xsl:param name="a" as="xs:integer"/>
    <xsl:param name="b" as="xs:integer"/>
    <xsl:sequence select="$a + $b"/>
  </xsl:function>

  <!-- Main template -->
  <xsl:template match="/">
    <result>
      <double>
        <xsl:value-of select="my:double(5)"/>
      </double>
      <add>
        <xsl:value-of select="my:add(10, 20)"/>
      </add>
    </result>
  </xsl:template>

</xsl:stylesheet>
