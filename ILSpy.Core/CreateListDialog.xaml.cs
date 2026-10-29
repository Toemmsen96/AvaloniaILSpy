using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ICSharpCode.ILSpy.Controls;
using ICSharpCode.TreeView;

namespace ICSharpCode.ILSpy
{
	/// <summary>
	/// Interaction logic for Create.xaml
	/// </summary>
	public partial class CreateListDialog : DialogWindow
	{
		public new bool DialogResult => base.DialogResult is true;

		internal Button okButton;
		internal Button cancelButton;
		internal TextBox ListName;

		public CreateListDialog()
		{
			this.InitializeComponent();
#if DEBUG
			this.AttachDevTools();
#endif
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
			okButton = this.FindControl<Button>("okButton");
			cancelButton = this.FindControl<Button>("cancelButton");
			ListName = this.FindControl<TextBox>("ListName");

			ListName.TextChanged += TextBox_TextChanged;

			Opened += (sender, e) => ListName.Focus();
		}

		private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			okButton.IsEnabled = !string.IsNullOrWhiteSpace(ListName.Text);
		}

		private void OKButton_Click(object sender, RoutedEventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(ListName.Text))
			{
				this.Close(true);
			}
		}

		void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			Close(false);
		}

		public string NewListName
		{
			get
			{
				return ListName.Text;
			}
		}

	}
}