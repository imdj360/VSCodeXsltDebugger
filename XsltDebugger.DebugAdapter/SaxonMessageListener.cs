using System;
using Saxon.Api;

namespace XsltDebugger.DebugAdapter;

internal sealed class SaxonMessageListener : IMessageListener2
{
    public void Message(XdmNode content, QName errorCode, bool terminate, IXmlLocation location)
    {
        var messageText = content?.StringValue ?? string.Empty;
        var locationText = FormatLocation(location);
        var prefix = terminate ? "[xsl:message terminate]" : "[xsl:message]";
        if (!string.IsNullOrEmpty(locationText))
        {
            prefix = $"{prefix} {locationText}";
        }
        if (!string.IsNullOrEmpty(messageText))
        {
            XsltEngineManager.NotifyOutput($"{prefix} {messageText}");

            // Parse debug messages in format: [DBG] variableName value
            TryParseDebugMessage(messageText);
        }
        else
        {
            XsltEngineManager.NotifyOutput(prefix);
        }
    }

    private static void TryParseDebugMessage(string messageText)
    {
        // Expected format: "[DBG] variableName value"
        // or sequence format: "[DBG] variableName value" (from xsl:message select="('[DBG]', 'varName', string($var))")

        if (string.IsNullOrWhiteSpace(messageText))
        {
            return;
        }

        var trimmed = messageText.Trim();

        // Handle sequence format: "[DBG] variableName value"
        if (trimmed.StartsWith("[DBG]", StringComparison.OrdinalIgnoreCase))
        {
            // Remove "[DBG]" prefix
            var content = trimmed.Substring(5).Trim();

            // Split into variable name and value
            // Expected: "variableName value"
            var parts = content.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 1)
            {
                var varName = parts[0].Trim();
                var varValue = parts.Length >= 2 ? parts[1].Trim() : string.Empty;

                // Add to Variables dictionary
                XsltEngineManager.Variables[varName] = varValue;

                if (XsltEngineManager.IsLogEnabled)
                {
                    XsltEngineManager.NotifyOutput($"[debug] Captured variable: ${varName} = {varValue}");
                }
            }
        }
    }

    private static string FormatLocation(IXmlLocation location)
    {
        if (location == null)
        {
            return string.Empty;
        }

        var uri = location.BaseUri;
        var line = location.LineNumber;
        var lineString = line > 0 ? $":{line}" : string.Empty;

        if (uri == null && string.IsNullOrEmpty(lineString))
        {
            return string.Empty;
        }

        var path = string.Empty;
        if (uri != null)
        {
            path = uri.IsFile ? uri.LocalPath : uri.ToString();
        }

        return $"{path}{lineString}".Trim();
    }
}
