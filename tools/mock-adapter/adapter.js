#!/usr/bin/env node
// Minimal Debug Adapter Protocol (DAP) server for testing the XSLT extension.
// Supports: initialize, setBreakpoints, launch, continue, disconnect/terminate, threads, stackTrace
const { stdin, stdout, stderr } = process;

let seq = 1;
function nextSeq() { return seq++; }

function send(obj) {
  const json = JSON.stringify(obj);
  const header = `Content-Length: ${Buffer.byteLength(json, 'utf8')}\r\n\r\n`;
  stdout.write(header);
  stdout.write(json);
}

function sendEvent(event, body) {
  send({ seq: nextSeq(), type: 'event', event, body });
}

function sendResponse(requestSeq, command, body = {}, success = true, message = null) {
  send({ seq: nextSeq(), type: 'response', request_seq: requestSeq, success, command, message, body });
}

let buffer = Buffer.alloc(0);
stdin.on('data', chunk => {
  buffer = Buffer.concat([buffer, chunk]);
  processBuffer();
});

function processBuffer() {
  while (true) {
    const idx = buffer.indexOf('\r\n\r\n');
    if (idx === -1) return;
    const header = buffer.slice(0, idx).toString('utf8');
    const m = header.match(/Content-Length:\s*(\d+)/i);
    if (!m) { buffer = buffer.slice(idx + 4); continue; }
    const len = parseInt(m[1], 10);
    const total = idx + 4 + len;
    if (buffer.length < total) return;
    const body = buffer.slice(idx + 4, total).toString('utf8');
    buffer = buffer.slice(total);
    try {
      const msg = JSON.parse(body);
      handle(msg);
    } catch (e) {
      stderr.write('[mock-adapter] Failed to parse message: ' + e + '\n');
    }
  }
}

let breakpoints = {}; // file -> [lines]
let lastRequestSeq = 0;

function handle(msg) {
  if (!msg || msg.type !== 'request') return;
  const { seq: s, command, arguments: args } = msg;
  lastRequestSeq = s;
  stderr.write(`[mock-adapter] Request: ${command} seq=${s} args=${args ? JSON.stringify(args) : '{}'}\n`);
  switch (command) {
    case 'initialize':
      sendResponse(s, 'initialize', { capabilities: { supportsConfigurationDoneRequest: true } });
      sendEvent('initialized', {});
      break;
    case 'setBreakpoints': {
      const sourceObj = args && args.source ? args.source : { path: args && args.source && args.source.name || 'unknown' };
      // Normalize file:// URIs to filesystem paths
      let sourcePath = sourceObj.path || sourceObj.name || 'unknown';
      if (typeof sourcePath === 'string' && sourcePath.startsWith('file://')) {
        try {
          const url = new URL(sourcePath);
          sourcePath = decodeURIComponent(url.pathname);
        } catch (e) {
          // leave as-is
        }
      }

      const rawBps = Array.isArray(args && args.breakpoints) ? args.breakpoints : [];
      const lines = rawBps.map(bp => Number(bp.line));
      breakpoints[sourcePath] = lines;

      // DAP expects 1-based lines in responses; mirror what we received
      const bps = rawBps.map(bp => ({ verified: true, line: Number(bp.line) }));

      stderr.write(`[mock-adapter] setBreakpoints for source=${sourcePath} rawSource=${JSON.stringify(sourceObj)} lines=${JSON.stringify(lines)}\n`);
      sendResponse(s, 'setBreakpoints', { breakpoints: bps });
      break;
    }
    case 'configurationDone':
      sendResponse(s, 'configurationDone', {});
      break;
    case 'launch':
      sendResponse(s, 'launch', {});
      // Simulate adapter behavior: output, then stopped (entry)
      sendEvent('output', { output: `Starting XSLT transform (mock)\n`, category: 'stdout' });
      sendEvent('output', { output: `Writing transform output to: /tmp/mock-out.xml\n`, category: 'stdout' });
      // stopped at entry (line 0)
      sendEvent('stopped', { reason: 'entry', threadId: 1, allThreadsStopped: true, text: 'entry' });
      break;
    case 'threads':
      sendResponse(s, 'threads', { threads: [{ id: 1, name: 'XSLT Engine' }] });
      break;
    case 'stackTrace':
      sendResponse(s, 'stackTrace', { stackFrames: [{ id: 1, name: 'main', line: 24, column: 1, source: { path: Object.keys(breakpoints)[0] || 'sample-inline-cs.xslt' } }], totalFrames: 1 });
      break;
    case 'continue':
      // resume: if any breakpoint was set, simulate hitting it
      sendResponse(s, 'continue', { allThreadsContinued: true });
      // if breakpoints exist, fire stopped breakpoint
      const files = Object.keys(breakpoints);
      if (files.length > 0 && breakpoints[files[0]].length > 0) {
        const bpLine = breakpoints[files[0]][0];
        sendEvent('stopped', { reason: 'breakpoint', threadId: 1, allThreadsStopped: true, text: `breakpoint at ${bpLine}` });
      }
      // then send exited/terminated
      sendEvent('exited', { exitCode: 0 });
      sendEvent('terminated', {});
      break;
    case 'disconnect':
    case 'terminate':
      sendResponse(s, command, {});
      sendEvent('exited', { exitCode: 0 });
      sendEvent('terminated', {});
      break;
    default:
      sendResponse(s, command, {}, false, `Unsupported command '${command}'`);
      break;
  }
}

// Keep process alive
process.on('SIGINT', () => process.exit(0));
