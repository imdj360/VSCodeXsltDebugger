# Consolidation of Best Practices
**Presenter: Daniel Jonathan**

---

## Table of Contents
1. [Key Learnings from Past Projects](#key-learnings)
   - Architecture & Design Patterns
   - Code Organization Principles
   - Testing Strategies
   - Error Handling Best Practices
   - Cross-Platform Development
   - Dependency Management
   - AI-Assisted Development ("Vibecoding")
2. [Reusable Patterns and Tools](#reusable-patterns)
   - Code Analysis & Transformation
   - Runtime Code Evaluation
   - Structured Logging
   - Asynchronous Patterns
   - Build & Packaging Automation
   - Testing Tools
3. [Recommendations for Standardization](#recommendations)
   - Architecture Standards
   - Code Quality Standards
   - Testing Standards
   - Build & Deployment Standards
   - Documentation Standards
   - Error Handling Standards
   - Version Control Standards
   - Security Standards

---

## Key Learnings from Past Projects

### 1. Architecture & Design Patterns

#### Strategy Pattern for Pluggable Components
**Learning**: When you need to support multiple implementations of the same functionality, use the Strategy pattern.

**Example Pattern**:
```csharp
// Define interface for abstraction
public interface IEngine
{
    Task StartAsync(string input);
    Task ContinueAsync();
    void SetBreakpoints(IEnumerable<(string file, int line)> breakpoints);
}

// Multiple implementations
public class EngineA : IEngine { /* Implementation A */ }
public class EngineB : IEngine { /* Implementation B */ }

// Factory for creation
public static class EngineFactory
{
    public static IEngine CreateEngine(EngineType type)
    {
        return type switch
        {
            EngineType.A => new EngineA(),
            EngineType.B => new EngineB(),
            _ => throw new ArgumentException($"Unknown engine: {type}")
        };
    }
}
```

**Benefits**:
- Add new implementations without modifying existing code
- Clients depend on abstractions, not concrete types
- Easy to test with mock implementations

---

#### Event-Based Communication (Publisher-Subscriber Pattern)
**Learning**: For cross-component communication where components shouldn't be tightly coupled, use event buses.

**Example Pattern**:
```csharp
public static class EventManager
{
    public static event Action<string, int>? ComponentStopped;
    public static event Action<string>? ComponentOutput;
    public static event Action<int>? ComponentTerminated;

    public static void NotifyStopped(string file, int line)
    {
        ComponentStopped?.Invoke(file, line);
    }

    public static void NotifyOutput(string message)
    {
        ComponentOutput?.Invoke(message);
    }
}

// Publishers
public class Worker
{
    public void DoWork()
    {
        EventManager.NotifyOutput("Working...");
        EventManager.NotifyStopped("file.cs", 42);
    }
}

// Subscribers
public class Monitor
{
    public Monitor()
    {
        EventManager.ComponentStopped += OnStopped;
        EventManager.ComponentOutput += OnOutput;
    }

    private void OnStopped(string file, int line) { /* Handle */ }
    private void OnOutput(string message) { /* Handle */ }
}
```

**Benefits**:
- Loose coupling between components
- Easy to add new subscribers without modifying publishers
- Centralized event coordination

**When to Use**:
- Multiple components need to react to the same events
- Publisher doesn't need to know about subscribers
- Components run in separate contexts or threads

---

#### Adapter Pattern for Protocol Translation
**Learning**: When integrating with external systems or protocols, use adapters to translate between formats.

**Example Pattern**:
```csharp
// External system messages
public class ExternalMessage
{
    public string Type { get; set; }
    public Dictionary<string, object> Data { get; set; }
}

// Your internal protocol
public class InternalEvent
{
    public string EventType { get; set; }
    public object Payload { get; set; }
}

// Adapter translates between them
public class ProtocolAdapter
{
    public InternalEvent TranslateToInternal(ExternalMessage external)
    {
        return new InternalEvent
        {
            EventType = MapEventType(external.Type),
            Payload = TransformPayload(external.Data)
        };
    }

    public ExternalMessage TranslateToExternal(InternalEvent internal)
    {
        return new ExternalMessage
        {
            Type = MapMessageType(internal.EventType),
            Data = SerializePayload(internal.Payload)
        };
    }
}
```

**Benefits**:
- Isolates protocol-specific logic
- Makes it easy to swap protocols
- Simplifies testing with mock protocols

---

### 2. Code Organization Principles

#### Single Responsibility Principle in Practice
**Learning**: Organize code into clear layers, each with a single responsibility.

**Recommended Layer Structure**:
```
/YourProject/
├── Protocol/          # External communication (REST, gRPC, DAP, etc.)
│   ├── Server.cs
│   └── MessageHandler.cs
├── Session/           # State management
│   ├── SessionState.cs
│   └── StateManager.cs
├── Engine/            # Core business logic abstraction
│   ├── IEngine.cs
│   ├── EngineFactory.cs
│   └── Implementations/
│       ├── EngineA.cs
│       └── EngineB.cs
├── Instrumentation/   # Code transformation/analysis
│   ├── CodeInstrumenter.cs
│   └── AstVisitor.cs
└── Utilities/         # Cross-cutting concerns
    ├── Logger.cs
    └── Evaluator.cs
```

**Layer Responsibilities**:
- **Protocol**: Handle external communication, parse/serialize messages
- **Session**: Manage state, configuration, lifecycle
- **Engine**: Core domain logic, business rules
- **Instrumentation**: Transform/analyze code or data
- **Utilities**: Reusable helpers, logging, evaluation

**Benefits**:
- Clear boundaries make code easier to navigate
- Changes in one layer rarely affect others
- Easy to locate where functionality belongs

---

#### One Public Type Per File
**Learning**: Keep files focused by limiting to one primary public type per file.

**Example**:
```
✅ Good:
- UserService.cs          (contains UserService class)
- IUserRepository.cs      (contains IUserRepository interface)
- ValidationException.cs  (contains ValidationException class)

❌ Avoid:
- Services.cs             (contains UserService, OrderService, ProductService)
- Interfaces.cs           (contains IUserRepo, IOrderRepo, IProductRepo)
```

**Exceptions**:
- Tightly coupled internal types (e.g., `HelperClass` only used by `MainClass`)
- Small DTOs or enums closely related to the main type

---

#### Namespace Strategy
**Learning**: Use namespaces to reflect architectural layers, not folder structure.

**Recommended Approach**:
```csharp
// Layer-based namespaces
namespace YourApp.Protocol;      // All protocol handling
namespace YourApp.Session;       // All state management
namespace YourApp.Engine;        // Core business logic
namespace YourApp.Utilities;     // Cross-cutting concerns

// NOT folder-based
namespace YourApp.Folder1.Subfolder2.Subfolder3;  // ❌ Too nested
```

**Benefits**:
- Shorter, more meaningful `using` statements
- Easier refactoring (can move files without changing namespaces)
- Clear architectural intent

---

### 3. Testing Strategies

#### Integration Tests with Task-Based Async Coordination
**Learning**: For event-driven systems, use `TaskCompletionSource` to coordinate async events in tests.

**Pattern**:
```csharp
[Fact]
public async Task Should_Handle_Event_Workflow()
{
    // Arrange
    var eventReceived = new TaskCompletionSource<(string file, int line)>();

    EventManager.ComponentStopped += (file, line) =>
    {
        eventReceived.TrySetResult((file, line));
    };

    var component = new Worker();

    // Act
    await component.StartAsync("input.txt");

    // Wait for event with timeout
    var result = await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

    // Assert
    result.file.Should().Be("expected.txt");
    result.line.Should().Be(42);
}
```

**Benefits**:
- Clean async test code
- Built-in timeout protection
- Works with event-driven architectures

---

#### Test Data Organization
**Learning**: Organize test fixtures in a clear, engine/scenario-specific structure.

**Recommended Structure**:
```
/TestData/
├── Integration/
│   ├── EngineA/
│   │   ├── simple-case.input
│   │   ├── complex-case.input
│   │   └── edge-case.input
│   ├── EngineB/
│   │   ├── simple-case.input
│   │   └── advanced-case.input
│   └── Common/
│       ├── shared-data.xml
│       └── shared-config.json
└── Unit/
    └── mock-responses.json
```

**Benefits**:
- Easy to locate test data for specific scenarios
- Shared data in common folder avoids duplication
- Clear separation between unit and integration fixtures

---

#### Path Resolution in Tests
**Learning**: Calculate paths relative to test assembly location, not hardcoded absolute paths.

**Pattern**:
```csharp
public static class TestHelpers
{
    private static string? _repoRoot;

    public static string GetRepositoryRoot()
    {
        if (_repoRoot != null) return _repoRoot;

        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        var directory = Path.GetDirectoryName(assemblyPath)!;

        // Walk up until we find the solution file
        while (!File.Exists(Path.Combine(directory, "YourProject.sln")))
        {
            directory = Path.GetDirectoryName(directory);
            if (directory == null)
                throw new InvalidOperationException("Could not find repository root");
        }

        _repoRoot = directory;
        return _repoRoot;
    }

    public static string GetTestDataPath(string relativePath)
    {
        return Path.Combine(GetRepositoryRoot(), "TestData", relativePath);
    }
}

// Usage in tests
[Fact]
public void Test_WithData()
{
    var inputPath = TestHelpers.GetTestDataPath("Integration/EngineA/simple-case.input");
    // Test code...
}
```

**Benefits**:
- Tests work on any machine without path adjustments
- Works in CI/CD environments
- Easy to reorganize test project location

---

### 4. Error Handling Best Practices

#### Graceful Degradation in Non-Critical Paths
**Learning**: Logging and diagnostics should never crash the main application.

**Pattern**:
```csharp
public void ProcessRequest(Request request)
{
    // Critical path - propagate exceptions
    var data = ValidateAndParse(request);  // Let this throw if invalid

    // Non-critical logging - catch and continue
    try
    {
        var diagnosticInfo = ExtractDiagnostics(request);
        Logger.LogTrace(diagnosticInfo);
    }
    catch
    {
        // Diagnostic failure doesn't affect processing
    }

    // Critical path continues
    return ProcessData(data);
}
```

**Guidelines**:
- **Critical paths**: Let exceptions propagate (validation, business logic)
- **Diagnostic paths**: Catch and suppress (logging, metrics, debugging)
- **External I/O**: Catch, log, and decide (file writes, network calls)

---

#### Context-Rich Error Messages
**Learning**: Include contextual information in error messages and logs.

**Pattern**:
```csharp
public class ContextualLogger
{
    public void LogError(Exception ex, string operation,
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0)
    {
        var context = new
        {
            Operation = operation,
            Caller = $"{member}:{line}",
            Timestamp = DateTime.UtcNow,
            Exception = ex.Message,
            StackTrace = ex.StackTrace
        };

        Console.Error.WriteLine($"[ERROR] {JsonSerializer.Serialize(context)}");
    }
}

// Usage
try
{
    ProcessFile(filePath);
}
catch (Exception ex)
{
    logger.LogError(ex, $"Processing file: {filePath}");
    throw;
}
```

**Benefits**:
- Easier debugging in production
- Caller information automatically captured
- Structured logging for log aggregation tools

---

#### Try-Finally for Resource Cleanup
**Learning**: Always clean up resources in `finally` blocks, especially in long-running processes.

**Pattern**:
```csharp
public async Task ProcessMessagesAsync()
{
    JsonDocument? document = null;

    try
    {
        while (true)
        {
            document = await ReadMessageAsync();
            ProcessMessage(document);

            document.Dispose();
            document = null;
        }
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Message processing failed");
    }
    finally
    {
        document?.Dispose();
        CleanupResources();
    }
}
```

**Key Points**:
- Set to `null` after normal disposal to avoid double-disposal
- Use `?.Dispose()` in `finally` for safety
- Clean up even on exceptions

---

### 5. Cross-Platform Development

#### Runtime Identifiers for Platform-Specific Builds
**Learning**: Use .NET Runtime Identifiers (RIDs) to build platform-specific binaries from a single codebase.

**Configuration**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RuntimeIdentifiers>win-x64;osx-arm64;linux-x64</RuntimeIdentifiers>
  </PropertyGroup>
</Project>
```

**Build Commands**:
```bash
# Build for Windows
dotnet publish -r win-x64 -c Release

# Build for macOS (Apple Silicon)
dotnet publish -r osx-arm64 -c Release

# Build for Linux
dotnet publish -r linux-x64 -c Release
```

**Benefits**:
- Single codebase for all platforms
- Platform-optimized binaries
- No conditional compilation needed (in most cases)

---

#### Path Normalization
**Learning**: Always use `Path.DirectorySeparatorChar` for cross-platform path operations.

**Pattern**:
```csharp
public static class PathHelper
{
    public static string NormalizePath(string path)
    {
        return path.Replace('\\', Path.DirectorySeparatorChar)
                   .Replace('/', Path.DirectorySeparatorChar);
    }

    public static string Combine(params string[] parts)
    {
        return Path.Combine(parts);  // Already cross-platform
    }
}

// Usage
var path = PathHelper.NormalizePath(userInput);
var fullPath = PathHelper.Combine(baseDir, "subfolder", "file.txt");
```

**What to Avoid**:
```csharp
❌ var path = baseDir + "\\" + fileName;           // Windows-only
❌ var path = baseDir + "/" + fileName;            // Unix-only
✅ var path = Path.Combine(baseDir, fileName);     // Cross-platform
```

---

### 6. Dependency Management

#### Constructor Injection for Testability
**Learning**: Pass dependencies through constructors to make components testable.

**Pattern**:
```csharp
public class DataProcessor
{
    private readonly IRepository _repository;
    private readonly ILogger _logger;

    // Dependency injection via constructor
    public DataProcessor(IRepository repository, ILogger logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ProcessAsync(string id)
    {
        var data = await _repository.GetAsync(id);
        _logger.LogInfo($"Processing {id}");
        // Process data...
    }
}

// Testing with mocks
[Fact]
public async Task ProcessAsync_Should_CallRepository()
{
    // Arrange
    var mockRepo = new Mock<IRepository>();
    var mockLogger = new Mock<ILogger>();
    var processor = new DataProcessor(mockRepo.Object, mockLogger.Object);

    // Act
    await processor.ProcessAsync("123");

    // Assert
    mockRepo.Verify(r => r.GetAsync("123"), Times.Once);
}
```

**Benefits**:
- Easy to test with mock dependencies
- Clear dependency declaration
- Supports dependency injection containers

---

#### Static Managers for Shared State (Use Sparingly)
**Learning**: When components across different contexts need shared state, static managers can be appropriate—but use sparingly.

**When Static Managers Are Justified**:
- Extension functions that can't receive parameters
- Multi-threaded components needing synchronized state
- Global configuration that rarely changes

**Pattern**:
```csharp
public static class SharedStateManager
{
    private static readonly object _lock = new object();
    private static Dictionary<string, object> _state = new();

    public static void SetState(string key, object value)
    {
        lock (_lock)
        {
            _state[key] = value;
        }
    }

    public static object? GetState(string key)
    {
        lock (_lock)
        {
            return _state.TryGetValue(key, out var value) ? value : null;
        }
    }

    public static void Clear()
    {
        lock (_lock)
        {
            _state.Clear();
        }
    }
}
```

**Testing Considerations**:
```csharp
public class StateTests : IDisposable
{
    public StateTests()
    {
        SharedStateManager.Clear();  // Reset before each test
    }

    public void Dispose()
    {
        SharedStateManager.Clear();  // Clean up after test
    }

    [Fact]
    public void Should_StoreAndRetrieveState()
    {
        SharedStateManager.SetState("key", "value");
        Assert.Equal("value", SharedStateManager.GetState("key"));
    }
}
```

**Alternatives to Consider First**:
- Scoped service lifetimes (ASP.NET Core, DI containers)
- Ambient context pattern
- Explicit parameter passing

---

### 7. AI-Assisted Development ("Vibecoding")

#### Understanding the "Vibecoding" Flow
**Learning**: AI coding assistants (GitHub Copilot, Claude, etc.) enable immersive development flow where developers can maintain extended focus and productivity, especially in unfamiliar codebases.

**Key Concept**: "Vibecoding" represents a state where AI assistance creates an engaging, flow-state coding experience that extends productive coding sessions.

---

#### Prompt Clarity & Specificity
**Learning**: Specific, well-contextualized prompts are essential for effective AI-assisted development.

**Best Practices**:
```markdown
❌ Bad Prompt: "Fix the code"
✅ Good Prompt: "Fix the null reference exception in UserService.GetUserById()
   when userId parameter is null. Add validation and return appropriate error."

❌ Bad Prompt: "Add logging"
✅ Good Prompt: "Add structured logging using ILogger to the ProcessOrder method.
   Log entry with order ID, log any exceptions with full context, and log completion
   with processing time."

❌ Bad Prompt: "Refactor this"
✅ Good Prompt: "Refactor the data access logic in OrderRepository to use the
   Repository pattern. Create IRepository<T> interface and extract common CRUD
   operations. Keep existing business logic unchanged."
```

**Guidelines**:
- Provide clear context about what you're trying to achieve
- Specify constraints (what should NOT change)
- Include relevant business rules or requirements
- Mention specific patterns or approaches to use

**Why It Matters**: Vague prompts lead to:
- Unnecessary code rewrites
- Unrelated module modifications
- Wasted iteration cycles
- Loss of context and focus

---

#### Context Preservation Strategies
**Learning**: Maintaining conversation context is critical for AI-assisted development sessions.

**Critical Warning**: Clearing chat history resets the AI's understanding of your codebase state and can result in losing incremental changes.

**Best Practices**:
1. **Keep Conversations Focused**: Use separate chat threads for unrelated tasks
2. **Document Key Decisions**: When AI makes important architectural choices, document them in comments
3. **Commit Incrementally**: Commit working changes before major refactoring requests
4. **Session Checkpointing**: Periodically summarize what's been done in the conversation

**Pattern**:
```markdown
# Start of new major task - create checkpoint comment
"Before we continue, here's what we've accomplished so far:
1. Implemented UserRepository with IRepository<T> pattern
2. Added unit tests for CRUD operations
3. Configured dependency injection in Program.cs

Now let's move on to implementing OrderRepository following the same pattern."
```

---

#### Strategic "Free Thinking" vs. Focused Tasks
**Learning**: Balance between letting AI explore solutions and maintaining focused execution.

**Approach**:
1. **Business Logic First**: Focus AI on core business requirements initially
2. **Controlled Exploration**: Periodically allow AI to suggest improvements without strict constraints
3. **Learn AI Capabilities**: Observing unrestricted AI approaches reveals tool capabilities and limitations

**Pattern**:
```markdown
# Focused Task
"Implement user authentication using JWT tokens. Follow our existing
SecurityService pattern. Do not modify existing controllers."

# Strategic Free Thinking (After core logic is done)
"Review the authentication implementation. What improvements or additional
features would you suggest? Feel free to explore different approaches."
```

**Benefits**:
- Discover new patterns or libraries
- Learn alternative approaches
- Identify edge cases you might have missed
- Understand AI's problem-solving strategies

**When to Use**:
- After completing core functionality
- When stuck on a problem
- During code review phase
- When learning new domains

---

#### Managing AI "Overreach"
**Learning**: AI assistants often exceed requests by generating additional code (READMEs, tests, documentation, refactoring commented code).

**Common Overreach Behaviors**:
- Auto-generating documentation files
- Creating unit tests for all methods
- Refactoring "old" or commented code
- Adding features not requested
- Reorganizing project structure

**Management Strategies**:

**1. Set Clear Boundaries**:
```markdown
"Implement the user login endpoint. ONLY modify UserController.cs.
Do not generate tests, documentation, or modify other files."
```

**2. Redirect Enthusiasm**:
```markdown
"Good suggestions on the tests and documentation. Let's complete the core
implementation first, then we'll address those in separate, focused tasks."
```

**3. Use Overreach Productively**:
```markdown
After implementing core feature:
"Now let's use that energy for refinement - review the code and suggest
improvements, then generate comprehensive unit tests."
```

**Pattern for Phased Development**:
```markdown
Phase 1: "Implement [feature]. Focus only on core business logic."
Phase 2: "Add error handling and validation to [feature]."
Phase 3: "Generate comprehensive unit tests for [feature]."
Phase 4: "Add XML documentation to public APIs in [feature]."
```

---

#### Re-prompting When Drift Occurs
**Learning**: When AI output diverges from intent, re-prompting with greater specificity is more effective than accepting divergent outputs.

**Pattern for Course Correction**:
```markdown
# When AI adds unwanted features:
"Let's reset. The authentication should ONLY validate JWT tokens.
Remove the user registration, password reset, and email verification
features. We'll add those later in separate tasks."

# When AI changes too much:
"You've modified 5 files, but I only asked for changes to UserService.cs.
Please provide a version that ONLY changes UserService.cs and keeps all
other files unchanged."

# When approach is wrong:
"This implementation uses Entity Framework, but our project uses Dapper.
Please reimplement using Dapper's Query<T> and Execute methods."
```

**Indicators That Drift Has Occurred**:
- More files modified than expected
- Different architectural pattern than project uses
- Features you didn't request
- Breaking changes to existing APIs
- Different technology stack components

**Recovery Steps**:
1. **Stop and assess**: Don't accept the divergent output
2. **Identify root cause**: Why did drift occur? Unclear prompt? Missing context?
3. **Re-prompt with specifics**: Address the root cause in new prompt
4. **Provide examples**: Show what you want using existing code patterns

---

#### Environment Isolation for Testing
**Learning**: Use Docker or containerization to isolate execution environments, especially when running servers and tests simultaneously.

**Problem**: Running API servers and test commands in the same terminal session can cause:
- Process blocking (server blocks test execution)
- Port conflicts
- Inconsistent behavior across platforms (macOS vs. Windows)
- Terminal freezes

**Solution**: Docker Isolation
```dockerfile
# Dockerfile for development environment
FROM mcr.microsoft.com/dotnet/sdk:8.0

WORKDIR /app
COPY . .

RUN dotnet restore
RUN dotnet build

# Separate container for API server
EXPOSE 5000
CMD ["dotnet", "run", "--project", "API"]
```

```bash
# docker-compose.yml
version: '3.8'
services:
  api:
    build: .
    ports:
      - "5000:5000"

  tests:
    build: .
    command: dotnet test
    depends_on:
      - api
```

**Benefits**:
- Predictable execution across platforms
- No process conflicts
- Clean separation of concerns
- Reproducible test environments
- Easy CI/CD integration

**Alternative Patterns**:
```bash
# Option 1: Background processes with proper cleanup
dotnet run --project API &
API_PID=$!
sleep 5  # Wait for startup
dotnet test
kill $API_PID

# Option 2: Separate terminal sessions (manual)
# Terminal 1: dotnet run --project API
# Terminal 2: dotnet test

# Option 3: Use Docker (recommended)
docker-compose up -d api
dotnet test
docker-compose down
```

---

#### Cross-Platform Considerations in AI Workflows
**Learning**: AI-generated code may work on one platform but fail on another. Explicitly specify cross-platform requirements.

**Best Practices**:

**1. Specify Platform Requirements in Prompts**:
```markdown
"Implement file operations that work on Windows, macOS, and Linux.
Use Path.Combine() instead of string concatenation with slashes."
```

**2. Request Cross-Platform Testing**:
```markdown
"Generate test cases that verify the path handling works correctly
on both Windows (backslashes) and Unix (forward slashes) systems."
```

**3. Ask for Platform-Specific Validations**:
```markdown
"Review this code for cross-platform compatibility issues.
Check for hardcoded paths, platform-specific APIs, and
path separator assumptions."
```

**Common Cross-Platform Issues**:
- Path separators (`\` vs `/`)
- Line endings (CRLF vs LF)
- File permissions
- Case-sensitive file systems
- Environment variable syntax
- Shell command differences

---

#### Practical AI-Assisted Workflow
**Learning**: Combine multiple AI tools strategically for different tasks.

**Recommended Tool Usage**:

**1. Code Understanding** (Copilot, Claude):
```markdown
"Explain how the authentication flow works in this codebase.
Trace the request from UserController.Login() through all layers."
```

**2. Code Execution & Testing** (Copilot Agent, Claude with execution):
```markdown
"Run the integration tests for the OrderService and show me
the results. If any fail, analyze the failures."
```

**3. Build & Deployment** (Docker, Scripts):
```markdown
"Create a Docker Compose configuration that runs the API, database,
and Redis cache for local development."
```

**Workflow Pattern**:
```
1. Understanding Phase (AI reads code)
   └─> "Analyze the existing UserService implementation"

2. Planning Phase (AI suggests approach)
   └─> "How should we add OAuth2 support to this service?"

3. Implementation Phase (AI writes code)
   └─> "Implement OAuth2 authentication following the suggested approach"

4. Testing Phase (AI generates & runs tests)
   └─> "Generate unit tests and run them"

5. Validation Phase (Manual review + AI suggestions)
   └─> "Review this implementation for security issues and edge cases"
```

---

#### AI-Assisted Debugging Best Practices
**Learning**: Effective debugging with AI requires providing complete context.

**Debugging Prompt Template**:
```markdown
**Problem**: [Clear description of the issue]

**Expected Behavior**: [What should happen]

**Actual Behavior**: [What is happening]

**Context**:
- Error message: [Full stack trace]
- Relevant code: [Code snippet where issue occurs]
- Environment: [.NET version, OS, etc.]
- Steps to reproduce: [Numbered steps]

**What I've Tried**: [Previous attempts]

Please help identify the root cause and suggest a fix.
```

**Example**:
```markdown
**Problem**: NullReferenceException in UserService.GetUserById()

**Expected Behavior**: Return user object or null if not found

**Actual Behavior**:
System.NullReferenceException: Object reference not set to an instance of an object
   at UserService.GetUserById(String id) in UserService.cs:line 42

**Context**:
- Error occurs when userId parameter is valid but user doesn't exist in database
- Using Entity Framework Core 8.0
- .NET 8.0 on macOS
- Steps: Call API with GET /api/users/nonexistent-id

**What I've Tried**:
- Added null check for userId parameter (didn't help)
- Verified database connection is working

Please help identify the root cause and suggest a fix.
```

---

#### Measuring AI-Assisted Development Success
**Learning**: Track metrics to understand AI assistance effectiveness.

**Key Metrics**:
1. **Time to Implementation**: Time from requirement to working code
2. **Iteration Count**: Number of prompt refinements needed
3. **Code Quality**: Test coverage, bug density
4. **Learning Velocity**: Time to understand unfamiliar codebases

**Success Indicators**:
- Fewer re-prompts needed over time (better prompt writing)
- Longer productive coding sessions (flow state)
- Faster onboarding to new projects
- Higher test coverage with less effort

**Anti-Patterns to Watch For**:
- Accepting AI suggestions without understanding
- Over-reliance leading to skill degradation
- Ignoring code reviews because "AI wrote it"
- Blindly trusting AI-generated security code

---

---

## Reusable Patterns and Tools

### 1. Code Analysis & Transformation

#### Roslyn Syntax Rewriter for C# AST Manipulation
**Tool**: `Microsoft.CodeAnalysis.CSharp` NuGet package

**Use Case**: Automatically transform C# code (add logging, instrumentation, refactoring).

**Pattern**:
```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class LoggingInstrumenter : CSharpSyntaxRewriter
{
    private int _instrumentedCount = 0;

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        // Add logging at method entry
        var entryLog = SyntaxFactory.ParseStatement(
            $"Logger.LogEntry(\"{node.Identifier}\");"
        );

        var newBody = node.Body?.WithStatements(
            node.Body.Statements.Insert(0, entryLog)
        );

        _instrumentedCount++;
        return node.WithBody(newBody);
    }

    public int InstrumentedCount => _instrumentedCount;
}

// Usage
public string InstrumentCode(string sourceCode)
{
    var tree = CSharpSyntaxTree.ParseText(sourceCode);
    var root = tree.GetRoot();

    var instrumenter = new LoggingInstrumenter();
    var newRoot = instrumenter.Visit(root);

    Console.WriteLine($"Instrumented {instrumenter.InstrumentedCount} methods");
    return newRoot.ToFullString();
}
```

**Benefits**:
- Type-safe code transformation
- Preserves formatting and comments
- Supports complex refactoring scenarios

**Common Use Cases**:
- Add logging/instrumentation
- Rename symbols across files
- Migrate deprecated APIs
- Generate boilerplate code

---

#### XDocument for XML Transformation
**Tool**: `System.Xml.Linq` (built-in)

**Use Case**: Transform XML/XSLT/HTML files programmatically.

**Pattern**:
```csharp
using System.Xml.Linq;

public class XmlInstrumenter
{
    private readonly XNamespace _targetNamespace;

    public XmlInstrumenter(string namespaceUri)
    {
        _targetNamespace = XNamespace.Get(namespaceUri);
    }

    public XDocument InjectDebugCalls(XDocument document)
    {
        var elements = document.Descendants(_targetNamespace + "template");

        foreach (var element in elements)
        {
            var lineAttr = element.Attribute("line");
            if (lineAttr != null)
            {
                // Inject debug call element
                var debugCall = new XElement(_targetNamespace + "value-of",
                    new XAttribute("select", $"debug:break({lineAttr.Value})")
                );

                element.AddFirst(debugCall);
            }
        }

        return document;
    }
}

// Usage
var doc = XDocument.Load("template.xml");
var instrumenter = new XmlInstrumenter("http://www.w3.org/1999/XSL/Transform");
var instrumented = instrumenter.InjectDebugCalls(doc);
instrumented.Save("template.instrumented.xml");
```

**Benefits**:
- Fluent API for XML manipulation
- Namespace-aware
- LINQ integration for querying

---

### 2. Runtime Code Evaluation

#### Roslyn Scripting API
**Tool**: `Microsoft.CodeAnalysis.CSharp.Scripting` NuGet package

**Use Case**: Evaluate C# expressions at runtime (watch expressions, REPL, dynamic logic).

**Pattern**:
```csharp
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

public class ScriptEvaluator
{
    private readonly ScriptOptions _options;

    public ScriptEvaluator()
    {
        _options = ScriptOptions.Default
            .WithImports("System", "System.Linq", "System.Collections.Generic")
            .WithReferences(
                typeof(Console).Assembly,
                typeof(Enumerable).Assembly
            );
    }

    public async Task<object?> EvaluateAsync(string code, object? globals = null)
    {
        try
        {
            return await CSharpScript.EvaluateAsync(code, _options, globals);
        }
        catch (CompilationErrorException ex)
        {
            Console.Error.WriteLine($"Compilation error: {string.Join("\n", ex.Diagnostics)}");
            throw;
        }
    }
}

// Usage
var evaluator = new ScriptEvaluator();

// Simple expression
var result = await evaluator.EvaluateAsync("2 + 2");
Console.WriteLine(result);  // 4

// With context
var globals = new { x = 10, y = 20 };
var sum = await evaluator.EvaluateAsync("x + y", globals);
Console.WriteLine(sum);  // 30

// LINQ query
var numbers = new { Numbers = new[] { 1, 2, 3, 4, 5 } };
var filtered = await evaluator.EvaluateAsync("Numbers.Where(n => n > 2)", numbers);
```

**Benefits**:
- Full C# language support
- Access to .NET libraries
- Configurable namespaces and references

**Use Cases**:
- Watch expressions in debuggers
- Dynamic business rules
- Interactive shells (REPL)
- User-defined formulas

---

### 3. Structured Logging

#### Caller Information Attributes
**Tool**: Built-in C# attributes (`System.Runtime.CompilerServices`)

**Use Case**: Automatic context capture in logging without manual parameters.

**Pattern**:
```csharp
using System.Runtime.CompilerServices;

public static class ContextualLogger
{
    public static void LogEntry(
        object? parameters = null,
        [CallerMemberName] string member = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var fileName = Path.GetFileName(filePath);
        Console.WriteLine($"[ENTRY] {fileName}:{lineNumber} {member}({parameters})");
    }

    public static T LogReturn<T>(
        T value,
        [CallerMemberName] string member = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Console.WriteLine($"[RETURN] {member}:{lineNumber} => {value}");
        return value;
    }
}

// Usage - no manual context needed!
public int CalculateSum(int a, int b)
{
    ContextualLogger.LogEntry(new { a, b });

    var result = a + b;

    return ContextualLogger.LogReturn(result);
}

// Output:
// [ENTRY] Calculator.cs:42 CalculateSum({ a = 5, b = 10 })
// [RETURN] CalculateSum:46 => 15
```

**Available Caller Attributes**:
- `CallerMemberName` - Method/property name
- `CallerFilePath` - Source file path
- `CallerLineNumber` - Line number
- `CallerArgumentExpression` (.NET 6+) - Argument expression text

**Benefits**:
- Zero-cost context capture
- Refactoring-safe (updates automatically)
- No runtime reflection needed

---

### 4. Asynchronous Patterns

#### TaskCompletionSource for Event-to-Task Conversion
**Tool**: Built-in `TaskCompletionSource<T>`

**Use Case**: Convert event-based APIs to async/await patterns.

**Pattern**:
```csharp
public class EventBasedComponent
{
    public event Action<string>? DataReceived;
    public event Action<Exception>? ErrorOccurred;

    public void StartListening() { /* starts async operation */ }
}

public class AsyncWrapper
{
    private readonly EventBasedComponent _component;

    public AsyncWrapper(EventBasedComponent component)
    {
        _component = component;
    }

    public Task<string> WaitForDataAsync(TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<string>();

        // Subscribe to events
        Action<string> onData = null!;
        Action<Exception> onError = null!;

        onData = (data) =>
        {
            _component.DataReceived -= onData;
            _component.ErrorOccurred -= onError;
            tcs.TrySetResult(data);
        };

        onError = (ex) =>
        {
            _component.DataReceived -= onData;
            _component.ErrorOccurred -= onError;
            tcs.TrySetException(ex);
        };

        _component.DataReceived += onData;
        _component.ErrorOccurred += onError;

        // Start operation
        _component.StartListening();

        // Return task with timeout
        return tcs.Task.WaitAsync(timeout);
    }
}

// Usage
var component = new EventBasedComponent();
var wrapper = new AsyncWrapper(component);

try
{
    var data = await wrapper.WaitForDataAsync(TimeSpan.FromSeconds(5));
    Console.WriteLine($"Received: {data}");
}
catch (TimeoutException)
{
    Console.WriteLine("Timeout waiting for data");
}
```

**Benefits**:
- Integrates event-based code into async/await workflows
- Built-in timeout support with `WaitAsync`
- Clean error propagation

---

### 5. Build & Packaging Automation

#### Cross-Platform Build Scripts
**Tool**: Bash scripts for automation

**Pattern**: Platform-specific packaging scripts

**package-darwin.sh**:
```bash
#!/bin/bash
set -e  # Exit on error

echo "Building for macOS (ARM64)..."

# Clean previous builds
rm -rf ./XsltDebugger.DebugAdapter/bin/Release

# Build .NET adapter for macOS
dotnet publish ./XsltDebugger.DebugAdapter \
  -c Release \
  -r osx-arm64 \
  --self-contained false

# Compile TypeScript extension
npm run compile

# Package as VSIX
npx vsce package \
  --target darwin-arm64 \
  --out ./dist/extension-darwin-arm64.vsix

echo "✓ macOS build complete: ./dist/extension-darwin-arm64.vsix"
```

**package-windows.sh**:
```bash
#!/bin/bash
set -e

echo "Building for Windows (x64)..."

# Clean previous builds
rm -rf ./XsltDebugger.DebugAdapter/bin/Release

# Build .NET adapter for Windows
dotnet publish ./XsltDebugger.DebugAdapter \
  -c Release \
  -r win-x64 \
  --self-contained false

# Compile TypeScript extension
npm run compile

# Package as VSIX
npx vsce package \
  --target win32-x64 \
  --out ./dist/extension-win32-x64.vsix

echo "✓ Windows build complete: ./dist/extension-win32-x64.vsix"
```

**package-all.sh**:
```bash
#!/bin/bash
set -e

echo "Building all platforms..."

./package-darwin.sh
./package-windows.sh

echo "✓ All builds complete!"
ls -lh ./dist/
```

**Benefits**:
- Consistent builds across platforms
- Easy CI/CD integration
- Single command for complete build

---

### 6. Testing Tools

#### xUnit with FluentAssertions
**Tools**:
- `xunit` - Testing framework
- `FluentAssertions` - Readable assertions
- `Moq` - Mocking framework

**Pattern**:
```csharp
using Xunit;
using FluentAssertions;
using Moq;

public class DataProcessorTests
{
    [Fact]
    public async Task ProcessAsync_Should_RetrieveAndTransformData()
    {
        // Arrange
        var mockRepo = new Mock<IRepository>();
        mockRepo.Setup(r => r.GetAsync("123"))
            .ReturnsAsync(new Data { Id = "123", Value = 100 });

        var processor = new DataProcessor(mockRepo.Object);

        // Act
        var result = await processor.ProcessAsync("123");

        // Assert
        result.Should().NotBeNull();
        result.TransformedValue.Should().Be(200);

        mockRepo.Verify(r => r.GetAsync("123"), Times.Once);
    }

    [Theory]
    [InlineData("123", 100, 200)]
    [InlineData("456", 50, 100)]
    [InlineData("789", 0, 0)]
    public async Task ProcessAsync_Should_DoubleValue(
        string id, int input, int expected)
    {
        // Arrange
        var mockRepo = new Mock<IRepository>();
        mockRepo.Setup(r => r.GetAsync(id))
            .ReturnsAsync(new Data { Id = id, Value = input });

        var processor = new DataProcessor(mockRepo.Object);

        // Act
        var result = await processor.ProcessAsync(id);

        // Assert
        result.TransformedValue.Should().Be(expected);
    }
}
```

**FluentAssertions Examples**:
```csharp
// Collections
result.Should().HaveCount(3);
result.Should().Contain(x => x.Id == "123");
result.Should().BeInAscendingOrder(x => x.Value);

// Exceptions
var act = () => processor.Process(null);
act.Should().Throw<ArgumentNullException>()
    .WithMessage("*parameter*");

// Objects
result.Should().BeEquivalentTo(expected, options =>
    options.Excluding(x => x.Timestamp));

// Strings
message.Should().StartWith("Error:")
    .And.Contain("file not found")
    .And.EndWith(".");
```

---

---

## Recommendations for Standardization

### 1. Architecture Standards

#### Adopt Layered Architecture Pattern
**Recommendation**: Standardize on a clear 4-layer architecture for all backend projects.

**Standard Layers**:
```
┌─────────────────────────────────────┐
│   Presentation / Protocol Layer     │  ← External APIs, UI, Protocols
├─────────────────────────────────────┤
│   Application / Session Layer       │  ← Orchestration, State Management
├─────────────────────────────────────┤
│   Domain / Engine Layer             │  ← Core Business Logic
├─────────────────────────────────────┤
│   Infrastructure / Utilities Layer  │  ← Data Access, Logging, External Services
└─────────────────────────────────────┘
```

**Folder Structure Template**:
```
/ProjectName/
├── Presentation/      or  Protocol/
├── Application/       or  Session/
├── Domain/            or  Engine/
└── Infrastructure/    or  Utilities/
```

**Benefits**:
- Consistent project navigation across teams
- Clear dependency rules (upper layers depend on lower)
- Easy onboarding for new developers

---

#### Dependency Rule Enforcement
**Recommendation**: Enforce dependency rules with ArchUnit or similar tools.

**Dependency Rules**:
```csharp
// Example rules to enforce
✅ Presentation → Application → Domain → Infrastructure
❌ Domain → Presentation (forbidden)
❌ Infrastructure → Presentation (forbidden)
```

**Tool**: ArchUnitNET
```csharp
[Fact]
public void Domain_Should_Not_DependOn_Presentation()
{
    var architecture = new ArchLoader()
        .LoadAssemblies(typeof(DomainClass).Assembly)
        .Build();

    var rule = Types()
        .That().ResideInNamespace("ProjectName.Domain")
        .Should().NotDependOnAny("ProjectName.Presentation");

    rule.Check(architecture);
}
```

---

### 2. Code Quality Standards

#### Enable Nullable Reference Types
**Recommendation**: Enable nullable reference types in all new projects.

**Configuration**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <WarningsAsErrors>nullable</WarningsAsErrors>
  </PropertyGroup>
</Project>
```

**Benefits**:
- Catch null-reference bugs at compile time
- Self-documenting code (`string?` vs `string`)
- Reduced production exceptions

**Migration Strategy**:
1. Enable in new projects immediately
2. For legacy projects, enable per-file with `#nullable enable`
3. Gradually expand coverage

---

#### Code Analysis & Style Enforcement
**Recommendation**: Standardize on EditorConfig + Roslyn Analyzers.

**.editorconfig**:
```ini
root = true

[*.cs]
indent_style = space
indent_size = 4
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

# Naming conventions
dotnet_naming_rule.async_methods_end_in_async.severity = warning
dotnet_naming_rule.async_methods_end_in_async.symbols = async_methods
dotnet_naming_rule.async_methods_end_in_async.style = end_in_async

# Code quality rules
dotnet_diagnostic.CA1062.severity = warning  # Validate arguments
dotnet_diagnostic.CA2007.severity = none     # ConfigureAwait (not needed in apps)
```

**Analyzers to Include**:
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="8.0.0" />
  <PackageReference Include="StyleCop.Analyzers" Version="1.2.0" />
  <PackageReference Include="SonarAnalyzer.CSharp" Version="9.0.0" />
</ItemGroup>
```

---

#### Test Coverage Targets
**Recommendation**: Set minimum code coverage thresholds.

**Targets**:
- **Unit Tests**: 80% coverage of business logic
- **Integration Tests**: Critical paths must have tests
- **New Code**: 100% coverage for all new features

**Tool Integration**:
```xml
<ItemGroup>
  <PackageReference Include="coverlet.collector" Version="6.0.0" />
</ItemGroup>
```

**CI/CD Check**:
```bash
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
# Fail build if coverage < 80%
```

---

### 3. Testing Standards

#### Test Naming Convention
**Recommendation**: Standardize on method name pattern: `MethodName_Should_ExpectedBehavior_When_Condition`

**Examples**:
```csharp
[Fact]
public void ProcessAsync_Should_ReturnResult_When_DataIsValid()

[Fact]
public void ProcessAsync_Should_ThrowException_When_DataIsNull()

[Fact]
public void GetUser_Should_ReturnNull_When_UserNotFound()

[Theory]
[InlineData(0)]
[InlineData(-1)]
public void Withdraw_Should_ThrowException_When_AmountIsInvalid(int amount)
```

**Benefits**:
- Readable test names in test runners
- Clear intent without reading test body
- Easy to identify missing test cases

---

#### Arrange-Act-Assert Pattern
**Recommendation**: Enforce AAA pattern with comments in all tests.

**Template**:
```csharp
[Fact]
public void MethodName_Should_Behavior()
{
    // Arrange
    var dependency = new Mock<IDependency>();
    var sut = new SystemUnderTest(dependency.Object);

    // Act
    var result = sut.Method(input);

    // Assert
    result.Should().Be(expected);
}
```

**Enforce in Code Reviews**: All tests must follow AAA structure.

---

#### Integration Test Organization
**Recommendation**: Separate integration tests with naming and attributes.

**Pattern**:
```csharp
[Trait("Category", "Integration")]
public class DatabaseIntegrationTests : IAsyncLifetime
{
    private DatabaseFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = new DatabaseFixture();
        await _fixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task Should_InsertAndRetrieveData()
    {
        // Test implementation
    }
}
```

**Run Separately in CI/CD**:
```bash
# Fast unit tests
dotnet test --filter "Category!=Integration"

# Slower integration tests
dotnet test --filter "Category=Integration"
```

---

### 4. Build & Deployment Standards

#### Multi-Platform Support
**Recommendation**: All CLI tools and services should target multiple platforms.

**Standard Runtime Identifiers**:
```xml
<RuntimeIdentifiers>win-x64;osx-arm64;linux-x64</RuntimeIdentifiers>
```

**CI/CD Matrix**:
```yaml
strategy:
  matrix:
    os: [ubuntu-latest, windows-latest, macos-latest]
    dotnet: ['8.0.x']
```

---

#### Versioning Strategy
**Recommendation**: Adopt Semantic Versioning 2.0.0 (SemVer).

**Format**: `MAJOR.MINOR.PATCH`
- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes (backward compatible)

**Implementation**:
```xml
<Project>
  <PropertyGroup>
    <Version>1.2.3</Version>
    <AssemblyVersion>1.0.0</AssemblyVersion>      <!-- Only MAJOR changes -->
    <FileVersion>1.2.3</FileVersion>              <!-- Full version -->
  </PropertyGroup>
</Project>
```

**Git Tags**:
```bash
git tag -a v1.2.3 -m "Release version 1.2.3"
git push origin v1.2.3
```

---

#### Automated Build Scripts
**Recommendation**: Provide platform-specific build scripts in every repository.

**Required Scripts**:
```
/scripts/
├── build-all.sh       # Build all platforms
├── build-windows.sh   # Windows-specific
├── build-macos.sh     # macOS-specific
├── build-linux.sh     # Linux-specific
├── test.sh            # Run all tests
├── package.sh         # Create distribution packages
└── clean.sh           # Clean build artifacts
```

**Standard Script Template**:
```bash
#!/bin/bash
set -e  # Exit on error
set -u  # Exit on undefined variable

echo "Building Project..."

# Restore dependencies
dotnet restore

# Build
dotnet build -c Release

# Test
dotnet test -c Release --no-build

echo "✓ Build complete"
```

**Make Executable**:
```bash
chmod +x scripts/*.sh
```

---

### 5. Documentation Standards

#### Code-Level Documentation
**Recommendation**: Require XML documentation for all public APIs.

**Enforcement**:
```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);CS1591</NoWarn>  <!-- Missing XML comment warning -->
</PropertyGroup>
```

**Template**:
```csharp
/// <summary>
/// Processes the input data and returns transformed result.
/// </summary>
/// <param name="input">The input data to process.</param>
/// <param name="options">Optional processing options.</param>
/// <returns>The transformed result.</returns>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is null.</exception>
/// <example>
/// <code>
/// var result = processor.Process(data, new Options { Verbose = true });
/// </code>
/// </example>
public Result Process(Data input, Options? options = null)
```

---

#### README Template
**Recommendation**: Standardize README structure across all repositories.

**Template Sections**:
```markdown
# Project Name

## Overview
Brief description (2-3 sentences)

## Features
- Key feature 1
- Key feature 2
- Key feature 3

## Requirements
- .NET 8.0 or higher
- Platform requirements

## Quick Start
```bash
# Clone repository
git clone <repo-url>

# Build
./scripts/build-all.sh

# Run tests
./scripts/test.sh
```

## Usage
Basic usage examples

## Configuration
Configuration options and environment variables

## Architecture
High-level architecture diagram and explanation

## Contributing
Link to CONTRIBUTING.md

## License
License information
```

---

#### Architecture Decision Records (ADRs)
**Recommendation**: Document significant architectural decisions in `/docs/adr/`.

**Template** (`docs/adr/0001-use-event-bus-pattern.md`):
```markdown
# 1. Use Event Bus Pattern for Cross-Component Communication

Date: 2024-01-15

## Status
Accepted

## Context
Components across different execution contexts need to communicate without tight coupling.
Direct dependencies would create circular references and complicate testing.

## Decision
We will use a static event bus (XsltEngineManager) with typed events for cross-component
communication.

## Consequences
**Positive:**
- Loose coupling between components
- Easy to add new subscribers
- Simplified testing with event mocking

**Negative:**
- Global state complicates some testing scenarios
- Harder to trace event flow in debugger

## Alternatives Considered
1. Dependency injection with scoped services (rejected: doesn't work across execution contexts)
2. Message queue (rejected: overkill for in-process communication)
```

---

### 6. Error Handling Standards

#### Exception Hierarchy
**Recommendation**: Define application-specific exception types.

**Pattern**:
```csharp
namespace YourApp.Exceptions;

/// <summary>
/// Base exception for all application exceptions.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
    protected AppException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Thrown when validation fails.
/// </summary>
public class ValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(Dictionary<string, string[]> errors)
        : base("Validation failed")
    {
        Errors = errors;
    }
}

/// <summary>
/// Thrown when a requested resource is not found.
/// </summary>
public class NotFoundException : AppException
{
    public string ResourceType { get; }
    public string ResourceId { get; }

    public NotFoundException(string resourceType, string resourceId)
        : base($"{resourceType} with ID '{resourceId}' not found")
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
    }
}
```

**Usage**:
```csharp
public User GetUser(string id)
{
    var user = _repository.FindById(id);
    if (user == null)
        throw new NotFoundException("User", id);

    return user;
}
```

---

#### Logging Standards
**Recommendation**: Use structured logging with log levels.

**Levels**:
- **Trace**: Detailed diagnostic (variable values, execution paths)
- **Debug**: General diagnostic information
- **Info**: Informational messages (startup, shutdown, milestones)
- **Warning**: Recoverable issues
- **Error**: Exceptions and failures
- **Critical**: Fatal errors requiring immediate attention

**Pattern**:
```csharp
public interface ILogger
{
    void Trace(string message, params object[] args);
    void Debug(string message, params object[] args);
    void Info(string message, params object[] args);
    void Warning(string message, Exception? ex = null, params object[] args);
    void Error(string message, Exception ex, params object[] args);
    void Critical(string message, Exception ex, params object[] args);
}

// Usage
_logger.Info("Processing started for user {UserId}", userId);
_logger.Warning("Retry attempt {Attempt} of {MaxAttempts}", attempt, maxAttempts);
_logger.Error("Failed to process request", ex, new { UserId = userId, RequestId = requestId });
```

---

### 7. Version Control Standards

#### Branch Strategy
**Recommendation**: Adopt GitHub Flow for simplicity.

**Branches**:
- `main` - Always deployable, protected
- `feature/*` - Feature branches (e.g., `feature/add-logging`)
- `fix/*` - Bug fix branches (e.g., `fix/null-reference`)

**Workflow**:
1. Create feature branch from `main`
2. Make changes, commit frequently
3. Open pull request to `main`
4. Code review + CI checks
5. Merge to `main`
6. Deploy from `main`

---

#### Commit Message Convention
**Recommendation**: Use Conventional Commits specification.

**Format**: `<type>(<scope>): <description>`

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `refactor`: Code refactoring
- `test`: Test additions/changes
- `docs`: Documentation changes
- `chore`: Build/tooling changes

**Examples**:
```
feat(auth): add OAuth2 login support
fix(api): handle null reference in user endpoint
refactor(engine): extract interface for engine abstraction
test(integration): add tests for step-into functionality
docs(readme): update build instructions
chore(deps): upgrade to .NET 8.0
```

---

#### Pull Request Template
**Recommendation**: Create `.github/pull_request_template.md`

**Template**:
```markdown
## Summary
<!-- Brief description of changes -->

## Changes
- Change 1
- Change 2
- Change 3

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing completed

## Checklist
- [ ] Code follows project conventions
- [ ] Documentation updated
- [ ] No breaking changes (or marked as BREAKING)
- [ ] CI checks passing
```

---

### 8. Security Standards

#### Dependency Scanning
**Recommendation**: Enable automated dependency vulnerability scanning.

**Tools**:
- **GitHub**: Dependabot (built-in)
- **NuGet**: `dotnet list package --vulnerable`

**CI/CD Integration**:
```bash
# Check for vulnerable packages
dotnet list package --vulnerable --include-transitive

# Fail build if critical vulnerabilities found
if [ $? -ne 0 ]; then
  echo "❌ Vulnerable dependencies detected"
  exit 1
fi
```

---

#### Secrets Management
**Recommendation**: Never commit secrets; use environment variables or secret managers.

**Pattern**:
```csharp
public class Configuration
{
    public string ApiKey { get; init; } =
        Environment.GetEnvironmentVariable("API_KEY")
        ?? throw new InvalidOperationException("API_KEY not set");
}
```

**Git Protection** (`.gitignore`):
```
# Secrets
.env
.env.local
appsettings.*.json
!appsettings.json
secrets.json
*.key
*.pem
credentials/
```

---

## Summary & Next Steps

### Key Takeaways

1. **Patterns**: Strategy, Factory, Event Bus, Adapter
2. **Tools**: Roslyn, xUnit, FluentAssertions, Moq
3. **Standards**: Layered architecture, nullable types, SemVer, conventional commits

### Immediate Actions

| Action | Owner | Deadline |
|--------|-------|----------|
| Review and approve architecture standards | Team Leads | Week 1 |
| Create project templates with standards | DevOps | Week 2 |
| Update existing projects with .editorconfig | Developers | Week 3 |
| Implement CI/CD checks for standards | DevOps | Week 4 |
| Conduct training session on patterns | Tech Leads | Week 4 |

### Long-Term Roadmap

- **Q1**: Establish standards, update tooling
- **Q2**: Migrate existing projects incrementally
- **Q3**: Measure adoption, refine standards
- **Q4**: Full compliance across all projects

---

## Questions & Discussion

**Contact**: Daniel Jonathan
**Documentation**: [Link to confluence/wiki]
**Feedback**: [Link to feedback form]
