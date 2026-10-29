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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ICSharpCode.ILSpy.Controls
{
	/// <summary>
	/// Thin wrapper around Avalonia's <see cref="IStorageProvider"/> that works with local file paths.
	/// </summary>
	public static class FileDialogs
	{
		public static FilePickerFileType Filter(string name, params string[] extensions)
		{
			return new FilePickerFileType(name) {
				Patterns = extensions.Select(e => e == "*" ? "*" : "*." + e.TrimStart('.')).ToArray()
			};
		}

		public static FilePickerFileType AllFiles => Filter("All Files", "*");

		static IStorageProvider StorageProvider => ((TopLevel)MainWindow.Instance ?? App.Current.GetMainWindow())?.StorageProvider;

		static async Task<IStorageFolder> TryGetFolder(IStorageProvider storage, string directory)
		{
			if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
				return null;
			try {
				return await storage.TryGetFolderFromPathAsync(directory);
			} catch (Exception) {
				return null;
			}
		}

		/// <summary>
		/// Shows an "open file" dialog. Returns an empty array if the dialog was cancelled.
		/// </summary>
		public static async Task<string[]> OpenFilesAsync(string title, IReadOnlyList<FilePickerFileType> filters, bool allowMultiple = true, string startDirectory = null)
		{
			var storage = StorageProvider;
			if (storage == null || !storage.CanOpen)
				return Array.Empty<string>();
			var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions {
				Title = title,
				AllowMultiple = allowMultiple,
				FileTypeFilter = filters,
				SuggestedStartLocation = await TryGetFolder(storage, startDirectory),
			});
			return files.Select(f => f.TryGetLocalPath()).Where(p => p != null).ToArray();
		}

		/// <summary>
		/// Shows a "save file" dialog. Returns null if the dialog was cancelled.
		/// </summary>
		public static async Task<string> SaveFileAsync(string title, string suggestedFileName, IReadOnlyList<FilePickerFileType> filters = null, string startDirectory = null)
		{
			var storage = StorageProvider;
			if (storage == null || !storage.CanSave)
				return null;
			var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions {
				Title = title,
				SuggestedFileName = suggestedFileName,
				FileTypeChoices = filters,
				ShowOverwritePrompt = true,
				SuggestedStartLocation = await TryGetFolder(storage, startDirectory),
			});
			return file?.TryGetLocalPath();
		}
	}
}
