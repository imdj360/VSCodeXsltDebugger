<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet
  version="1.0"
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:msxsl="urn:schemas-microsoft-com:xslt"
  xmlns:userCSharp="urn:userCSharp"
  exclude-result-prefixes="msxsl userCSharp">

  <xsl:output method="xml" indent="yes" encoding="UTF-8" />

  <msxsl:script language="C#" implements-prefix="userCSharp">
    <![CDATA[

   
      public string Sum(string values)
{
    double total = 0;
    foreach (string s in values.Split(','))
    {
        double d;
        if (double.TryParse(s, out d))
            total += d;
    }
    return total.ToString("0.##");
}


public string RoundToTwoDecimals(string input)
  {
      try
      {
          double d = Convert.ToDouble(input);
          return d.ToString("0.00");
      }
      catch
      {
          return "0.00";
      }
  }

public string MinDate(string values)
{
    DateTime min = DateTime.MaxValue;
    foreach (string s in values.Split(','))
    {
        DateTime dt;
        if (DateTime.TryParse(s, out dt) && dt < min)
            min = dt;
    }
    return min == DateTime.MaxValue ? "" : min.ToString("dd.MM.yyyy");
}
    ]]>
  </msxsl:script>

  <xsl:template match="/">
    <TransportConfirmations>
      <!-- Optional header/placeholder to mirror original shape -->
      <Confirmation />

      <xsl:for-each select="/ShipmentConfirmation/Orders/OrderItems">
        <Confirmation>
          <CompanyName>Logic App Tool Shipper</CompanyName>

          <Reference>
            <xsl:value-of select="/ShipmentConfirmation/Reference" />
          </Reference>
          <xsl:message>Hello</xsl:message>
          <OrderNumber>
            <xsl:value-of select="/ShipmentConfirmation/Orders/Number" />
            <xsl:text>/</xsl:text>
            <xsl:value-of select="Sequence" />
          </OrderNumber>

          <Quantity>
            <xsl:value-of select="userCSharp:RoundToTwoDecimals(/ShipmentConfirmation/Net)" />
          </Quantity>

          <Unit>KG</Unit>

          <!-- Earliest operation date across ReportInfo nodes -->
          <Date>
            <xsl:variable name="dateList">
              <xsl:for-each select="OperationReports/ReportInfo/OperationReportDate">
                <xsl:value-of select="normalize-space(.)" />
                <xsl:if test="position() != last()">,</xsl:if>
              </xsl:for-each>
            </xsl:variable>
            <xsl:value-of select="userCSharp:MinDate($dateList)" />
          </Date>

          <LicensePlate>
            <xsl:value-of select="/ShipmentConfirmation/LicensePlate" />
          </LicensePlate>
        </Confirmation>
      </xsl:for-each>
    </TransportConfirmations>
  </xsl:template>
</xsl:stylesheet>