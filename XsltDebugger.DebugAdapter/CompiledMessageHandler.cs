using System;
using System.Xml.Xsl;

namespace XsltDebugger.DebugAdapter;

/// <summary>
/// Handles xsl:message events from XslCompiledTransform.
/// Provides message capture and debug variable parsing similar to SaxonMessageListener.
/// </summary>
internal sealed class CompiledMessageHandler
{
    /// <summary>
    /// Event handler for XsltMessageEncountered events.
    /// Call this method by subscribing to XsltArgumentList.XsltMessageEncountered.
    /// </summary>
    public void OnMessageEncountered(object sender, XsltMessageEncounteredEventArgs e)
    {
        var messageText = e.Message ?? string.Empty;
        var prefix = "[xsl:message]";

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
}
