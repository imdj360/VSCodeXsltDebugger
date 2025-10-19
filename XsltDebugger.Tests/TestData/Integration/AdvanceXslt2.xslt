<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet
  version="2.0"
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:xs="http://www.w3.org/2001/XMLSchema"
  xmlns:f="urn:f"
  exclude-result-prefixes="xs f">

  <xsl:output method="xml" indent="yes" />
  <xsl:strip-space elements="*" />
  <xsl:decimal-format name="dot" decimal-separator="." grouping-separator="," />

  <xsl:param name="report-cutoff" as="xs:dateTime?" select="()" />

  <xsl:function name="f:to-datetime" as="xs:dateTime?">
    <xsl:param name="s" as="xs:string?" />
    <xsl:sequence
      select="
      if (empty($s)) then ()
      else if ($s castable as xs:dateTime) then xs:dateTime($s)
      else if ($s castable as xs:date) then xs:dateTime(concat(string(xs:date($s)),'T00:00:00'))
      else ()
    " />
  </xsl:function>

  <xsl:function name="f:hours-between" as="xs:decimal?">
    <xsl:param name="a" as="xs:dateTime?" />
    <xsl:param name="b" as="xs:dateTime?" />
    <xsl:sequence
      select="
      if (exists($a) and exists($b))
      then round-half-to-even( ( ($b - $a) div xs:dayTimeDuration('PT1H') ), 3 )
      else ()
    " />
  </xsl:function>

  <xsl:function name="f:max-datetime" as="xs:dateTime?">
    <xsl:param name="nodes" as="node()*" />
    <xsl:variable name="tmp">
      <dts>
        <xsl:for-each select="$nodes">
          <xsl:variable name="dt" select="f:to-datetime(string(.))" as="xs:dateTime?" />
          <xsl:if test="exists($dt) and (empty($report-cutoff) or $dt ge $report-cutoff)">
            <dt>
              <xsl:value-of select="$dt" />
            </dt>
          </xsl:if>
        </xsl:for-each>
      </dts>
    </xsl:variable>
    <xsl:variable name="seq" as="xs:dateTime*"
      select="for $n in $tmp/dts/dt return xs:dateTime(string($n))" />
    <xsl:sequence select="if (empty($seq)) then () else max($seq)" />
  </xsl:function>

  <xsl:template match="/ShipmentConfirmation">
    <xsl:variable name="arrival" as="xs:dateTime?" select="f:to-datetime(string(Arrival))" />
    <xsl:variable name="departure" as="xs:dateTime?" select="f:to-datetime(string(Departure))" />
    <xsl:variable name="hoursOnSite" as="xs:decimal?" select="f:hours-between($arrival, $departure)" />

    <xsl:variable name="all-report-dts-tree">
      <dts>
        <xsl:for-each select="Orders/OrderItems/OperationReports/ReportInfo/OperationReportDate">
          <xsl:variable name="dt" select="f:to-datetime(string(.))" as="xs:dateTime?" />
          <xsl:if test="exists($dt) and (empty($report-cutoff) or $dt ge $report-cutoff)">
            <dt>
              <xsl:value-of select="$dt" />
            </dt>
          </xsl:if>
        </xsl:for-each>
      </dts>
    </xsl:variable>

    <xsl:variable name="all-report-dts" as="node()*" select="$all-report-dts-tree/dts/dt" />

    <ShipmentSummary>
      <Transport>
        <TypeCode>
          <xsl:value-of select="TransportTypeCode" />
        </TypeCode>
        <TypeDescription>
          <xsl:value-of select="TransportTypeDescription" />
        </TypeDescription>
        <Mode>
          <xsl:value-of select="TransportMode" />
        </Mode>
        <Direction>
          <xsl:value-of select="Direction" />
        </Direction>
        <LicensePlate>
          <xsl:value-of select="LicensePlate" />
        </LicensePlate>
        <Status>
          <xsl:value-of select="Status" />
        </Status>
        <Reference>
          <xsl:value-of select="Reference" />
        </Reference>
      </Transport>

      <Timing>
        <Arrival>
          <xsl:value-of select="Arrival" />
        </Arrival>
        <Departure>
          <xsl:value-of select="Departure" />
        </Departure>
        <HoursOnSite>
          <xsl:value-of
            select="if (exists($hoursOnSite)) then format-number($hoursOnSite,'0.###','dot') else ''" />
        </HoursOnSite>
      </Timing>

      <Net>
        <xsl:value-of select="format-number(xs:decimal(Net),'0.###','dot')" />
      </Net>

      <Orders>
        <Header>
          <Number>
            <xsl:value-of select="Orders/Number" />
          </Number>
          <Date>
            <xsl:value-of select="Orders/Date" />
          </Date>
          <CustomerCode>
            <xsl:value-of select="Orders/CustomerCode" />
          </CustomerCode>
          <CustomerName>
            <xsl:value-of select="Orders/CustomerName" />
          </CustomerName>
        </Header>

        <OrderItems>
          <xsl:for-each select="Orders/OrderItems">
            <OrderItem>
              <Sequence>
                <xsl:value-of select="Sequence" />
              </Sequence>
              <OperationName>
                <xsl:value-of select="OperationName" />
              </OperationName>
              <OperationCode>
                <xsl:value-of select="OperationCode" />
              </OperationCode>

              <LatestReport>
                <xsl:variable name="latest" as="xs:dateTime?"
                  select="f:max-datetime(OperationReports/ReportInfo/OperationReportDate)" />
                <xsl:value-of select="if (exists($latest)) then string($latest) else ''" />
              </LatestReport>

              <Reports>
                <xsl:for-each select="OperationReports/ReportInfo">
                  <xsl:variable name="dt" as="xs:dateTime?"
                    select="f:to-datetime(string(OperationReportDate))" />
                  <xsl:if test="exists($dt) and (empty($report-cutoff) or $dt ge $report-cutoff)">
                    <Report>
                      <OperationReportDate>
                        <xsl:value-of select="string($dt)" />
                      </OperationReportDate>
                    </Report>
                  </xsl:if>
                </xsl:for-each>
              </Reports>
            </OrderItem>
          </xsl:for-each>
        </OrderItems>
      </Orders>

      <MergedReportTimeline>
        <xsl:for-each-group select="$all-report-dts" group-by=".">
          <xsl:sort select="current-grouping-key()" />
          <Point>
            <DateTime>
              <xsl:value-of select="current-grouping-key()" />
            </DateTime>
            <Count>
              <xsl:value-of select="count(current-group())" />
            </Count>
          </Point>
        </xsl:for-each-group>
      </MergedReportTimeline>

      <Meta>
        <ReportCount>
          <xsl:value-of select="count($all-report-dts)" />
        </ReportCount>
        <CutoffApplied>
          <xsl:value-of select="exists($report-cutoff)" />
        </CutoffApplied>
      </Meta>
    </ShipmentSummary>
  </xsl:template>

</xsl:stylesheet>
