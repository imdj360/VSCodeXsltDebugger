using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;

namespace XsltDebugger.DebugAdapter;

internal sealed class DapServer
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly SessionState _state;
    private readonly Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    private readonly object _writeLock = new();
    private int _nextSeq = 1;
    private bool _running = true;
    private readonly object _variablesLock = new();
    private readonly Dictionary<int, Func<List<VariableDescriptor>>> _variableProviders = new();
    private int _nextVariableReference = 1;
    private bool _configurationDone = false;
    private (string engineType, string stylesheet, string xml, bool stopOnEntry, bool debug, LogLevel logLevel)? _pendingLaunch;

    public DapServer(Stream input, Stream output, SessionState state)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public async Task RunAsync()
    {
        while (_running)
        {
            JsonDocument? document = null;
            try
            {
                document = await ReadMessageAsync().ConfigureAwait(false);
                if (document == null)
                {
                    break;
                }

                var root = document.RootElement;
                if (!root.TryGetProperty("type", out var typeProperty) || typeProperty.GetString() != "request")
                {
                    continue;
                }

                var requestSeq = root.GetProperty("seq").GetInt32();
                var command = root.GetProperty("command").GetString() ?? string.Empty;
                var arguments = root.TryGetProperty("arguments", out var args) ? args : default;
                await HandleRequestAsync(requestSeq, command, arguments).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SendOutput($"[ERR] {ex}", isError: true);
            }
            finally
            {
                document?.Dispose();
            }
        }
    }

    public void NotifyStopped(string file, int line, DebugStopReason reason)
    {
        var reasonString = reason switch
        {
            DebugStopReason.Entry => "entry",
            DebugStopReason.Step => "step",
            _ => "breakpoint"
        };
        var body = new
        {
            reason = reasonString,
            threadId = XsltEngineManager.ThreadId,
            allThreadsStopped = true,
            text = $"{Path.GetFileName(file)}:{line}"
        };
        SendEvent("stopped", body);
    }

    public void NotifyOutput(string message)
    {
        SendOutput(message, isError: false);
    }

    public void NotifyTerminated(int exitCode)
    {
        SendEvent("exited", new { exitCode });
        SendEvent("terminated", new { });
    }

    private async Task HandleRequestAsync(int requestSeq, string command, JsonElement arguments)
    {
        try
        {
            var rawArgs = string.Empty;
            try { rawArgs = arguments.ValueKind != JsonValueKind.Undefined ? arguments.GetRawText() : string.Empty; } catch { }
            Console.Error.WriteLine($"[dap] Request: {command}, seq={requestSeq}, args={rawArgs}");
        }
        catch
        {
            // ignore logging failures
        }
        switch (command)
        {
            case "initialize":
                HandleInitialize(requestSeq);
                break;
            case "setBreakpoints":
                HandleSetBreakpoints(requestSeq, arguments);
                break;
            case "configurationDone":
                _configurationDone = true;
                // Extra trace to confirm receipt of configurationDone
                if (XsltEngineManager.IsTraceEnabled)
                {
                    try { SendOutput("[trace] received configurationDone", isError: false); } catch { }
                }
                SendResponse(requestSeq, command, new { });
                TryStartPendingLaunch();
                break;
            case "launch":
                await HandleLaunchAsync(requestSeq, arguments).ConfigureAwait(false);
                break;
            case "threads":
                HandleThreads(requestSeq);
                break;
            case "stackTrace":
                HandleStackTrace(requestSeq);
                break;
            case "scopes":
                HandleScopes(requestSeq);
                break;
            case "variables":
                HandleVariables(requestSeq, arguments);
                break;
            case "continue":
                await HandleContinueAsync(requestSeq, command, engine => engine.ContinueAsync()).ConfigureAwait(false);
                break;
            case "next":
                await HandleContinueAsync(requestSeq, command, engine => engine.StepOverAsync()).ConfigureAwait(false);
                break;
            case "stepIn":
                await HandleContinueAsync(requestSeq, command, engine => engine.StepInAsync()).ConfigureAwait(false);
                break;
            case "stepOut":
                await HandleContinueAsync(requestSeq, command, engine => engine.StepOutAsync()).ConfigureAwait(false);
                break;
            case "evaluate":
                HandleEvaluate(requestSeq, arguments);
                break;
            case "disconnect":
            case "terminate":
                HandleDisconnect(requestSeq, command);
                break;
            default:
                SendResponse(requestSeq, command, new { }, success: false, message: $"Unsupported command '{command}'");
                break;
        }
    }

    private void HandleInitialize(int requestSeq)
    {
        // Per DAP, the initialize response's body is the capabilities object itself
        var capabilities = new
        {
            supportsConfigurationDoneRequest = true,
            supportsTerminateRequest = true,
            supportTerminateDebuggee = true,
            supportsStepInTargetsRequest = false,
            supportsGotoTargetsRequest = false,
            supportsHitConditionalBreakpoints = false,
            supportsSetVariable = false,
            supportsCompletionsRequest = false
        };

        SendResponse(requestSeq, "initialize", capabilities);
        SendEvent("initialized", new { });
    }

    private void HandleSetBreakpoints(int requestSeq, JsonElement arguments)
    {
        var sourcePath = string.Empty;
        if (arguments.TryGetProperty("source", out var source))
        {
            if (source.TryGetProperty("path", out var pathProp))
            {
                sourcePath = pathProp.GetString() ?? string.Empty;
            }
            else if (source.TryGetProperty("name", out var nameProp))
            {
                sourcePath = nameProp.GetString() ?? string.Empty;
            }
        }

        var lines = new List<int>();
        if (arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("breakpoints", out var breakpointsElement) && breakpointsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var bp in breakpointsElement.EnumerateArray())
            {
                if (bp.ValueKind == JsonValueKind.Object && bp.TryGetProperty("line", out var lineElement) && lineElement.TryGetInt32(out var line))
                {
                    lines.Add(line);
                }
            }
        }
        else if (arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("lines", out var linesElement) && linesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var lineElement in linesElement.EnumerateArray())
            {
                if (lineElement.TryGetInt32(out var line))
                {
                    lines.Add(line);
                }
            }
        }
        // DAP already uses 1-based line numbers; keep only positive values for the engine.
        var engineLines = lines.Where(l => l > 0).ToList();

        var resolvedLines = _state.SetBreakpoints(sourcePath, engineLines);
        if (XsltEngineManager.IsTraceEnabled)
        {
            try
            {
                var norm = NormalizePath(sourcePath);
                var linesText = string.Join(",", resolvedLines);
                SendOutput($"[trace] setBreakpoints for '{norm}' => [{linesText}]", isError: false);
            }
            catch { }
        }
        var breakpointBodies = new List<object>();
        foreach (var line in resolvedLines)
        {
            var dapLine = line > 0 ? line : 1;
            breakpointBodies.Add(new { verified = line > 0, line = dapLine });
        }

        SendResponse(requestSeq, "setBreakpoints", new { breakpoints = breakpointBodies });
    }

    private Task HandleLaunchAsync(int requestSeq, JsonElement arguments)
    {
        var engineType = GetString(arguments, "engine") ?? "compiled";
        var stylesheet = GetString(arguments, "stylesheet");
        var xml = GetString(arguments, "xml");
        var stopOnEntry = GetBoolean(arguments, "stopOnEntry");
        var debug = GetBooleanWithDefault(arguments, "debug", true);
        var logLevelStr = GetString(arguments, "logLevel") ?? "log";
        var logLevel = ParseLogLevel(logLevelStr);

        if (string.IsNullOrWhiteSpace(stylesheet) || string.IsNullOrWhiteSpace(xml))
        {
            SendResponse(requestSeq, "launch", new { }, success: false, message: "stylesheet and xml arguments are required.");
            return Task.CompletedTask;
        }

        IXsltEngine engine;
        try
        {
            engine = XsltEngineFactory.CreateEngine(engineType);
        }
        catch (ArgumentException ex)
        {
            SendResponse(requestSeq, "launch", new { }, success: false, message: ex.Message);
            return Task.CompletedTask;
        }

        _state.SetEngine(engine, debug, logLevel);
        var allBreakpoints = _state.GetAllBreakpoints();
        if (debug && allBreakpoints.Count > 0)
        {
            engine.SetBreakpoints(allBreakpoints);
        }

        try
        {
            var normSheet = NormalizePath(stylesheet!);
            var normXml = NormalizePath(xml!);
            if (XsltEngineManager.IsTraceEnabled)
            {
                SendOutput($"[trace] launch engine={engineType}, stylesheet={normSheet}, xml={normXml}, stopOnEntry={stopOnEntry}, debug={debug}, logLevel={logLevel}", isError: false);
            }
        }
        catch { }

        // Queue launch until configurationDone to ensure breakpoints are set and client is ready for 'stopped' events.
        _pendingLaunch = (engineType, stylesheet!, xml!, stopOnEntry, debug, logLevel);
        if (_configurationDone)
        {
            TryStartPendingLaunch();
        }

        SendResponse(requestSeq, "launch", new { });
        if (!_configurationDone && XsltEngineManager.IsTraceEnabled)
        {
            SendOutput("[trace] launch queued until configurationDone", isError: false);
        }
        return Task.CompletedTask;
    }

    private void TryStartPendingLaunch()
    {
        if (_pendingLaunch is null)
        {
            return;
        }

        var launch = _pendingLaunch.Value;
        _pendingLaunch = null;

        var engine = _state.Engine;
        if (engine == null)
        {
            SendOutput("[ERR] No engine available to start.", isError: true);
            return;
        }

        try
        {
            if (XsltEngineManager.IsTraceEnabled)
            {
                SendOutput($"[trace] starting engine now: {launch.engineType}", isError: false);
            }
            var task = engine.StartAsync(launch.stylesheet, launch.xml, launch.stopOnEntry);
            _ = task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    var ex = t.Exception?.GetBaseException() ?? t.Exception;
                    if (ex != null)
                    {
                        try { SendOutput($"[ERR] engine task faulted: {ex.Message}", isError: true); } catch { }
                    }
                }
                else if (t.IsCanceled)
                {
                    if (XsltEngineManager.IsTraceEnabled)
                    {
                        try { SendOutput("[trace] engine task canceled", isError: false); } catch { }
                    }
                }
                else
                {
                    if (XsltEngineManager.IsTraceEnabled)
                    {
                        try { SendOutput("[trace] engine task completed", isError: false); } catch { }
                    }
                }
            });
            if (XsltEngineManager.IsLogEnabled)
            {
                SendOutput($"Starting XSLT transform using engine '{launch.engineType}'.", isError: false);
            }
        }
        catch (Exception ex)
        {
            SendOutput($"[ERR] Failed to start engine: {ex.Message}", isError: true);
        }
    }

    private static string NormalizePath(string path)
    {
        var result = path ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(result))
        {
            if (result.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                try { result = new Uri(result).LocalPath; } catch { }
            }
            try { result = Path.GetFullPath(result); } catch { }
        }
        return result;
    }

    private void HandleThreads(int requestSeq)
    {
        var threads = new[]
        {
            new { id = XsltEngineManager.ThreadId, name = "XSLT Engine" }
        };
        SendResponse(requestSeq, "threads", new { threads });
    }

    private void HandleStackTrace(int requestSeq)
    {
        var stop = XsltEngineManager.LastStop;
        if (stop == null)
        {
            SendResponse(requestSeq, "stackTrace", new { stackFrames = Array.Empty<object>(), totalFrames = 0 });
            return;
        }

        var (file, line) = stop.Value;
        var frame = new
        {
            id = 1,
            name = "main",
            line,
            column = 1,
            source = new
            {
                name = Path.GetFileName(file),
                path = file
            }
        };

        SendResponse(requestSeq, "stackTrace", new { stackFrames = new[] { frame }, totalFrames = 1 });
    }

    private void HandleScopes(int requestSeq)
    {
        ClearVariableProviders();

        var context = XsltEngineManager.LastContext;
        var contextReference = 0;
        if (context != null)
        {
            var snapshot = context.Clone();
            contextReference = RegisterVariables(() => BuildContextVariables(snapshot.Clone()));
        }

        // Add XSLT Variables scope - always show it, even if empty
        var xsltVariablesReference = RegisterVariables(() => BuildXsltVariables());

        var scopes = new[]
        {
            new { name = "Context", variablesReference = contextReference, expensive = false },
            new { name = "XSLT Variables", variablesReference = xsltVariablesReference, expensive = false }
        };
        SendResponse(requestSeq, "scopes", new { scopes });
    }

    private void HandleVariables(int requestSeq, JsonElement arguments)
    {
        var reference = GetInt(arguments, "variablesReference");
        var variables = ResolveVariables(reference);
        SendResponse(requestSeq, "variables", new { variables });
    }

    private void HandleEvaluate(int requestSeq, JsonElement arguments)
    {
        var expression = GetString(arguments, "expression");
        if (string.IsNullOrWhiteSpace(expression))
        {
            SendResponse(requestSeq, "evaluate", new { result = string.Empty, variablesReference = 0 }, success: false, message: "Expression is empty.");
            return;
        }

        var context = XsltEngineManager.LastContext;
        if (XsltEngineManager.IsTraceEnabled)
        {
            SendOutput($"[trace] HandleEvaluate: expression='{expression}', context={(context != null ? $"available (node={context.Name})" : "NULL")}", isError: false);
        }

        if (context == null)
        {
            const string noContextMessage = "No active XSLT context available for evaluation.";
            SendResponse(requestSeq, "evaluate", new { result = noContextMessage, variablesReference = 0 }, success: false, message: noContextMessage);
            return;
        }

        try
        {
            var navigator = context.Clone();
            var evaluationResult = EvaluateXPath(navigator, expression);
            var formatted = FormatEvaluationResult(evaluationResult, out var variablesReference);

            if (XsltEngineManager.IsTraceAllEnabled)
            {
                SendOutput($"[traceall] XPath evaluation: {expression}\n  Result: {formatted}", isError: false);
            }

            SendResponse(requestSeq, "evaluate", new { result = formatted, variablesReference });
        }
        catch (Exception ex)
        {
            SendResponse(requestSeq, "evaluate", new { result = ex.Message, variablesReference = 0 }, success: false, message: ex.Message);
        }
    }

    private void ClearVariableProviders()
    {
        lock (_variablesLock)
        {
            _variableProviders.Clear();
            _nextVariableReference = 1;
        }
    }

    private int RegisterVariables(Func<List<VariableDescriptor>> provider)
    {
        if (provider == null)
        {
            return 0;
        }

        lock (_variablesLock)
        {
            var reference = _nextVariableReference++;
            _variableProviders[reference] = provider;
            return reference;
        }
    }

    private List<object> ResolveVariables(int reference)
    {
        if (reference == 0)
        {
            return new List<object>();
        }

        Func<List<VariableDescriptor>>? provider;
        lock (_variablesLock)
        {
            if (!_variableProviders.TryGetValue(reference, out provider))
            {
                return new List<object>();
            }
        }

        var descriptors = provider();
        var variables = new List<object>();
        foreach (var descriptor in descriptors)
        {
            var childReference = descriptor.ChildProvider != null ? descriptor.ChildProvider() : 0;
            variables.Add(new
            {
                name = descriptor.Name,
                value = descriptor.Value,
                type = descriptor.Type,
                variablesReference = childReference
            });
        }
        return variables;
    }

    private List<VariableDescriptor> BuildXsltVariables()
    {
        var variables = new List<VariableDescriptor>();
        foreach (var kvp in XsltEngineManager.Variables)
        {
            variables.Add(new VariableDescriptor
            {
                Name = $"${kvp.Key}",
                Value = kvp.Value?.ToString() ?? "null",
                Type = kvp.Value?.GetType().Name ?? "null"
            });
        }
        return variables;
    }

    private List<VariableDescriptor> BuildContextVariables(XPathNavigator? context)
    {
        var variables = new List<VariableDescriptor>();
        if (context == null)
        {
            variables.Add(new VariableDescriptor
            {
                Name = "(no context)",
                Value = string.Empty,
                Type = "string"
            });
            return variables;
        }

        var nav = context.Clone();
        variables.Add(new VariableDescriptor { Name = "name", Value = nav.Name, Type = "string" });
        variables.Add(new VariableDescriptor { Name = "localName", Value = nav.LocalName, Type = "string" });
        variables.Add(new VariableDescriptor { Name = "namespaceUri", Value = nav.NamespaceURI, Type = "string" });
        variables.Add(new VariableDescriptor { Name = "nodeType", Value = nav.NodeType.ToString(), Type = "string" });
        variables.Add(new VariableDescriptor { Name = "value", Value = nav.Value ?? string.Empty, Type = "string" });
        variables.Add(new VariableDescriptor { Name = "baseUri", Value = nav.BaseURI ?? string.Empty, Type = "string" });

        if (nav.HasAttributes)
        {
            variables.Add(new VariableDescriptor
            {
                Name = "attributes",
                Value = string.Empty,
                Type = "array",
                ChildProvider = CreateAttributesProvider(nav.Clone())
            });
        }

        if (nav.HasChildren)
        {
            variables.Add(new VariableDescriptor
            {
                Name = "children",
                Value = string.Empty,
                Type = "array",
                ChildProvider = CreateChildrenProvider(nav.Clone())
            });
        }

        return variables;
    }

    private Func<int> CreateAttributesProvider(XPathNavigator navigator)
    {
        return () => RegisterVariables(() => BuildAttributeVariables(navigator.Clone()));
    }

    private List<VariableDescriptor> BuildAttributeVariables(XPathNavigator navigator)
    {
        var attributes = new List<VariableDescriptor>();
        if (!navigator.MoveToFirstAttribute())
        {
            return attributes;
        }

        do
        {
            var name = navigator.Name;
            var value = navigator.Value ?? string.Empty;
            attributes.Add(new VariableDescriptor
            {
                Name = $"@{name}",
                Value = value,
                Type = "attribute"
            });
        }
        while (navigator.MoveToNextAttribute());

        return attributes;
    }

    private Func<int> CreateChildrenProvider(XPathNavigator navigator)
    {
        return () => RegisterVariables(() => BuildChildVariables(navigator.Clone()));
    }

    private List<VariableDescriptor> BuildChildVariables(XPathNavigator navigator)
    {
        var children = new List<VariableDescriptor>();
        if (!navigator.MoveToFirstChild())
        {
            return children;
        }

        var index = 0;
        do
        {
            var childSnapshot = navigator.Clone();
            Func<int>? childProvider = null;
            if (childSnapshot.HasChildren || childSnapshot.HasAttributes)
            {
                var nested = childSnapshot.Clone();
                childProvider = () => RegisterVariables(() => BuildContextVariables(nested.Clone()));
            }

            children.Add(new VariableDescriptor
            {
                Name = $"[{index}] {DescribeNode(childSnapshot)}",
                Value = childSnapshot.Value ?? string.Empty,
                Type = childSnapshot.NodeType.ToString(),
                ChildProvider = childProvider
            });
            index++;
        }
        while (navigator.MoveToNext());

        return children;
    }

    private List<VariableDescriptor> BuildNodeListVariables(IReadOnlyList<XPathNavigator> nodes)
    {
        var list = new List<VariableDescriptor>();
        for (var i = 0; i < nodes.Count; i++)
        {
            var snapshot = nodes[i].Clone();
            Func<int>? provider = null;
            if (snapshot.HasChildren || snapshot.HasAttributes)
            {
                var nested = snapshot.Clone();
                provider = () => RegisterVariables(() => BuildContextVariables(nested.Clone()));
            }

            list.Add(new VariableDescriptor
            {
                Name = $"[{i}] {DescribeNode(snapshot)}",
                Value = snapshot.Value ?? string.Empty,
                Type = snapshot.NodeType.ToString(),
                ChildProvider = provider
            });
        }
        return list;
    }

    private static string DescribeNode(XPathNavigator navigator)
    {
        return navigator.NodeType switch
        {
            XPathNodeType.Element => string.IsNullOrEmpty(navigator.Prefix)
                ? $"<{navigator.LocalName}>"
                : $"<{navigator.Prefix}:{navigator.LocalName}>",
            XPathNodeType.Attribute => $"@{navigator.Name}",
            XPathNodeType.Text => "text()",
            XPathNodeType.Comment => "comment()",
            XPathNodeType.ProcessingInstruction => $"processing-instruction({navigator.Name})",
            XPathNodeType.Root => "root",
            _ => navigator.Name ?? navigator.NodeType.ToString()
        };
    }

    private object EvaluateXPath(XPathNavigator navigator, string expression)
    {
        var manager = CreateNamespaceManager(navigator);
        var compiled = XPathExpression.Compile(expression);
        compiled.SetContext(manager);
        return navigator.Evaluate(compiled);
    }

    private static XmlNamespaceManager CreateNamespaceManager(XPathNavigator navigator)
    {
        var manager = new XmlNamespaceManager(navigator.NameTable);
        foreach (var kvp in navigator.GetNamespacesInScope(XmlNamespaceScope.All))
        {
            var prefix = string.IsNullOrEmpty(kvp.Key) ? string.Empty : kvp.Key;
            manager.AddNamespace(prefix, kvp.Value);
        }
        return manager;
    }

    private string FormatEvaluationResult(object? value, out int variablesReference)
    {
        variablesReference = 0;
        if (value == null)
        {
            return "null";
        }

        switch (value)
        {
            case string s:
                return s;
            case double d:
                return d.ToString(CultureInfo.InvariantCulture);
            case bool b:
                return b ? "true" : "false";
            case XPathNodeIterator iterator:
                var nodes = MaterializeNodes(iterator);
                if (nodes.Count > 0)
                {
                    variablesReference = RegisterVariables(() => BuildNodeListVariables(nodes));
                }
                return $"{nodes.Count} node(s)";
            default:
                return value.ToString() ?? string.Empty;
        }
    }

    private static List<XPathNavigator> MaterializeNodes(XPathNodeIterator iterator)
    {
        var list = new List<XPathNavigator>();
        var clone = iterator.Clone();
        while (clone.MoveNext())
        {
            if (clone.Current != null)
            {
                list.Add(clone.Current.Clone());
            }
        }
        return list;
    }

    private async Task HandleContinueAsync(int requestSeq, string command, Func<IXsltEngine, Task> action)
    {
        var engine = _state.Engine;
        if (engine != null)
        {
            await action(engine).ConfigureAwait(false);
        }
        ClearVariableProviders();
        SendResponse(requestSeq, command, new { allThreadsContinued = true });
    }

    private void HandleDisconnect(int requestSeq, string command)
    {
        _running = false;
        _state.ClearEngine();
        SendResponse(requestSeq, command, new { });
    }

    private async Task<JsonDocument?> ReadMessageAsync()
    {
        int contentLength = 0;
        while (true)
        {
            var headerLine = await ReadLineAsync().ConfigureAwait(false);
            if (headerLine == null)
            {
                return null;
            }

            if (headerLine.Length == 0)
            {
                break;
            }

            if (headerLine.StartsWith("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                var parts = headerLine.Split(':', 2);
                if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var length))
                {
                    contentLength = length;
                }
            }
        }

        if (contentLength <= 0)
        {
            return null;
        }

        var buffer = new byte[contentLength];
        var read = 0;
        while (read < contentLength)
        {
            var chunk = await _input.ReadAsync(buffer, read, contentLength - read).ConfigureAwait(false);
            if (chunk == 0)
            {
                return null;
            }
            read += chunk;
        }

        var json = _encoding.GetString(buffer, 0, read);
        return JsonDocument.Parse(json);
    }

    private async Task<string?> ReadLineAsync()
    {
        var builder = new StringBuilder();
        var buffer = new byte[1];

        while (true)
        {
            var read = await _input.ReadAsync(buffer, 0, 1).ConfigureAwait(false);
            if (read == 0)
            {
                return builder.Length == 0 ? null : builder.ToString();
            }

            var ch = (char)buffer[0];
            if (ch == '\r')
            {
                var peek = await _input.ReadAsync(buffer, 0, 1).ConfigureAwait(false);
                if (peek == 0)
                {
                    break;
                }

                if ((char)buffer[0] != '\n')
                {
                    builder.Append(ch);
                    builder.Append((char)buffer[0]);
                    continue;
                }

                break;
            }
            if (ch == '\n')
            {
                break;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private void SendResponse(int requestSeq, string command, object body, bool success = true, string? message = null)
    {
        var payload = new
        {
            seq = NextSequence(),
            type = "response",
            request_seq = requestSeq,
            success,
            command,
            message,
            body
        };
        SendMessage(payload);
    }

    private void SendEvent(string eventName, object body)
    {
        var payload = new
        {
            seq = NextSequence(),
            type = "event",
            @event = eventName,
            body
        };
        SendMessage(payload);
    }

    private void SendOutput(string message, bool isError)
    {
        var text = message.EndsWith("\n", StringComparison.Ordinal) ? message : message + "\n";
        var payload = new
        {
            output = text,
            category = isError ? "stderr" : "stdout"
        };
        SendEvent("output", payload);
    }

    private void SendMessage(object payload)
    {
        var json = JsonSerializer.Serialize(payload, _serializerOptions);
        var jsonBytes = _encoding.GetBytes(json);
        var header = $"Content-Length: {jsonBytes.Length}\r\n\r\n";
        var headerBytes = _encoding.GetBytes(header);

        lock (_writeLock)
        {
            _output.Write(headerBytes, 0, headerBytes.Length);
            _output.Write(jsonBytes, 0, jsonBytes.Length);
            _output.Flush();
        }
    }

    private int NextSequence() => Interlocked.Increment(ref _nextSeq);

    private sealed class VariableDescriptor
    {
        public string Name { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
        public string? Type { get; init; }
        public Func<int>? ChildProvider { get; init; }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null)
        {
            return value.GetString();
        }
        return null;
    }

    private static int GetInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        if (!element.TryGetProperty(propertyName, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return 0;
    }

    private static bool GetBoolean(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!element.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt32(out var number) => number != 0,
            _ => false
        };
    }

    private static bool GetBooleanWithDefault(JsonElement element, string propertyName, bool defaultValue)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return defaultValue;
        }

        if (!element.TryGetProperty(propertyName, out var value))
        {
            return defaultValue;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt32(out var number) => number != 0,
            _ => defaultValue
        };
    }

    private static LogLevel ParseLogLevel(string logLevelStr)
    {
        return logLevelStr?.ToLowerInvariant() switch
        {
            "none" => LogLevel.None,
            "log" => LogLevel.Log,
            "trace" => LogLevel.Trace,
            "traceall" => LogLevel.TraceAll,
            _ => LogLevel.Log // Default to log level
        };
    }
}
