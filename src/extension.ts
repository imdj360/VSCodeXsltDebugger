import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

const output = vscode.window.createOutputChannel('XSLT Debugger');

class XsltDebugConfigurationProvider implements vscode.DebugConfigurationProvider {
	provideDebugConfigurations(folder: vscode.WorkspaceFolder | undefined): vscode.DebugConfiguration[] {
		const baseFolder = folder?.uri.fsPath ?? '${workspaceFolder}';
		return [
			{
				type: 'xslt',
				name: 'XSLT: Launch',
				request: 'launch',
				engine: 'compiled',
				stylesheet: '${file}',
				xml: `${baseFolder}/XsltDebugger.ConsoleTest/sample/sample.xml`,
				stopOnEntry: false
			}
		];
	}

	resolveDebugConfiguration(folder: vscode.WorkspaceFolder | undefined, config: vscode.DebugConfiguration): vscode.DebugConfiguration | null | undefined {
		if (!config.type) {
			config.type = 'xslt';
		}
		if (!config.name) {
			config.name = 'XSLT: Launch';
		}
		if (!config.request) {
			config.request = 'launch';
		}

		const resolvedStylesheet = this.resolvePath(folder, config.stylesheet ?? '');
		if (!resolvedStylesheet) {
			vscode.window.showErrorMessage('Provide a valid "stylesheet" pointing to an XSLT stylesheet.');
			return undefined;
		}
		const resolvedXml = this.resolvePath(folder, config.xml ?? '');
		if (!resolvedXml) {
			vscode.window.showErrorMessage('Provide a valid "xml" path pointing to the input XML document.');
			return undefined;
		}

		config.stylesheet = resolvedStylesheet;
		config.xml = resolvedXml;
		config.engine = config.engine ?? 'compiled';
		config.stopOnEntry = !!config.stopOnEntry;

		try {
			output.appendLine(`[xslt] resolved config: engine=${config.engine}, stylesheet=${config.stylesheet}, xml=${config.xml}, stopOnEntry=${config.stopOnEntry}`);
			output.show(true);
		} catch {}

		return config;
	}

	private resolvePath(folder: vscode.WorkspaceFolder | undefined, rawPath: string): string | undefined {
		if (!rawPath || typeof rawPath !== 'string') {
			return undefined;
		}

		const expanded = rawPath.replace('${file}', vscode.window.activeTextEditor?.document.uri.fsPath ?? '');
		const workspaceFolder = folder?.uri.fsPath ?? vscode.workspace.workspaceFolders?.[0]?.uri.fsPath ?? '';
		const candidate = expanded.replace('${workspaceFolder}', workspaceFolder);
		const absolute = path.isAbsolute(candidate) ? candidate : path.join(workspaceFolder, candidate);
		return fs.existsSync(absolute) ? absolute : undefined;
	}
}

class XsltDebugAdapterDescriptorFactory implements vscode.DebugAdapterDescriptorFactory, vscode.Disposable {
	private readonly disposables: vscode.Disposable[] = [];

	constructor(private readonly context: vscode.ExtensionContext) { }

	createDebugAdapterDescriptor(_session: vscode.DebugSession): vscode.ProviderResult<vscode.DebugAdapterDescriptor> {
		// Allow using a local Node-based mock adapter during development by setting
		// environment variable XSLT_MOCK_ADAPTER=1. This helps test the DAP flow
		// without launching the .NET debug adapter.
		if (process.env['XSLT_MOCK_ADAPTER'] === '1') {
			const mock = this.context.asAbsolutePath(path.join('tools', 'mock-adapter', 'adapter.js'));
			if (fs.existsSync(mock)) {
				const options: vscode.DebugAdapterExecutableOptions = { cwd: path.dirname(mock) };
				return new vscode.DebugAdapterExecutable('node', [mock], options);
			} else {
				void vscode.window.showErrorMessage('Mock adapter not found at ' + mock);
				return undefined;
			}
		}

		const adapterPath = this.locateAdapter();
		if (!adapterPath) {
			return undefined;
		}

		const options: vscode.DebugAdapterExecutableOptions = {
			cwd: path.dirname(adapterPath)
		};
		const args = [adapterPath];

		try {
			output.appendLine(`[xslt] adapter: dotnet ${adapterPath}`);
			output.show(true);
		} catch {}

		return new vscode.DebugAdapterExecutable('dotnet', args, options);
	}

	dispose(): void {
		while (this.disposables.length > 0) {
			this.disposables.pop()?.dispose();
		}
	}

	private locateAdapter(): string | undefined {
		const candidates = [
			path.join('XsltDebugger.DebugAdapter', 'bin', 'Debug', 'net8.0', 'XsltDebugger.DebugAdapter.dll'),
			path.join('XsltDebugger.DebugAdapter', 'bin', 'Debug', 'net9.0', 'XsltDebugger.DebugAdapter.dll'),
			path.join('XsltDebugger.DebugAdapter', 'bin', 'Debug', 'net7.0', 'XsltDebugger.DebugAdapter.dll'),
			path.join('XsltDebugger.DebugAdapter', 'bin', 'Debug', 'net6.0', 'XsltDebugger.DebugAdapter.dll')
		];

		for (const relative of candidates) {
			const candidate = this.context.asAbsolutePath(relative);
			if (fs.existsSync(candidate)) {
				return candidate;
			}
		}

		void vscode.window.showErrorMessage('Cannot find XSLT debug adapter. Run "dotnet build" for the XsltDebugger.DebugAdapter project.');
		return undefined;
	}
}

export function activate(context: vscode.ExtensionContext) {
	const configProvider = new XsltDebugConfigurationProvider();
	const configRegistration = vscode.debug.registerDebugConfigurationProvider('xslt', configProvider);

	const factory = new XsltDebugAdapterDescriptorFactory(context);
	const factoryRegistration = vscode.debug.registerDebugAdapterDescriptorFactory('xslt', factory);

	context.subscriptions.push(configRegistration, factoryRegistration, factory);
}

export function deactivate() { }
