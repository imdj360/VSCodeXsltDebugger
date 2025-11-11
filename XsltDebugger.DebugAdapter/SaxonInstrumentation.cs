using System.Linq;
using System.Xml.Linq;

namespace XsltDebugger.DebugAdapter;

/// <summary>
/// Instrumentation helpers specific to XSLT 2.0/3.0 features (Saxon engine)
/// </summary>
internal static class SaxonInstrumentation
{
    /// <summary>
    /// Instruments xsl:function elements to log parameter values and return expressions.
    /// This is only applicable to XSLT 2.0/3.0 as xsl:function is not available in XSLT 1.0.
    /// </summary>
    public static void InstrumentFunctions(XDocument doc, XNamespace debugNamespace, bool addProbeAttribute)
    {
        if (doc.Root == null)
        {
            return;
        }

        var xsltNamespace = doc.Root.Name.Namespace;

        var functions = doc
            .Descendants()
            .Where(e => e.Name.Namespace == xsltNamespace && e.Name.LocalName == "function")
            .Where(e => e.Attribute("name") != null)
            .ToList();

        if (XsltEngineManager.IsLogEnabled)
        {
            XsltEngineManager.NotifyOutput($"[debug] Instrumenting {functions.Count} function(s) for debugging");
        }

        foreach (var function in functions)
        {
            var functionName = function.Attribute("name")?.Value ?? "unknown";

            // Get all parameters
            var parameters = function.Elements()
                .Where(e => e.Name.Namespace == xsltNamespace && e.Name.LocalName == "param")
                .Where(e => e.Attribute("name") != null)
                .ToList();

            if (parameters.Count == 0)
            {
                if (XsltEngineManager.IsLogEnabled)
                {
                    XsltEngineManager.NotifyOutput($"[debug]   Skipped function {functionName} - no parameters");
                }
                continue;
            }

            // Find the last param element
            var lastParam = parameters.LastOrDefault();
            if (lastParam == null)
            {
                continue;
            }

            // Build a single xsl:message with all parameters
            // Format: [function functionName] param1=$value1, param2=$value2, ...
            var debugMessage = new XElement(xsltNamespace + "message");

            // Add opening text with function name
            debugMessage.Add(new XElement(xsltNamespace + "text", $"[function {functionName}] "));

            // Add each parameter with its value
            for (int i = 0; i < parameters.Count; i++)
            {
                var param = parameters[i];
                var paramName = param.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(paramName))
                {
                    continue;
                }

                // Add "param name="
                debugMessage.Add(new XElement(xsltNamespace + "text", $"{paramName}="));

                // Add the parameter value
                debugMessage.Add(new XElement(xsltNamespace + "value-of", new XAttribute("select", $"${paramName}")));

                // Add comma separator if not the last parameter
                if (i < parameters.Count - 1)
                {
                    debugMessage.Add(new XElement(xsltNamespace + "text", ", "));
                }

                if (XsltEngineManager.IsLogEnabled)
                {
                    XsltEngineManager.NotifyOutput($"[debug]   Instrumented function parameter: {functionName}::{paramName}");
                }
            }

            if (addProbeAttribute)
            {
                debugMessage.SetAttributeValue(debugNamespace + "probe", "1");
            }

            // Insert the combined message after the last parameter
            lastParam.AddAfterSelf(debugMessage);
        }
    }
}
