const { spawn } = require('child_process');
const path = require('path');

const adapter = spawn('node', [path.join(__dirname, 'adapter.js')], { stdio: ['pipe', 'pipe', 'pipe'] });

function send(obj) {
  const json = JSON.stringify(obj);
  const header = `Content-Length: ${Buffer.byteLength(json, 'utf8')}\r\n\r\n`;
  adapter.stdin.write(header);
  adapter.stdin.write(json);
}

adapter.stdout.on('data', d => process.stdout.write('[adapter stdout] ' + d.toString()));
adapter.stderr.on('data', d => process.stderr.write('[adapter stderr] ' + d.toString()));

const init = { seq: 1, type: 'request', command: 'initialize', arguments: { clientID: 'test', adapterID: 'xslt' } };
const setBp = { seq: 2, type: 'request', command: 'setBreakpoints', arguments: { source: { path: 'file:///Users/danieljonathan/Workspace/LearnDJ/XsltDebugger/XsltDebugger.ConsoleTest/sample/sample-inline-cs.xslt' }, breakpoints: [ { line: 23 } ] } };
const confDone = { seq: 3, type: 'request', command: 'configurationDone' };
const launch = { seq: 4, type: 'request', command: 'launch', arguments: { } };

setTimeout(() => send(init), 100);
setTimeout(() => send(setBp), 300);
setTimeout(() => send(confDone), 500);
setTimeout(() => send(launch), 700);
setTimeout(() => setTimeout(() => adapter.kill(), 2000), 3000);

adapter.on('exit', (code) => console.log('adapter exited', code));
