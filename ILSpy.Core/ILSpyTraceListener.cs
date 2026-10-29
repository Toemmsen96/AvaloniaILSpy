// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ICSharpCode.TreeView;
using System.Linq;
using ICSharpCode.ILSpy.Controls;

namespace ICSharpCode.ILSpy
{
	class ILSpyTraceListener : DefaultTraceListener
	{
		[Conditional("DEBUG")]
		public static void Install()
		{
			//TODO: This was debug listeners
			Trace.Listeners.Clear();
			Trace.Listeners.Add(new ILSpyTraceListener());
		}

		public ILSpyTraceListener()
		{
			base.AssertUiEnabled = false;
		}

		HashSet<string> ignoredStacks = new HashSet<string>();
		bool dialogIsOpen;

		public override void Fail(string message)
		{
			this.Fail(message, null);
		}

		public override void Fail(string message, string detailMessage)
		{
			base.Fail(message, detailMessage); // let base class write the assert to the debug console
			string topFrame = "";
			string stackTrace = "";
			try {
				stackTrace = new StackTrace(true).ToString();
				var frames = stackTrace.Split('\r', '\n')
					.Where(f => f.Length > 0)
					.SkipWhile(f => f.Contains("ILSpyTraceListener") || f.Contains("System.Diagnostics"))
					.ToList();
				topFrame = frames[0];
				stackTrace = string.Join(Environment.NewLine, frames);
			} catch { }
			lock (ignoredStacks) {
				if (ignoredStacks.Contains(topFrame))
					return;
				if (dialogIsOpen)
					return;
				dialogIsOpen = true;
			}
			// Avalonia windows can only be shown on the UI thread: wait for the dialog there,
			// either in a nested dispatcher frame (when called on the UI thread) or by blocking the
			// calling background thread.
			int result = 0;
			try {
				if (Dispatcher.UIThread.CheckAccess()) {
					var task = ShowAssertionDialog(message, detailMessage, stackTrace);
					var frame = new DispatcherFrame();
					task.ContinueWith(_ => frame.Continue = false, TaskScheduler.FromCurrentSynchronizationContext());
					Dispatcher.UIThread.PushFrame(frame);
					result = task.Result;
				} else {
					result = Dispatcher.UIThread.InvokeAsync(() => ShowAssertionDialog(message, detailMessage, stackTrace)).GetAwaiter().GetResult();
				}
			} finally {
				dialogIsOpen = false;
			}
			if (result == 0) { // throw
				throw new AssertionFailedException(message);
			} else if (result == 1) { // debug
				Debugger.Break();
			} else if (result == 2) { // ignore
			} else if (result == 3) {
				lock (ignoredStacks) {
					ignoredStacks.Add(topFrame);
				}
			}
		}

		async Task<int> ShowAssertionDialog(string message, string detailMessage, string stackTrace)
		{
			message = message + Environment.NewLine + detailMessage + Environment.NewLine + stackTrace;
			string[] buttonTexts = { "Throw", "Debug", "Ignore", "Ignore All" };
			CustomDialog inputBox = new CustomDialog("Assertion Failed", message.TakeStartEllipsis(750), MessageBoxImage.Error, -1, 2, buttonTexts);
			inputBox.ShowInTaskbar = true; // make this window more visible, because it effectively interrupts the decompilation process.
			var owner = MainWindow.Instance;
			if (owner != null && owner.IsVisible) {
				await inputBox.ShowDialog(owner);
			} else {
				var closed = new TaskCompletionSource<bool>();
				inputBox.Closed += (sender, e) => closed.TrySetResult(true);
				inputBox.Show();
				await closed.Task;
			}
			// closing the dialog without choosing counts as "Ignore"
			return inputBox.Result < 0 ? 2 : inputBox.Result;
		}
	}

	class AssertionFailedException : Exception
	{
		public AssertionFailedException(string message) : base(message) { }
	}
}
