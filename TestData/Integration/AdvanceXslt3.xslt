<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet
  version="3.0"
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:xs="http://www.w3.org/2001/XMLSchema"
  xmlns:f="urn:f"
  exclude-result-prefixes="xs f">

  <!-- Output & whitespace -->
  <xsl:output method="xml" indent="yes" />
  <xsl:strip-space elements="*" />
  <xsl:decimal-format name="dot" decimal-separator="." grouping-separator="," />

  <!-- Params -->
  <xsl:param name="report-cutoff" as="xs:dateTime?" select="()" />
  <xsl:param name="tz-offset" as="xs:dayTimeDuration?" select="()" />

  <!-- Accumulator: count OperationReportDate elements -->
  <xsl:accumulator name="report-count" as="xs:integer" initial-value="0">
    <xsl:accumulator-rule match="OperationReportDate" select="$value + 1" />
  </xsl:accumulator>

  <!-- ===== Utility functions ===== -->

  <!-- Lenient parse: try xs:dateTime($s); if that fails, try xs:date($s)||'T00:00:00' -->
  <xsl:function name="f:to-datetime" as="xs:dateTime?">
    <xsl:param name="s" as="xs:string?" />
    <xsl:variable name="res">
      <xsl:choose>
        <xsl:when test="empty($s)" />
        <xsl:otherwise>
          <xsl:try>
            <xsl:sequence select="xs:dateTime($s)" />
            <xsl:catch>
              <xsl:sequence
                select="
                if ($s castable as xs:date)
                then xs:dateTime(concat(string(xs:date($s)),'T00:00:00'))
                else ()
              " />
            </xsl:catch>
          </xsl:try>
        </xsl:otherwise>
      </xsl:choose>
    </xsl:variable>
    <xsl:sequence select="$res" />
  </xsl:function>

  <!-- Normalize a datetime to a specific timezone offset -->
  <xsl:function name="f:normalize-datetime-to-timezone" as="xs:dateTime?">
    <xsl:param name="dt" as="xs:dateTime?" />
    <xsl:param name="tz" as="xs:dayTimeDuration?" />
    <xsl:sequence
      select="if (exists($dt) and exists($tz)) then adjust-dateTime-to-timezone($dt, $tz) else $dt" />
  </xsl:function>

  <!-- Hours between two datetimes, rounded to 3 decimals -->
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

  <!-- Human-friendly Hh MMm formatting for decimal hours -->
  <xsl:function name="f:format-duration" as="xs:string">
    <xsl:param name="hours" as="xs:decimal?" />
    <xsl:variable name="h" select="if (exists($hours)) then floor($hours) else 0" />
    <xsl:variable name="m" select="if (exists($hours)) then round(60 * ($hours - $h)) else 0" />
    <xsl:sequence
      select="if (empty($hours)) then '' else concat($h, 'h ', format-number($m, '00'), 'm')" />
  </xsl:function>

  <!-- Max datetime from node()* of date/time strings; respects $report-cutoff -->
  <xsl:function name="f:max-datetime" as="xs:dateTime?">
    <xsl:param name="nodes" as="node()*" />
    <xsl:variable name="filtered" as="xs:dateTime*">
      <xsl:for-each select="$nodes">
        <xsl:variable name="dt" select="f:to-datetime(string(.))" as="xs:dateTime?" />
        <xsl:if test="exists($dt) and (empty($report-cutoff) or $dt ge $report-cutoff)">
          <xsl:sequence select="$dt" />
        </xsl:if>
      </xsl:for-each>
    </xsl:variable>
    <xsl:sequence select="if (empty($filtered)) then () else max($filtered)" />
  </xsl:function>

  <!-- Latest timestamp strictly BEFORE a cutoff -->
  <xsl:function name="f:latest-before" as="xs:dateTime?">
    <xsl:param name="nodes" as="node()*" />
    <xsl:param name="cutoff" as="xs:dateTime?" />
    <xsl:variable name="filtered" as="xs:dateTime*">
      <xsl:for-each select="$nodes">
        <xsl:variable name="dt" select="f:to-datetime(string(.))" as="xs:dateTime?" />
        <xsl:if test="exists($dt) and exists($cutoff) and $dt lt $cutoff">
          <xsl:sequence select="$dt" />
        </xsl:if>
      </xsl:for-each>
    </xsl:variable>
    <xsl:sequence select="if (empty($filtered)) then () else max($filtered)" />
  </xsl:function>

  <!-- ===== Main transform ===== -->
  <xsl:mode use-accumulators="report-count" />

  <xsl:template match="/ShipmentConfirmation">
    <xsl:variable name="arrival-raw" as="xs:dateTime?" select="f:to-datetime(string(Arrival))" />
    <xsl:variable name="departure-raw" as="xs:dateTime?" select="f:to-datetime(string(Departure))" />

    <!-- Optionally normalized for display -->
    <xsl:variable name="arrival" as="xs:dateTime?"
      select="f:normalize-datetime-to-timezone($arrival-raw,   $tz-offset)" />
    <xsl:variable name="departure" as="xs:dateTime?"
      select="f:normalize-datetime-to-timezone($departure-raw, $tz-offset)" />

    <xsl:variable name="hoursOnSite" as="xs:decimal?"
      select="f:hours-between($arrival-raw, $departure-raw)" />

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

        <xsl:if test="exists($tz-offset)">
          <Normalized timezone="{string($tz-offset)}">
            <Arrival>
              <xsl:value-of select="string($arrival)" />
            </Arrival>
            <Departure>
              <xsl:value-of select="string($departure)" />
            </Departure>
          </Normalized>
        </xsl:if>

        <HoursOnSite>
          <Decimal>
            <xsl:value-of
              select="if (exists($hoursOnSite)) then format-number($hoursOnSite,'0.###','dot') else ''" />
          </Decimal>
          <Human>
            <xsl:value-of select="f:format-duration($hoursOnSite)" />
          </Human>
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

              <LatestReportBeforeDeparture>
                <xsl:variable name="lrbd" as="xs:dateTime?"
                  select="f:latest-before(OperationReports/ReportInfo/OperationReportDate, $departure-raw)" />
                <xsl:value-of select="if (exists($lrbd)) then string($lrbd) else ''" />
              </LatestReportBeforeDeparture>

              <Reports>
                <xsl:for-each select="OperationReports/ReportInfo">
                  <xsl:variable name="dt" as="xs:dateTime?"
                    select="f:to-datetime(string(OperationReportDate))" />
                  <xsl:if test="exists($dt) and (empty($report-cutoff) or $dt ge $report-cutoff)">
                    <Report>
                      <OperationReportDate>
                        <xsl:value-of select="string($dt)" />
                      </OperationReportDate>
                      <xsl:if test="exists($tz-offset)">
                        <OperationReportDateNormalized>
                          <xsl:value-of
                            select="string(f:normalize-datetime-to-timezone($dt, $tz-offset))" />
                        </OperationReportDateNormalized>
                      </xsl:if>
                    </Report>
                  </xsl:if>
                </xsl:for-each>
              </Reports>
            </OrderItem>
          </xsl:for-each>
        </OrderItems>
      </Orders>

      <!-- Merged timeline using xsl:for-each-group (Option A) -->
      <MergedReportTimeline>
        <xsl:for-each-group
          select="Orders/OrderItems/OperationReports/ReportInfo/OperationReportDate
                  [exists(f:to-datetime(string(.))) and (empty($report-cutoff) or f:to-datetime(string(.)) ge $report-cutoff)]"
          group-by="string(f:to-datetime(string(.)))">
          <!-- sort must be first child -->
          <xsl:sort select="xs:dateTime(current-grouping-key())" />
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
          <xsl:value-of select="accumulator-after('report-count')" />
        </ReportCount>
        <CutoffApplied>
          <xsl:value-of select="exists($report-cutoff)" />
        </CutoffApplied>
        <TimezoneApplied>
          <xsl:value-of select="exists($tz-offset)" />
        </TimezoneApplied>
      </Meta>
    </ShipmentSummary>
  </xsl:template>

</xsl:stylesheet>