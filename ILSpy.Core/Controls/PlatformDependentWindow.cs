using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace ICSharpCode.ILSpy.Controls
{
    public class PlatformDependentWindow : Window
    {
        protected override Type StyleKeyOverride => typeof(Window);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Close shortcut
            if (OperatingSystem.IsMacOS())
            {
                // Cmd + W
                if (!e.Handled && e.KeyModifiers == KeyModifiers.Meta && e.Key == Key.W)
                {
                    Close();
                    e.Handled = true;
                }
            }
        }
    }
}
