// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
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
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;

namespace ICSharpCode.ILSpy
{
	/// <summary>
	/// Minimal replacement for WPF's CommandManager: lets commands tell the UI
	/// that their CanExecute state might have changed.
	/// </summary>
	public static class CommandManager
	{
		static bool requeryPending;

		static CommandManager()
		{
			InputElement.GotFocusEvent.AddClassHandler<InputElement>((_, _) => InvalidateRequerySuggested());
		}

		/// <summary>
		/// Raised (coalesced, on the UI thread) after <see cref="InvalidateRequerySuggested"/> was called.
		/// </summary>
		public static event EventHandler RequerySuggested;

		public static void InvalidateRequerySuggested()
		{
			if (requeryPending)
				return;
			requeryPending = true;
			Dispatcher.UIThread.Post(() => {
				requeryPending = false;
				RequerySuggested?.Invoke(null, EventArgs.Empty);
			}, DispatcherPriority.Background);
		}
	}

	/// <summary>
	/// Exposes a <see cref="RoutedCommand"/> as plain <see cref="ICommand"/> for menu and toolbar items.
	/// The command is routed to the focused element of the main window (or the main window itself),
	/// so it works even if the invoking menu lives in a separate popup.
	/// </summary>
	sealed class RoutedCommandAdapter : ICommand
	{
		readonly RoutedCommand command;

		public RoutedCommandAdapter(RoutedCommand command)
		{
			this.command = command ?? throw new ArgumentNullException(nameof(command));
		}

		public RoutedCommand Command => command;

		public event EventHandler CanExecuteChanged {
			add { CommandManager.RequerySuggested += value; }
			remove { CommandManager.RequerySuggested -= value; }
		}

		static IInputElement Target {
			get {
				var mainWindow = MainWindow.Instance;
				if (mainWindow == null)
					return null;
				var focused = mainWindow.FocusManager?.GetFocusedElement();
				if (focused is Avalonia.Visual visual && visual.GetVisualRoot() == mainWindow)
					return focused;
				return mainWindow;
			}
		}

		public bool CanExecute(object parameter) => command.CanExecute(parameter, Target);

		public void Execute(object parameter) => command.Execute(parameter, Target);

		public static ICommand Wrap(ICommand command)
		{
			return command is RoutedCommand routed ? new RoutedCommandAdapter(routed) : command;
		}
	}
}
