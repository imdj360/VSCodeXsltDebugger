<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0"
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:msxsl="urn:schemas-microsoft-com:xslt"
  xmlns:user="urn:my-scripts"
  exclude-result-prefixes="msxsl user">

  <!-- Output formatting -->
  <xsl:output method="xml" indent="yes" encoding="UTF-8"/>

  <!-- Inline C# script block -->
  <msxsl:script language="C#" implements-prefix="user">
    <![CDATA[
      public string SayHello()
      {
          return "Hello, World!";
      }
    ]]>
  </msxsl:script>

  <!-- Template that calls the C# function -->
  <xsl:template match="/root">
    <greeting>
      <xsl:value-of select="user:SayHello()"/>
    </greeting>
  </xsl:template>

</xsl:stylesheet>