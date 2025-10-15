using System.Collections.Generic;
using System.Threading.Tasks;

namespace XsltDebugger.DebugAdapter;

public interface IXsltEngine
{
    Task StartAsync(string stylesheet, string xml, bool stopOnEntry);
    Task ContinueAsync();
    Task StepOverAsync();
    Task StepInAsync();
    Task StepOutAsync();
    void SetBreakpoints(IEnumerable<(string file, int line)> breakpoints);
}
