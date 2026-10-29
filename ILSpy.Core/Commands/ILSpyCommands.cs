// Copyright (c) 2018 Siegfried Pammer
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
using Avalonia.Input;
using AvaloniaEdit;
using ICSharpCode.ILSpy.Analyzers;

namespace ICSharpCode.ILSpy
{
	static class ILSpyCommands
	{
		public static readonly AnalyzeCommand Analyze = new AnalyzeCommand();
	}

    public static class NavigationCommands
    {
        public static RoutedCommand BrowseBack { get; } = new RoutedCommand(nameof(BrowseBack), new KeyGesture(Key.Left, KeyModifiers.Alt));
        public static RoutedCommand BrowseForward { get; } = new RoutedCommand(nameof(BrowseForward), new KeyGesture(Key.Right, KeyModifiers.Alt));
        public static RoutedCommand Search { get; } = new RoutedCommand(nameof(Search), new KeyGesture(Key.F, ApplicationCommands.CommandModifiers | KeyModifiers.Shift));
    }

    public static class ApplicationCommands
    {
        /// <summary>
        /// The modifier used for application shortcuts: Cmd on macOS, Ctrl everywhere else.
        /// </summary>
        public static KeyModifiers CommandModifiers { get; } = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

        public static RoutedCommand Open { get; } = new RoutedCommand(nameof(Open), new KeyGesture(Key.O, CommandModifiers));
        public static RoutedCommand Save { get; } = new RoutedCommand(nameof(Save), new KeyGesture(Key.S, CommandModifiers));
        public static RoutedCommand Refresh { get; } = new RoutedCommand(nameof(Refresh), new KeyGesture(Key.R, CommandModifiers));
    }
}
