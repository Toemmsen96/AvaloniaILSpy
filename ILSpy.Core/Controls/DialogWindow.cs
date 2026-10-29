using Avalonia.Input;

namespace ICSharpCode.ILSpy.Controls
{
    public class DialogWindow : PlatformDependentWindow
	{
        /// <summary>
        /// Gets the result passed to <see cref="Close(object)"/>, or null if the dialog was closed otherwise.
        /// </summary>
        public object DialogResult { get; private set; }

        public new void Close(object dialogResult)
        {
            DialogResult = dialogResult;
            base.Close(dialogResult);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!e.Handled && e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
            {
                Close();
                e.Handled = true;
            }
        }
    }
}
