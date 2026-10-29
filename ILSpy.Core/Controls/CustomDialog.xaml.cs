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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ICSharpCode.ILSpy.Controls
{
	public sealed class CustomDialog : DialogWindow
	{
		SelectableTextBlock label;
		ItemsControl buttons;
		PathIcon icon;
		readonly int acceptButton;
		readonly int cancelButton;

		/// <summary>
		/// Gets the index of the button pressed, or -1 if the dialog was closed without pressing a button.
		/// </summary>
		public int Result
		{
			get {
				return DialogResult as int? ?? -1;
			}
		}

		// public parameterless constructor for the XAML loader / designer
		public CustomDialog() : this("Message", string.Empty)
		{
		}

		public CustomDialog(string caption, string message, int acceptButton = -1, int cancelButton = -1, params string[] buttonLabels)
			: this(caption, message, MessageBoxImage.None, acceptButton, cancelButton, buttonLabels)
		{
		}

		public CustomDialog(string caption, string message, MessageBoxImage image, int acceptButton = -1, int cancelButton = -1, params string[] buttonLabels)
		{
			this.InitializeComponent();
#if DEBUG
			this.AttachDevTools();
#endif
			this.acceptButton = acceptButton;
			this.cancelButton = cancelButton;
			this.Title = caption;
			label.Text = message;

			var buttonList = new List<Button>();
			for (int i = 0; i < buttonLabels.Length; i++) {
				int index = i;
				var button = new Button {
					Content = buttonLabels[i],
					IsDefault = i == acceptButton,
					IsCancel = i == cancelButton,
				};
				if (i == acceptButton)
					button.Classes.Add("accent");
				button.Click += (sender, e) => {
					Close(index);
					e.Handled = true;
				};
				buttonList.Add(button);
			}
			buttons.ItemsSource = buttonList;

			SetIcon(image);
			Opened += (sender, e) => {
				if (acceptButton >= 0 && acceptButton < buttonList.Count)
					buttonList[acceptButton].Focus();
				else if (buttonList.Count > 0)
					buttonList[0].Focus();
			};
		}

		void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
			this.buttons = this.FindControl<ItemsControl>("buttons");
			this.label = this.FindControl<SelectableTextBlock>("content");
			this.icon = this.FindControl<PathIcon>("icon");
		}

		void SetIcon(MessageBoxImage image)
		{
			string resourceKey;
			IBrush foreground = null;
			switch (image) {
				case MessageBoxImage.Error:
					resourceKey = "ILSpy.Icon.Error";
					foreground = new SolidColorBrush(Color.FromRgb(0xD1, 0x34, 0x38));
					break;
				case MessageBoxImage.Warning:
					resourceKey = "ILSpy.Icon.Warning";
					foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xA6, 0x00));
					break;
				case MessageBoxImage.Question:
					resourceKey = "ILSpy.Icon.Question";
					break;
				case MessageBoxImage.Information:
					resourceKey = "ILSpy.Icon.Info";
					break;
				default:
					return;
			}
			if (foreground == null && this.TryFindResource("SystemAccentColor", ActualThemeVariant, out var accent) && accent is Color c)
				foreground = new SolidColorBrush(c);
			if (this.TryFindResource(resourceKey, out var geometry) && geometry is Geometry g) {
				icon.Data = g;
				icon.Foreground = foreground;
				icon.IsVisible = true;
			}
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None) {
				// Without a cancel button, Escape closes the dialog without a result.
				if (cancelButton != -1)
					Close(cancelButton);
				else
					Close();
				e.Handled = true;
				return;
			}
			base.OnKeyDown(e);
		}
	}
}
