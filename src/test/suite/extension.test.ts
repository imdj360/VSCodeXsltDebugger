import * as assert from 'assert';
import * as vscode from 'vscode';
import * as path from 'path';

suite('XSLT Debugger End-to-End', function () {
  this.timeout(60000);

  test('should launch and run XSLT debug session with inline C#', async () => {
    const workspaceFolder = vscode.workspace.workspaceFolders?.[0].uri.fsPath || '';
    const debugConfig: vscode.DebugConfiguration = {
      type: 'xslt',
      request: 'launch',
      name: 'Debug XSLT (.NET Hybrid)',
      stylesheet: path.join(workspaceFolder, 'TestData', 'Integration', 'xslt', 'compiled', 'sample-inline-cs.xslt'),
      xml: path.join(workspaceFolder, 'TestData', 'Integration', 'xml', 'sample.xml'),
      engine: 'compiled'
    };

    let sessionStarted = false;
    let sessionTerminated = false;
    let outputReceived = '';

    const startListener = vscode.debug.onDidStartDebugSession(session => {
      if (session.name === debugConfig.name) {
        sessionStarted = true;
      }
    });
    const termListener = vscode.debug.onDidTerminateDebugSession(session => {
      if (session.name === debugConfig.name) {
        sessionTerminated = true;
      }
    });
    const outputListener = vscode.debug.onDidReceiveDebugSessionCustomEvent(e => {
      if (e.session.name === debugConfig.name && e.event === 'output') {
        outputReceived += e.body.output;
      }
    });

    await vscode.debug.startDebugging(vscode.workspace.workspaceFolders?.[0], debugConfig);

    // Wait for session to start and terminate
    for (let i = 0; i < 60 && !sessionTerminated; i++) {
      await new Promise(res => setTimeout(res, 1000));
    }

    startListener.dispose();
    termListener.dispose();
    outputListener.dispose();

    assert.ok(sessionStarted, 'Debug session should start');
    assert.ok(sessionTerminated, 'Debug session should terminate');
    assert.match(outputReceived, /Hello, World!/);
  });
});
