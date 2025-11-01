<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:msxsl="urn:schemas-microsoft-com:xslt"
                xmlns:user="urn:my-scripts">

  <msxsl:script language="C#" implements-prefix="user">
    <![CDATA[
using System;
using System.Globalization;

public class MathHelper {
    // Private fields
    private int counter = 0;
    private string prefix = "Result: ";
    private double multiplier = 2.5;
    private int[] cachedValues = new int[] { 10, 20, 30, 40, 50 };

    // Methods using private variables
    public int IncrementCounter() {
        counter++;
        return counter;
    }

    public int GetCounterValue() {
        return counter;
    }

    public string GetPrefixedMessage(string message) {
        string result = prefix + message;
        return result;
    }

    public double ApplyMultiplier(double value) {
        double result = value * multiplier;
        return result;
    }

    public int GetCachedValue(int index) {
        if (index >= 0 && index < cachedValues.Length) {
            int value = cachedValues[index];
            return value;
        }
        return -1;
    }

    public int SumCachedValues() {
        int sum = 0;
        for (int i = 0; i < cachedValues.Length; i++) {
            sum += cachedValues[i];
        }
        return sum;
    }

    // Integer operations
    public int Add(int a, int b) {
        return a + b;
    }

    public int Multiply(int a, int b) {
        int result = a * b;
        return result;
    }

    public string FormatNumber(int num) {
        return num.ToString("N0", CultureInfo.InvariantCulture);
    }

    // Double operations
    public double AddDouble(double a, double b) {
        double result = a + b;
        return result;
    }

    public double DivideDouble(double a, double b) {
        if (b == 0) return 0;
        return a / b;
    }

    public string FormatDouble(double num) {
        return num.ToString("F2", CultureInfo.InvariantCulture);
    }

    // Decimal operations
    public decimal AddDecimal(decimal a, decimal b) {
        decimal result = a + b;
        return result;
    }

    public decimal MultiplyDecimal(decimal a, decimal b) {
        return a * b;
    }

    public string FormatCurrency(decimal amount) {
        return amount.ToString("C2", CultureInfo.InvariantCulture);
    }

    // DateTime operations
    public string GetCurrentDate() {
        DateTime now = DateTime.Now;
        return now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public string FormatDate(int year, int month, int day) {
        DateTime date = new DateTime(year, month, day);
        return date.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture);
    }

    public int GetDaysDifference(int year1, int month1, int day1, int year2, int month2, int day2) {
        DateTime date1 = new DateTime(year1, month1, day1);
        DateTime date2 = new DateTime(year2, month2, day2);
        TimeSpan diff = date2 - date1;
        return diff.Days;
    }

    // String operations
    public string ConcatStrings(string a, string b) {
        string result = a + " " + b;
        return result;
    }

    public string ToUpperCase(string text) {
        return text.ToUpper();
    }

    public string ReverseString(string text) {
        char[] charArray = text.ToCharArray();
        Array.Reverse(charArray);
        return new string(charArray);
    }

    public int GetStringLength(string text) {
        return text.Length;
    }

    // Array operations
    public int SumArray(int count) {
        int[] numbers = new int[count];
        for (int i = 0; i < count; i++) {
            numbers[i] = i + 1;
        }

        int sum = 0;
        for (int i = 0; i < numbers.Length; i++) {
            sum += numbers[i];
        }
        return sum;
    }

    public string JoinStringArray() {
        string[] words = new string[] { "XSLT", "Debugger", "Test" };
        string result = "";
        for (int i = 0; i < words.Length; i++) {
            if (i > 0) result += " ";
            result += words[i];
        }
        return result;
    }

    public double AverageDoubles(int count) {
        double[] values = new double[count];
        for (int i = 0; i < count; i++) {
            values[i] = (i + 1) * 1.5;
        }

        double sum = 0;
        for (int i = 0; i < values.Length; i++) {
            sum += values[i];
        }
        double average = sum / values.Length;
        return average;
    }

    public int FindMaxInArray(int size) {
        int[] numbers = new int[size];
        for (int i = 0; i < size; i++) {
            numbers[i] = (i * 7) % 100;
        }

        int max = numbers[0];
        for (int i = 1; i < numbers.Length; i++) {
            if (numbers[i] > max) {
                max = numbers[i];
            }
        }
        return max;
    }

    public string ReverseArray(int count) {
        int[] numbers = new int[count];
        for (int i = 0; i < count; i++) {
            numbers[i] = i + 1;
        }

        Array.Reverse(numbers);

        string result = "";
        for (int i = 0; i < numbers.Length; i++) {
            if (i > 0) result += ",";
            result += numbers[i].ToString();
        }
        return result;
    }
}
]]>
  </msxsl:script>

  <xsl:template match="/">
    <html>
      <head>
        <title>Auto-Instrumentation Test</title>
      </head>
      <body>
        <h1>C# Method Auto-Instrumentation Test</h1>

        <h2>Private Field Access</h2>
        <p>Increment counter (1st call): <xsl:value-of select="user:IncrementCounter()"/></p>
        <p>Increment counter (2nd call): <xsl:value-of select="user:IncrementCounter()"/></p>
        <p>Increment counter (3rd call): <xsl:value-of select="user:IncrementCounter()"/></p>
        <p>Get counter value: <xsl:value-of select="user:GetCounterValue()"/></p>
        <p>Prefixed message: <xsl:value-of select="user:GetPrefixedMessage('Hello')"/></p>
        <p>Apply multiplier to 10: <xsl:value-of select="user:ApplyMultiplier(10)"/></p>
        <p>Cached value at index 2: <xsl:value-of select="user:GetCachedValue(2)"/></p>
        <p>Sum of cached values: <xsl:value-of select="user:SumCachedValues()"/></p>

        <h2>Integer Operations</h2>
        <p>Add 5 + 3 = <xsl:value-of select="user:Add(5, 3)"/></p>
        <p>Multiply 4 * 7 = <xsl:value-of select="user:Multiply(4, 7)"/></p>
        <p>Formatted: <xsl:value-of select="user:FormatNumber(1000000)"/></p>

        <h2>Double Operations</h2>
        <p>Add 3.14 + 2.86 = <xsl:value-of select="user:AddDouble(3.14, 2.86)"/></p>
        <p>Divide 10.5 / 2.5 = <xsl:value-of select="user:DivideDouble(10.5, 2.5)"/></p>
        <p>Formatted Double: <xsl:value-of select="user:FormatDouble(123.456)"/></p>

        <h2>Decimal Operations</h2>
        <p>Add Decimal 10.50 + 20.75 = <xsl:value-of select="user:AddDecimal(10.50, 20.75)"/></p>
        <p>Multiply Decimal 5.5 * 2 = <xsl:value-of select="user:MultiplyDecimal(5.5, 2)"/></p>
        <p>Currency: <xsl:value-of select="user:FormatCurrency(1234.56)"/></p>

        <h2>DateTime Operations</h2>
        <p>Current Date: <xsl:value-of select="user:GetCurrentDate()"/></p>
        <p>Formatted Date: <xsl:value-of select="user:FormatDate(2025, 11, 1)"/></p>
        <p>Days between 2025-01-01 and 2025-12-31: <xsl:value-of select="user:GetDaysDifference(2025, 1, 1, 2025, 12, 31)"/></p>

        <h2>String Operations</h2>
        <p>Concat: <xsl:value-of select="user:ConcatStrings('Hello', 'World')"/></p>
        <p>Uppercase: <xsl:value-of select="user:ToUpperCase('xslt debugger')"/></p>
        <p>Reverse: <xsl:value-of select="user:ReverseString('XSLT')"/></p>
        <p>Length of 'Testing': <xsl:value-of select="user:GetStringLength('Testing')"/></p>

        <h2>Array Operations</h2>
        <p>Sum of array [1,2,3,4,5]: <xsl:value-of select="user:SumArray(5)"/></p>
        <p>Join string array: <xsl:value-of select="user:JoinStringArray()"/></p>
        <p>Average of doubles [1.5, 3.0, 4.5, 6.0]: <xsl:value-of select="user:AverageDoubles(4)"/></p>
        <p>Max in array (size 10): <xsl:value-of select="user:FindMaxInArray(10)"/></p>
        <p>Reverse array [1,2,3,4,5,6]: <xsl:value-of select="user:ReverseArray(6)"/></p>
      </body>
    </html>
  </xsl:template>

</xsl:stylesheet>
