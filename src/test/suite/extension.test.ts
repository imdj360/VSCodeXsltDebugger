import * as assert from 'assert';
import * as fs from 'fs';
import * as vscode from 'vscode';
import * as path from 'path';

suite('XSLT Debugger End-to-End', function () {
  this.timeout(60000);

  test('should launch and run XSLT debug session with inline C#', async () => {
    const repoRoot = path.resolve(__dirname, '..', '..', '..');
    const stylesheet = path.join(repoRoot, 'TestData', 'Integration', 'xslt', 'compiled', 'sample-inline-cs.xslt');
    const outFile = path.join(repoRoot, 'TestData', 'Integration', 'xslt', 'compiled', 'out', 'sample-inline-cs.out.xml');

    // Remove stale output file before run
    if (fs.existsSync(outFile)) { fs.unlinkSync(outFile); }

    const debugConfig: vscode.DebugConfiguration = {
      type: 'xslt',
      request: 'launch',
      name: 'Debug XSLT (.NET Hybrid)',
      stylesheet,
      xml: path.join(repoRoot, 'TestData', 'Integration', 'xml', 'sample.xml'),
      engine: 'compiled'
    };

    let sessionStarted = false;
    let sessionTerminated = false;

    const startListener = vscode.debug.onDidStartDebugSession(session => {
      if (session.name === debugConfig.name) { sessionStarted = true; }
    });
    const termListener = vscode.debug.onDidTerminateDebugSession(session => {
      if (session.name === debugConfig.name) { sessionTerminated = true; }
    });

    await vscode.debug.startDebugging(vscode.workspace.workspaceFolders?.[0], debugConfig);

    // Wait for session to terminate
    for (let i = 0; i < 60 && !sessionTerminated; i++) {
      await new Promise(res => setTimeout(res, 1000));
    }

    startListener.dispose();
    termListener.dispose();

    assert.ok(sessionStarted, 'Debug session should start');
    assert.ok(sessionTerminated, 'Debug session should terminate');
    assert.ok(fs.existsSync(outFile), `Output file should be written at ${outFile}`);
    const output = fs.readFileSync(outFile, 'utf8');
    assert.match(output, /Hello, World!/, 'Transform output should contain Hello, World!');
  });
});
