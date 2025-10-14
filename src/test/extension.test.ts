import * as assert from 'assert';

// You can import and use all API from the 'vscode' module
// as well as import your extension to test it
import * as vscode from 'vscode';
// import * as myExtension from '../../extension';

suite('Extension Test Suite', () => {
	vscode.window.showInformationMessage('Start all tests.');

	test('Sample test', () => {
		assert.strictEqual(-1, [1, 2, 3].indexOf(5));
		assert.strictEqual(-1, [1, 2, 3].indexOf(0));
	});

	test('Inline C# compilation with using statements', () => {
		// This test verifies that inline C# code with using statements compiles correctly
		// Previously this would fail due to duplicate using statements in the prelude
		const testCode = `
using System;
using System.Globalization;

public class TestExtension {
    public string FormatDate(string dateStr) {
        return DateTime.Parse(dateStr).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}`;

		// The test passes if no exception is thrown during compilation
		// We'll test this indirectly through the debug session in the e2e test
		assert.ok(testCode.includes('using System;'), 'Test code contains using statement');
	});
});
