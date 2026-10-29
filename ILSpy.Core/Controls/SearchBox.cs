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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace ICSharpCode.ILSpy.Controls
{
	/// <summary>
	/// A text box with a search icon and a "clear" button.
	/// <see cref="SearchTerm"/> follows <see cref="TextBox.Text"/> after <see cref="UpdateDelay"/>.
	/// </summary>
	[PseudoClasses(":hastext")]
	public class SearchBox : TextBox
	{
		public static readonly StyledProperty<TimeSpan> UpdateDelayProperty =
			AvaloniaProperty.Register<SearchBox, TimeSpan>(nameof(UpdateDelay), TimeSpan.FromMilliseconds(200));

		public static readonly StyledProperty<string> SearchTermProperty =
			AvaloniaProperty.Register<SearchBox, string>(nameof(SearchTerm), string.Empty, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

		readonly DispatcherTimer updateTimer;
		readonly Button clearButton;
		bool updatingText;

		public SearchBox()
		{
			updateTimer = new DispatcherTimer { Interval = UpdateDelay };
			updateTimer.Tick += (sender, e) => CommitSearchTerm();

			InnerLeftContent = new PathIcon {
				Width = 14,
				Height = 14,
				Margin = new Thickness(8, 0, 0, 0),
				VerticalAlignment = VerticalAlignment.Center,
				[!PathIcon.DataProperty] = this.GetResourceObservable("ILSpy.Icon.Find").ToBinding(),
				[!PathIcon.ForegroundProperty] = this.GetResourceObservable("ILSpy.SecondaryTextBrush").ToBinding(),
			};
			clearButton = new Button {
				Classes = { "paneCloseButton" },
				Margin = new Thickness(0, 0, 4, 0),
				IsVisible = false,
				Focusable = false,
				Content = new PathIcon {
					Width = 10,
					Height = 10,
					[!PathIcon.DataProperty] = this.GetResourceObservable("ILSpy.Icon.Close").ToBinding(),
				},
			};
			ToolTip.SetTip(clearButton, "Clear Search");
			clearButton.Click += (sender, e) => {
				Clear();
				Focus();
			};
			InnerRightContent = clearButton;
			UpdatePseudoclasses();
		}

		protected override Type StyleKeyOverride => typeof(TextBox);

		public TimeSpan UpdateDelay
		{
			get { return GetValue(UpdateDelayProperty); }
			set { SetValue(UpdateDelayProperty, value); }
		}

		/// <summary>
		/// The search term, updated with a delay while the user is typing.
		/// </summary>
		public string SearchTerm
		{
			get { return GetValue(SearchTermProperty); }
			set { SetValue(SearchTermProperty, value); }
		}

		void CommitSearchTerm()
		{
			updateTimer.Stop();
			updatingText = true;
			try {
				SearchTerm = Text ?? string.Empty;
			} finally {
				updatingText = false;
			}
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);

			if (change.Property == TextProperty) {
				UpdatePseudoclasses();
				if (!updatingText) {
					updateTimer.Stop();
					if (string.IsNullOrEmpty(Text) || UpdateDelay <= TimeSpan.Zero) {
						CommitSearchTerm();
					} else {
						updateTimer.Start();
					}
				}
			} else if (change.Property == SearchTermProperty && !updatingText) {
				updatingText = true;
				try {
					Text = SearchTerm;
				} finally {
					updatingText = false;
				}
			} else if (change.Property == UpdateDelayProperty) {
				updateTimer.Interval = UpdateDelay > TimeSpan.Zero ? UpdateDelay : TimeSpan.FromMilliseconds(1);
			}
		}

		private void UpdatePseudoclasses()
		{
			bool hasText = !string.IsNullOrEmpty(Text);
			PseudoClasses.Set(":hastext", hasText);
			if (clearButton != null)
				clearButton.IsVisible = hasText;
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Escape && !string.IsNullOrEmpty(Text)) {
				Clear();
				e.Handled = true;
			} else if (e.Key == Key.Enter) {
				// Search immediately
				CommitSearchTerm();
				base.OnKeyDown(e);
			} else {
				base.OnKeyDown(e);
			}
		}
	}
}
