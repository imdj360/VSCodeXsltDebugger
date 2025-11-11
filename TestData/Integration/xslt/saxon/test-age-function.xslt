<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="3.0"
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:xs="http://www.w3.org/2001/XMLSchema"
  xmlns:ef="http://azure.workflow.datamapper.extensions">

  <xsl:output method="xml" indent="yes"/>

  <!-- Function to calculate age from date of birth -->
  <xsl:function name="ef:age" as="xs:float">
    <xsl:param name="inputDate" as="xs:date" />
    <xsl:value-of select="round(days-from-duration(current-date() - xs:date($inputDate)) div 365.25, 1)" />
  </xsl:function>

  <!-- Main template -->
  <xsl:template match="/">
    <result>
      <age>
        <xsl:value-of select="ef:age(xs:date('2000-05-15'))"/>
      </age>
    </result>
  </xsl:template>

</xsl:stylesheet>
