using Avalonia;
using System;
using System.IO;

namespace ICSharpCode.ILSpy
{
	static class Program
	{
		[STAThread]
		static void Main(string[] args)
		{
			Directory.SetCurrentDirectory(AppContext.BaseDirectory);

			try
			{
				BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
			}
			catch (Exception exception)
			{
				Console.WriteLine("Sorry, we crashed");
				Console.WriteLine(exception.ToString());
			}
		}

		/// <summary>
		/// This method is needed for IDE previewer infrastructure
		/// </summary>
		public static AppBuilder BuildAvaloniaApp()
		{
			var builder = AppBuilder.Configure<App>()
				.UsePlatformDetect()
				.WithInterFont()
				.With(new X11PlatformOptions
				{
					UseDBusMenu = true
				});

#if DEBUG
			builder = builder.LogToTrace();
#endif

			return builder;
		}
	}
}
