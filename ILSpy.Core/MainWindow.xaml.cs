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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Documentation;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using ICSharpCode.ILSpy.Controls;
using ICSharpCode.ILSpy.Search;
using ICSharpCode.ILSpy.TextView;
using ICSharpCode.ILSpy.TreeNodes;
using ICSharpCode.TreeView;

namespace ICSharpCode.ILSpy
{
	/// <summary>
	/// The main window of the application.
	/// </summary>
	public partial class MainWindow : PlatformDependentWindow, IRoutedCommandBindable
	{
		bool refreshInProgress;
		readonly NavigationHistory<NavigationState> history = new NavigationHistory<NavigationState>();
		ILSpySettings spySettingsForMainWindow_Loaded;
		internal SessionSettings sessionSettings;
		
		internal AssemblyListManager assemblyListManager;
		AssemblyList assemblyList;
		AssemblyListTreeNode assemblyListTreeNode;
		
		readonly DecompilerTextView decompilerTextView;
		
		static MainWindow instance;

		internal Menu mainMenu;
		internal StackPanel toolBar;
		internal ComboBox languageComboBox;
		internal ComboBox languageVersionComboBox;
		internal Control statusBar;
		internal TextBlock StatusLabel;
		internal Grid mainGrid;
		internal ColumnDefinition leftColumn;
		internal ColumnDefinition rightColumn;
		internal SharpTreeView treeView;
		internal Grid rightPane;
		internal RowDefinition topPaneRow;
		internal RowDefinition textViewRow;
		internal RowDefinition bottomPaneRow;
		internal Border updatePanel;
		internal TextBlock updatePanelMessage;
		internal Button downloadOrCheckUpdateButton;
		internal DockedPane topPane;
		internal ContentControl mainPane;
		internal DockedPane bottomPane;

		public static MainWindow Instance {
			get { return instance; }
		}
		
		public SessionSettings SessionSettings {
			get { return sessionSettings; }
		}

		public IList<RoutedCommandBinding> CommandBindings { get; } = new List<RoutedCommandBinding>();

		/// <summary>
		/// Additional keyboard shortcuts for commands (the RoutedCommand itself only supports a single gesture).
		/// </summary>
		readonly List<(KeyGesture Gesture, RoutedCommand Command)> additionalGestures = new List<(KeyGesture, RoutedCommand)>();

		public MainWindow()
		{
			instance = this;
			var spySettings = ILSpySettings.Load();
			this.spySettingsForMainWindow_Loaded = spySettings;
			this.sessionSettings = new SessionSettings(spySettings);
			this.assemblyListManager = new AssemblyListManager(spySettings);

			InitializeComponent();

			this.DataContext = sessionSettings;

#if DEBUG
			this.AttachDevTools();
#endif

			decompilerTextView = App.ExportProvider.GetExportedValue<DecompilerTextView>();
			mainPane.Content = decompilerTextView;
			
			if (sessionSettings.SplitterPosition > 0 && sessionSettings.SplitterPosition < 1) {
				leftColumn.Width = new GridLength(sessionSettings.SplitterPosition, GridUnitType.Star);
				rightColumn.Width = new GridLength(1 - sessionSettings.SplitterPosition, GridUnitType.Star);
			}
			sessionSettings.FilterSettings.PropertyChanged += filterSettings_PropertyChanged;
			
			ContextMenuProvider.Add(treeView, decompilerTextView);

			RestoreWindowBounds();
		}
		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
			mainMenu = this.FindControl<Menu>("mainMenu");
			toolBar = this.FindControl<StackPanel>("toolBar");
			languageComboBox = this.FindControl<ComboBox>("languageComboBox");
			languageVersionComboBox = this.FindControl<ComboBox>("languageVersionComboBox");
			statusBar = this.FindControl<Border>("statusBar");
			StatusLabel = this.FindControl<TextBlock>("StatusLabel");
			mainGrid = this.FindControl<Grid>("mainGrid");
			leftColumn = mainGrid.ColumnDefinitions[0];
			rightColumn = mainGrid.ColumnDefinitions[2];

			treeView = this.FindControl<SharpTreeView>("treeView");
			treeView.SelectionChanged += TreeView_SelectionChanged;

			rightPane = this.FindControl<Grid>("rightPane");
			topPaneRow = rightPane.RowDefinitions[1];
			textViewRow = rightPane.RowDefinitions[3];
			bottomPaneRow = rightPane.RowDefinitions[5];

			updatePanel = this.FindControl<Border>("updatePanel");
			var updatePanelCloseButton = this.FindControl<Button>("updatePanelCloseButton");
			updatePanelCloseButton.Click += updatePanelCloseButtonClick;
			updatePanelMessage = this.FindControl<TextBlock>("updatePanelMessage");
			downloadOrCheckUpdateButton = this.FindControl<Button>("downloadOrCheckUpdateButton");
			downloadOrCheckUpdateButton.Click += downloadOrCheckUpdateButtonClick;
			topPane = this.FindControl<DockedPane>("topPane");
			topPane.CloseButtonClicked += TopPane_CloseButtonClicked;
			mainPane = this.FindControl<ContentControl>("mainPane");
			bottomPane = this.FindControl<DockedPane>("bottomPane");
			bottomPane.CloseButtonClicked += BottomPane_CloseButtonClicked;

			InitThemeSelector();

			CommandBindings.Add(new RoutedCommandBinding(ApplicationCommands.Open, OpenCommandExecuted));
			CommandBindings.Add(new RoutedCommandBinding(ApplicationCommands.Refresh, RefreshCommandExecuted));
			CommandBindings.Add(new RoutedCommandBinding(ApplicationCommands.Save, SaveCommandExecuted, SaveCommandCanExecute));
			CommandBindings.Add(new RoutedCommandBinding(NavigationCommands.BrowseBack, BackCommandExecuted, BackCommandCanExecute));
			CommandBindings.Add(new RoutedCommandBinding(NavigationCommands.BrowseForward, ForwardCommandExecuted, ForwardCommandCanExecute));
			CommandBindings.Add(new RoutedCommandBinding(NavigationCommands.Search, SearchCommandExecuted));
			additionalGestures.Add((new KeyGesture(Key.E, ApplicationCommands.CommandModifiers), NavigationCommands.Search));
			additionalGestures.Add((new KeyGesture(Key.F5), ApplicationCommands.Refresh));
			additionalGestures.Add((new KeyGesture(Key.BrowserBack), NavigationCommands.BrowseBack));
			additionalGestures.Add((new KeyGesture(Key.BrowserForward), NavigationCommands.BrowseForward));

			TemplateApplied += MainWindow_Loaded;
			// Handle shortcuts while tunneling, so that they work regardless of which control has the focus.
			AddHandler(KeyDownEvent, MainWindow_KeyDown, RoutingStrategies.Tunnel);
			AddHandler(PointerReleasedEvent, MainWindow_PointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
			mainMenu.AttachedToVisualTree += MenuAttached;
		}

		bool menuInitialized;

		private void MenuAttached(object sender, VisualTreeAttachmentEventArgs e)
		{
			if (menuInitialized)
				return;
			menuInitialized = true;
			if (NativeMenu.GetIsNativeMenuExported(this))
			{
				mainMenu.IsVisible = false;
				InitNativeMenu();
			}
			else
			{
				InitMainMenu();
			}
		}

		private void MainWindow_KeyDown(object sender, KeyEventArgs e)
		{
			foreach (var commandBinding in CommandBindings)
			{
				if (commandBinding.Command.Gesture?.Matches(e) == true)
				{
					commandBinding.Command.Execute(null, this);
					e.Handled = true;
					return;
				}
			}
			foreach (var (gesture, command) in additionalGestures)
			{
				if (gesture.Matches(e))
				{
					command.Execute(null, this);
					e.Handled = true;
					return;
				}
			}
		}

		void MainWindow_PointerReleased(object sender, PointerReleasedEventArgs e)
		{
			// Support the "back" and "forward" buttons of the mouse.
			switch (e.InitialPressMouseButton) {
				case MouseButton.XButton1:
					NavigationCommands.BrowseBack.Execute(null, this);
					e.Handled = true;
					break;
				case MouseButton.XButton2:
					NavigationCommands.BrowseForward.Execute(null, this);
					e.Handled = true;
					break;
			}
		}

		#region Themes

		static readonly string[] themeNames = { "System", "Light", "Dark" };

		void InitThemeSelector()
		{
			var themeComboBox = this.FindControl<ComboBox>("themeComboBox");
			themeComboBox.ItemsSource = themeNames;
			int selectedTheme = Array.FindIndex(themeNames, n => string.Equals(n, sessionSettings.Theme, StringComparison.OrdinalIgnoreCase));
			themeComboBox.SelectedIndex = selectedTheme < 0 ? 0 : selectedTheme;
			ApplyRequestedTheme(themeNames[themeComboBox.SelectedIndex]);
			themeComboBox.SelectionChanged += (sender, e) => {
				if (themeComboBox.SelectedIndex >= 0)
					ApplyRequestedTheme(themeNames[themeComboBox.SelectedIndex]);
			};

			RegisterHighlightings(Application.Current.ActualThemeVariant);
			Application.Current.ActualThemeVariantChanged += (sender, e) => {
				RegisterHighlightings(Application.Current.ActualThemeVariant);
				// The C# output is colorized while decompiling, so the code has to be decompiled again.
				RefreshDecompiledView(recordHistory: false);
			};
		}

		void ApplyRequestedTheme(string name)
		{
			sessionSettings.Theme = name;
			Application.Current.RequestedThemeVariant = name switch {
				"Light" => ThemeVariant.Light,
				"Dark" => ThemeVariant.Dark,
				_ => ThemeVariant.Default,
			};
		}

		static void RegisterHighlightings(ThemeVariant theme)
		{
			bool dark = theme == ThemeVariant.Dark;
			RegisterHighlighting("ILAsm", ".il", dark ? "ILAsm-Mode-Dark.xshd" : "ILAsm-Mode.xshd");
			RegisterHighlighting("C#", ".cs", dark ? "CSharp-Mode-Dark.xshd" : "CSharp-Mode.xshd");
		}

		static void RegisterHighlighting(string name, string extension, string resourceName)
		{
			HighlightingManager.Instance.RegisterHighlighting(
				name, new[] { extension },
				delegate {
					using (Stream s = typeof(DecompilerTextView).Assembly.GetManifestResourceStream("ICSharpCode.ILSpy.Themes." + resourceName))
					using (XmlTextReader reader = new XmlTextReader(s)) {
						return HighlightingLoader.Load(reader, HighlightingManager.Instance);
					}
				});
		}

		#endregion

		void RestoreWindowBounds()
		{
			if (sessionSettings.WindowState == WindowState.Maximized || sessionSettings.WindowState == WindowState.FullScreen) {
				WindowState = sessionSettings.WindowState;
				return;
			}
			var bounds = sessionSettings.WindowBounds;
			double scaling = DesktopScaling;
			bool boundsOK = false;
			foreach (var screen in Screens.All) {
				var intersection = bounds.Intersect(screen.WorkingArea.ToRect(scaling));
				if (intersection.Width > 10 && intersection.Height > 10)
					boundsOK = true;
			}
			if (!boundsOK)
				bounds = SessionSettings.DefaultWindowBounds;
			WindowStartupLocation = boundsOK ? WindowStartupLocation.Manual : WindowStartupLocation.CenterScreen;
			Width = bounds.Width;
			Height = bounds.Height;
			if (boundsOK)
				Position = PixelPoint.FromPoint(bounds.Position, scaling);
		}

		#region Toolbar extensibility

		void InitToolbar()
		{
			int navigationPos = 0;
			int openPos = 1;
			var toolbarCommands = App.ExportProvider.GetExports<ICommand, IToolbarCommandMetadata>("ToolbarCommand");
			var toolbarItems = toolBar.Children;
			foreach (var commandGroup in toolbarCommands.OrderBy(c => c.Metadata.ToolbarOrder).GroupBy(c => c.Metadata.ToolbarCategory)) {
				if (commandGroup.Key == nameof(Properties.Resources.Navigation)) {
					foreach (var command in commandGroup) {
						toolbarItems.Insert(navigationPos++, MakeToolbarItem(command));
						openPos++;
					}
				} else if (commandGroup.Key == nameof(Properties.Resources.Open)) {
					foreach (var command in commandGroup) {
						toolbarItems.Insert(openPos++, MakeToolbarItem(command));
					}
				} else {
					toolbarItems.Add(new Border { Classes = { "toolbarSeparator" } });
					foreach (var command in commandGroup) {
						toolbarItems.Add(MakeToolbarItem(command));
					}
				}
			}
		}

		Button MakeToolbarItem(Lazy<ICommand, IToolbarCommandMetadata> command)
		{
			var toolbarButton = new Button {
				Classes = { "toolbar" },
				Command = RoutedCommandAdapter.Wrap(CommandWrapper.Unwrap(command.Value)),
				Tag = command.Metadata.Tag,
				Content = CreateIcon(command.Value, command.Metadata.ToolbarIcon)
			};
			string toolTip = GetResourceString(command.Metadata.ToolTip);
			if (CommandWrapper.Unwrap(command.Value) is RoutedCommand routed && routed.Gesture != null)
				toolTip += " (" + routed.Gesture.ToString("p", null) + ")";
			ToolTip.SetTip(toolbarButton, toolTip);
			return toolbarButton;
		}

		/// <summary>
		/// Creates the icon for a toolbar button or a menu item.
		/// Prefers a vector icon "ILSpy.Icon.{name}" over the bitmap, as it looks good in all themes.
		/// </summary>
		internal Control CreateIcon(object part, string icon)
		{
			if (string.IsNullOrEmpty(icon))
				return null;
			if (part.GetType().Assembly == typeof(MainWindow).Assembly
				&& this.TryFindResource("ILSpy.Icon." + Path.GetFileNameWithoutExtension(icon), out var resource)
				&& resource is Geometry geometry) {
				return new PathIcon { Data = geometry, Width = 16, Height = 16 };
			}
			return new Image {
				Width = 16,
				Height = 16,
				Source = Images.LoadImage(part, icon)
			};
		}
		#endregion
		
		#region Main Menu extensibility
		
		void InitMainMenu()
		{
			var mainMenuCommands = App.ExportProvider.GetExports<ICommand, IMainMenuCommandMetadata>("MainMenuCommand");
			foreach (var topLevelMenu in mainMenuCommands.OrderBy(c => c.Metadata.MenuOrder).GroupBy(c => GetResourceString(c.Metadata.Menu))) {
				MenuItem topLevelMenuItem = mainMenu.Items.OfType<MenuItem>().FirstOrDefault(m => GetResourceString(m.Header as string) == topLevelMenu.Key);
				foreach (var category in topLevelMenu.GroupBy(c => GetResourceString(c.Metadata.MenuCategory))) {
					if (topLevelMenuItem == null) {
						topLevelMenuItem = new MenuItem();
						topLevelMenuItem.Header = GetResourceString(category.First().Metadata.Menu, keepAccessKey: true);
						mainMenu.Items.Add(topLevelMenuItem);
					} else if (topLevelMenuItem.Items.Count > 0) {
						topLevelMenuItem.Items.Add(new Separator());
					}
					foreach (var entry in category) {
						MenuItem menuItem = new MenuItem();
						var command = CommandWrapper.Unwrap(entry.Value);
						menuItem.Command = RoutedCommandAdapter.Wrap(command);
						if (command is RoutedCommand routed && routed.Gesture != null)
							menuItem.InputGesture = routed.Gesture;
						if (!string.IsNullOrEmpty(GetResourceString(entry.Metadata.Header)))
							menuItem.Header = GetResourceString(entry.Metadata.Header, keepAccessKey: true);
						menuItem.Icon = CreateIcon(entry.Value, entry.Metadata.MenuIcon);
						
						menuItem.IsEnabled = entry.Metadata.IsEnabled;
						topLevelMenuItem.Items.Add(menuItem);
					}
				}
			}
		}

		void InitNativeMenu()
		{
			var mainMenuCommands = App.ExportProvider.GetExports<ICommand, IMainMenuCommandMetadata>("MainMenuCommand");
			var mainMenuItems = NativeMenu.GetMenu(this).Items;
			foreach (var topLevelMenu in mainMenuCommands.OrderBy(c => c.Metadata.MenuOrder).GroupBy(c => GetResourceString(c.Metadata.Menu)))
			{
				NativeMenuItem topLevelMenuItem = mainMenuItems.OfType<NativeMenuItem>().FirstOrDefault(m => (GetResourceString(m.Header as string)) == topLevelMenu.Key);
				if (topLevelMenuItem == null)
				{
					topLevelMenuItem = new NativeMenuItem();
					topLevelMenuItem.Header = GetResourceString(topLevelMenu.Key);
					topLevelMenuItem.Menu = new NativeMenu();
					mainMenuItems.Add(topLevelMenuItem);
				}

				var topLevelMenuItems = topLevelMenuItem.Menu.Items;
				foreach (var category in topLevelMenu.GroupBy(c => GetResourceString(c.Metadata.MenuCategory)))
				{
					if (topLevelMenuItems.Count > 0)
					{
						topLevelMenuItems.Add(new NativeMenuItemSeparator());
					}
					foreach (var entry in category)
					{
						NativeMenuItem menuItem = new NativeMenuItem();
						var command = CommandWrapper.Unwrap(entry.Value);
						menuItem.Command = RoutedCommandAdapter.Wrap(command);
						if (command is RoutedCommand routed && routed.Gesture != null)
							menuItem.Gesture = routed.Gesture;
						if (!string.IsNullOrEmpty(GetResourceString(entry.Metadata.Header)))
							menuItem.Header = GetResourceString(entry.Metadata.Header);

						menuItem.IsEnabled = entry.Metadata.IsEnabled;
						topLevelMenuItems.Add(menuItem);
					}
				}
			}
		}

		/// <summary>
		/// Gets the localized string for <paramref name="key"/>, or the key itself if there is no such resource.
		/// Access key markers ('_') are removed unless <paramref name="keepAccessKey"/> is set.
		/// </summary>
		internal static string GetResourceString(string key, bool keepAccessKey = false)
		{
			var str = !string.IsNullOrEmpty(key) ? Properties.Resources.ResourceManager.GetString(key) : null;
			if (string.IsNullOrEmpty(str))
				str = key;
			if (str == null || keepAccessKey)
				return str;
			return str.Replace("_", string.Empty);
		}

		#endregion
		
		public AssemblyList CurrentAssemblyList {
			get { return assemblyList; }
		}
		
		public event NotifyCollectionChangedEventHandler CurrentAssemblyListChanged;
		
		List<LoadedAssembly> commandLineLoadedAssemblies = new List<LoadedAssembly>();

		List<string> nugetPackagesToLoad = new List<string>();
		
		bool HandleCommandLineArguments(CommandLineArguments args)
		{
			int i = 0;
			while (i < args.AssembliesToLoad.Count) {
				var asm = args.AssembliesToLoad[i];
				if (Path.GetExtension(asm) == ".nupkg") {
					nugetPackagesToLoad.Add(asm);
					args.AssembliesToLoad.RemoveAt(i);
				} else {
					i++;
				}
			}
			LoadAssemblies(args.AssembliesToLoad, commandLineLoadedAssemblies, focusNode: false);
			if (args.Language != null)
				sessionSettings.FilterSettings.Language = Languages.GetLanguage(args.Language);
			return true;
		}

		/// <summary>
		/// Called on startup or when passed arguments via WndProc from a second instance.
		/// In the format case, spySettings is non-null; in the latter it is null.
		/// </summary>
		void HandleCommandLineArgumentsAfterShowList(CommandLineArguments args, ILSpySettings spySettings = null)
		{
			if (nugetPackagesToLoad.Count > 0) {
				var relevantPackages = nugetPackagesToLoad.ToArray();
				nugetPackagesToLoad.Clear();

				// Show the nuget package open dialog after the command line/window message was processed.
				Dispatcher.UIThread.InvokeAsync(new Action(() => LoadAssemblies(relevantPackages, commandLineLoadedAssemblies, focusNode: false)), DispatcherPriority.Normal);

			}
			var relevantAssemblies = commandLineLoadedAssemblies.ToList();
			commandLineLoadedAssemblies.Clear(); // clear references once we don't need them anymore
			NavigateOnLaunch(args.NavigateTo, sessionSettings.ActiveTreeViewPath, spySettings, relevantAssemblies);
			if (args.Search != null)
			{
				SearchPane.Instance.SearchTerm = args.Search;
				SearchPane.Instance.Show();
			}
		}

		async void NavigateOnLaunch(string navigateTo, string[] activeTreeViewPath, ILSpySettings spySettings, List<LoadedAssembly> relevantAssemblies)
		{
			var initialSelection = treeView.SelectedItem;
			if (navigateTo != null) {
				bool found = false;
				if (navigateTo.StartsWith("N:", StringComparison.Ordinal)) {
					string namespaceName = navigateTo.Substring(2);
					foreach (LoadedAssembly asm in relevantAssemblies) {
						AssemblyTreeNode asmNode = assemblyListTreeNode.FindAssemblyNode(asm);
						if (asmNode != null) {
							// FindNamespaceNode() blocks the UI if the assembly is not yet loaded,
							// so use an async wait instead.
							await asm.GetPEFileAsync().Catch<Exception>(ex => { });
							NamespaceTreeNode nsNode = asmNode.FindNamespaceNode(namespaceName);
							if (nsNode != null) {
								found = true;
								if (treeView.SelectedItem == initialSelection) {
									SelectNode(nsNode);
								}
								break;
							}
						}
					}
				}
				else if (navigateTo == "none")
				{
					// Don't navigate anywhere; start empty.
					// Used by ILSpy VS addin, it'll send us the real location to navigate to via IPC.
					found = true;

				} else {
					IEntity mr = await Task.Run(() => FindEntityInRelevantAssemblies(navigateTo, relevantAssemblies));
					if (mr != null && mr.ParentModule.PEFile != null) {
						found = true;
						if (treeView.SelectedItem == initialSelection) {
							JumpToReference(mr);
						}
					}
				}
				if (!found && treeView.SelectedItem == initialSelection) {
					AvaloniaEditTextOutput output = new AvaloniaEditTextOutput();
					output.Write(string.Format("Cannot find '{0}' in command line specified assemblies.", navigateTo));
					decompilerTextView.ShowText(output);
				}
			} else if (relevantAssemblies.Count == 1) {
				// NavigateTo == null and an assembly was given on the command-line:
				// Select the newly loaded assembly
				AssemblyTreeNode asmNode = assemblyListTreeNode.FindAssemblyNode(relevantAssemblies[0]);
				if (asmNode != null && treeView.SelectedItem == initialSelection) {
					SelectNode(asmNode);
				}
			} else if (spySettings != null) {
				SharpTreeNode node = null;
				if (activeTreeViewPath?.Length > 0) {
					foreach (var asm in assemblyList.GetAssemblies()) {
						if (asm.FileName == activeTreeViewPath[0]) {
							// FindNodeByPath() blocks the UI if the assembly is not yet loaded,
							// so use an async wait instead.
							await asm.GetPEFileAsync().Catch<Exception>(ex => { });
						}
					}
					node = FindNodeByPath(activeTreeViewPath, true);
				}
				if (treeView.SelectedItem == initialSelection) {
					if (node != null) {
						SelectNode(node);

						// only if not showing the about page, perform the update check:
						ShowMessageIfUpdatesAvailableAsync(spySettings);
					} else {
						AboutPage.Display(decompilerTextView);
					}
				}
			}
		}

		private IEntity FindEntityInRelevantAssemblies(string navigateTo, IEnumerable<LoadedAssembly> relevantAssemblies)
		{
			ITypeReference typeRef = null;
			IMemberReference memberRef = null;
			if (navigateTo.StartsWith("T:", StringComparison.Ordinal)) {
				typeRef = IdStringProvider.ParseTypeName(navigateTo);
			}
			else {
				memberRef = IdStringProvider.ParseMemberIdString(navigateTo);
				typeRef = memberRef.DeclaringTypeReference;
			}
			foreach (LoadedAssembly asm in relevantAssemblies.ToList()) {
				var module = asm.GetPEFileOrNull();
				if (CanResolveTypeInPEFile(module, typeRef, out var typeHandle)) {
					ICompilation compilation = typeHandle.Kind == HandleKind.ExportedType
						? new DecompilerTypeSystem(module, module.GetAssemblyResolver())
						: new SimpleCompilation(module, MinimalCorlib.Instance);
					return memberRef == null
						? typeRef.Resolve(new SimpleTypeResolveContext(compilation)) as ITypeDefinition
						: (IEntity)memberRef.Resolve(new SimpleTypeResolveContext(compilation));
				}
			}
			return null;
		}

		private bool CanResolveTypeInPEFile(PEFile module, ITypeReference typeRef, out EntityHandle typeHandle)
		{
			switch (typeRef)
			{
				case GetPotentiallyNestedClassTypeReference topLevelType:
					typeHandle = topLevelType.ResolveInPEFile(module);
					return !typeHandle.IsNil;
				case NestedTypeReference nestedType:
					if (!CanResolveTypeInPEFile(module, nestedType.DeclaringTypeReference, out typeHandle))
						return false;
					if (typeHandle.Kind == HandleKind.ExportedType)
						return true;
					var typeDef = module.Metadata.GetTypeDefinition((TypeDefinitionHandle)typeHandle);
					typeHandle = typeDef.GetNestedTypes().FirstOrDefault(t => {
						var td = module.Metadata.GetTypeDefinition(t);
						var typeName = ReflectionHelper.SplitTypeParameterCountFromReflectionName(module.Metadata.GetString(td.Name), out int typeParameterCount);
						return nestedType.AdditionalTypeParameterCount == typeParameterCount && nestedType.Name == typeName;
					});
					return !typeHandle.IsNil;
				default:
					typeHandle = default;
					return false;
			}
		}

		void MainWindow_Loaded(object sender, EventArgs e)
		{
			TemplateApplied -= MainWindow_Loaded;
			treeView.Focus();

			InitToolbar();

			ILSpySettings spySettings = this.spySettingsForMainWindow_Loaded;
			this.spySettingsForMainWindow_Loaded = null;
			var loadPreviousAssemblies = Options.MiscSettingsPanel.CurrentMiscSettings.LoadPreviousAssemblies;

			if (loadPreviousAssemblies) {
				// Load AssemblyList only in Loaded event so that WPF is initialized before we start the CPU-heavy stuff.
				// This makes the UI come up a bit faster.
				this.assemblyList = assemblyListManager.LoadList(spySettings, sessionSettings.ActiveAssemblyList);
			} else {
				this.assemblyList = new AssemblyList(AssemblyListManager.DefaultListName);
				assemblyListManager.ClearAll();
			}

			HandleCommandLineArguments(App.CommandLineArguments);

			if (assemblyList.GetAssemblies().Length == 0
				&& assemblyList.ListName == AssemblyListManager.DefaultListName
				&& loadPreviousAssemblies) {
				LoadInitialAssemblies();
			}

			ShowAssemblyList(this.assemblyList);

			if (sessionSettings.ActiveAutoLoadedAssembly != null) {
				this.assemblyList.Open(sessionSettings.ActiveAutoLoadedAssembly, true);
			}

			Dispatcher.UIThread.InvokeAsync(new Action(() => OpenAssemblies(spySettings)), DispatcherPriority.Loaded);
		}

		void OpenAssemblies(ILSpySettings spySettings)
		{
			HandleCommandLineArgumentsAfterShowList(App.CommandLineArguments, spySettings);

			AvaloniaEditTextOutput output = new AvaloniaEditTextOutput();
			if (FormatExceptions(App.StartupExceptions.ToArray(), output))
				decompilerTextView.ShowText(output);
		}
		
		bool FormatExceptions(App.ExceptionData[] exceptions, ITextOutput output)
		{
			var stringBuilder = new StringBuilder();
			var result = FormatExceptions(exceptions, stringBuilder);
			if (result)
			{
				output.Write(stringBuilder.ToString());
			}
			return result;
		}

		internal static bool FormatExceptions(App.ExceptionData[] exceptions, StringBuilder output)
		{
			if (exceptions.Length == 0) return false;
			bool first = true;
			
			foreach (var item in exceptions) {
				if (first)
					first = false;
				else
					output.AppendLine("-------------------------------------------------");
				output.AppendLine("Error(s) loading plugin: " + item.PluginName);
				if (item.Exception is System.Reflection.ReflectionTypeLoadException) {
					var e = (System.Reflection.ReflectionTypeLoadException)item.Exception;
					foreach (var ex in e.LoaderExceptions) {
						output.AppendLine(ex.ToString());
						output.AppendLine();
					}
				} else
					output.AppendLine(item.Exception.ToString());
			}
			
			return true;
		}
		
		#region Update Check
		string updateAvailableDownloadUrl;
		
		public void ShowMessageIfUpdatesAvailableAsync(ILSpySettings spySettings, bool forceCheck = false)
		{
			Task<string> result;
			if (forceCheck) {
				result = AboutPage.CheckForUpdatesAsync(spySettings);
			} else {
				result = AboutPage.CheckForUpdatesIfEnabledAsync(spySettings);
			}
			result.ContinueWith(task => AdjustUpdateUIAfterCheck(task, forceCheck), TaskScheduler.FromCurrentSynchronizationContext());
		}
		
		void updatePanelCloseButtonClick(object sender, RoutedEventArgs e)
		{
			updatePanel.IsVisible = false;
		}
		
		void downloadOrCheckUpdateButtonClick(object sender, RoutedEventArgs e)
		{
			if (updateAvailableDownloadUrl != null) {
				MainWindow.OpenLink(updateAvailableDownloadUrl);
			} else {
				updatePanel.IsVisible = false;
				AboutPage.CheckForUpdatesAsync(ILSpySettings.Load())
					.ContinueWith(task => AdjustUpdateUIAfterCheck(task, true), TaskScheduler.FromCurrentSynchronizationContext());
			}
		}

		void AdjustUpdateUIAfterCheck(Task<string> task, bool displayMessage)
		{
			updateAvailableDownloadUrl = task.Result;
			updatePanel.IsVisible = displayMessage ? true : false;
			if (task.Result != null) {
				updatePanelMessage.Text = Properties.Resources.ILSpyVersionAvailable;
				downloadOrCheckUpdateButton.Content = Properties.Resources.Download;
			}
			else {
				updatePanelMessage.Text = Properties.Resources.UpdateILSpyFound;
				downloadOrCheckUpdateButton.Content = Properties.Resources.CheckAgain;
			}
		}
		#endregion
		
		public void ShowAssemblyList(string name)
		{
			ILSpySettings settings = ILSpySettings.Load();
			AssemblyList list = this.assemblyListManager.LoadList(settings, name);
			//Only load a new list when it is a different one
			if (list.ListName != CurrentAssemblyList.ListName)
			{
				ShowAssemblyList(list);
			}
		}
		
		void ShowAssemblyList(AssemblyList assemblyList)
		{
			history.Clear();
			this.assemblyList = assemblyList;
			
			assemblyList.assemblies.CollectionChanged += assemblyList_Assemblies_CollectionChanged;
			
			assemblyListTreeNode = new AssemblyListTreeNode(assemblyList);
			assemblyListTreeNode.FilterSettings = sessionSettings.FilterSettings.Clone();
			assemblyListTreeNode.Select = SelectNode;
			treeView.Root = assemblyListTreeNode;
			
			if (assemblyList.ListName == AssemblyListManager.DefaultListName)
#if DEBUG
				this.Title = $"ILSpy {RevisionClass.FullVersion}";
#else
				this.Title = "ILSpy";
#endif
			else
#if DEBUG
				this.Title = $"ILSpy {RevisionClass.FullVersion} - " + assemblyList.ListName;
#else
				this.Title = "ILSpy - " + assemblyList.ListName;
#endif
		}

		void assemblyList_Assemblies_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action == NotifyCollectionChangedAction.Reset) {
				history.RemoveAll(_ => true);
			}
			if (e.OldItems != null) {
				var oldAssemblies = new HashSet<LoadedAssembly>(e.OldItems.Cast<LoadedAssembly>());
				history.RemoveAll(n => n.TreeNodes.Any(
					nd => nd.AncestorsAndSelf().OfType<AssemblyTreeNode>().Any(
						a => oldAssemblies.Contains(a.LoadedAssembly))));
			}
			if (CurrentAssemblyListChanged != null)
				CurrentAssemblyListChanged(this, e);
		}

		void LoadInitialAssemblies()
		{
			// Called when loading an empty assembly list; so that
			// the user can see something initially.
			System.Reflection.Assembly[] initialAssemblies = {
				typeof(object).Assembly,
				typeof(Uri).Assembly,
				typeof(System.Linq.Enumerable).Assembly,
				typeof(System.Xml.XmlDocument).Assembly,
			};
			foreach (System.Reflection.Assembly asm in initialAssemblies)
				assemblyList.OpenAssembly(asm.Location);
		}

		void filterSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			RefreshTreeViewFilter();
			if (e.PropertyName == "Language" || e.PropertyName == "LanguageVersion") {
				DecompileSelectedNodes(recordHistory: false);
			}
		}

		public void RefreshTreeViewFilter()
		{
			// filterSettings is mutable; but the ILSpyTreeNode filtering assumes that filter settings are immutable.
			// Thus, the main window will use one mutable instance (for data-binding), and assign a new clone to the ILSpyTreeNodes whenever the main
			// mutable instance changes.
			if (assemblyListTreeNode != null)
				assemblyListTreeNode.FilterSettings = sessionSettings.FilterSettings.Clone();
		}

		internal AssemblyListTreeNode AssemblyListTreeNode {
			get { return assemblyListTreeNode; }
		}

		#region Node Selection

		public void SelectNode(SharpTreeNode obj)
		{
			if (obj != null) {
				if (!obj.AncestorsAndSelf().Any(node => node.IsHidden)) {
					// Set both the selection and focus to ensure that keyboard navigation works as expected.
					treeView.FocusNode(obj);
					treeView.SelectedItem = obj;
				} else {
					MessageBox.Show("Navigation failed because the target is hidden or a compiler-generated class.\n" +
						"Please disable all filters that might hide the item (i.e. activate " +
						"\"View > Show internal types and members\") and try again.",
						"ILSpy", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				}
			}
		}

		public void SelectNodes(IEnumerable<SharpTreeNode> nodes)
		{
			if (nodes.Any() && nodes.All(n => !n.AncestorsAndSelf().Any(a => a.IsHidden))) {
				treeView.FocusNode(nodes.First());
				treeView.SetSelectedNodes(nodes);
			}
		}

		/// <summary>
		/// Retrieves a node using the .ToString() representations of its ancestors.
		/// </summary>
		public SharpTreeNode FindNodeByPath(string[] path, bool returnBestMatch)
		{
			if (path == null)
				return null;
			SharpTreeNode node = treeView.Root;
			SharpTreeNode bestMatch = node;
			foreach (var element in path) {
				if (node == null)
					break;
				bestMatch = node;
				node.EnsureLazyChildren();
				var ilSpyTreeNode = node as ILSpyTreeNode;
				if (ilSpyTreeNode != null)
					ilSpyTreeNode.EnsureChildrenFiltered();
				node = node.Children.FirstOrDefault(c => c.ToString() == element);
			}
			if (returnBestMatch)
				return node ?? bestMatch;
			else
				return node;
		}

		/// <summary>
		/// Gets the .ToString() representation of the node's ancestors.
		/// </summary>
		public static string[] GetPathForNode(SharpTreeNode node)
		{
			if (node == null)
				return null;
			List<string> path = new List<string>();
			while (node.Parent != null) {
				path.Add(node.ToString());
				node = node.Parent;
			}
			path.Reverse();
			return path.ToArray();
		}

		public ILSpyTreeNode FindTreeNode(object reference)
		{
			switch (reference) {
				case PEFile asm:
					return assemblyListTreeNode.FindAssemblyNode(asm);
				case Resource res:
					return assemblyListTreeNode.FindResourceNode(res);
				case ITypeDefinition type:
					return assemblyListTreeNode.FindTypeNode(type);
				case IField fd:
					return assemblyListTreeNode.FindFieldNode(fd);
				case IMethod md:
					return assemblyListTreeNode.FindMethodNode(md);
				case IProperty pd:
					return assemblyListTreeNode.FindPropertyNode(pd);
				case IEvent ed:
					return assemblyListTreeNode.FindEventNode(ed);
				default:
					return null;
			}
		}

		public void JumpToReference(object reference)
		{
			JumpToReferenceAsync(reference).HandleExceptions();
		}

		/// <summary>
		/// Jumps to the specified reference.
		/// </summary>
		/// <returns>
		/// Returns a task that will signal completion when the decompilation of the jump target has finished.
		/// The task will be marked as canceled if the decompilation is canceled.
		/// </returns>
		public Task JumpToReferenceAsync(object reference)
		{
			decompilationTask = TaskHelper.CompletedTask;
			switch (reference) {
				case Decompiler.Disassembler.OpCodeInfo opCode:
					OpenLink(opCode.Link);
					break;
				case ValueTuple<PEFile, System.Reflection.Metadata.EntityHandle> unresolvedEntity:
					var typeSystem = new DecompilerTypeSystem(unresolvedEntity.Item1, unresolvedEntity.Item1.GetAssemblyResolver(),
						TypeSystemOptions.Default | TypeSystemOptions.Uncached);
					reference = typeSystem.MainModule.ResolveEntity(unresolvedEntity.Item2);
					goto default;
				default:
					ILSpyTreeNode treeNode = FindTreeNode(reference);
					if (treeNode != null)
						SelectNode(treeNode);
					break;
			}
			return decompilationTask;
		}

		/// <summary>
		/// TODO: Opens the link in browser.
		/// </summary>
		/// <param name="link">Link.</param>
		public static void OpenLink(string link)
		{
			try {
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					Process.Start("open", link);
				}
				else
				{
					Process.Start("xdg-open", link);
				}
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			} catch (Exception) {
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
				// Process.Start can throw several errors (not all of them documented),
				// just ignore all of them.
			}
		}

		/// <summary>
		/// Opens the folder containing <paramref name="path"/> in the file manager (selecting the file where supported).
		/// </summary>
		/// <param name="path">full path</param>
		public static void OpenFolder(string path)
		{
			try
			{
				if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					Process.Start("explorer.exe", $"/select,\"{path}\"");
				}
				else if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					Process.Start(new ProcessStartInfo("open") { ArgumentList = { "-R", path } });
				}
				else
				{
					// xdg-open would open the file itself, so open the containing directory instead.
					string directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
					Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { directory } });
				}
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			}
			catch (Exception)
			{
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
				// Process.Start can throw several errors (not all of them documented),
				// just ignore all of them.
			}
		}

		/// <summary>
		/// Open command line interface
		/// </summary>
		/// <param name="path">Full path.</param>
		public static void OpenCommandLine(string path)
		{
			try
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					Process.Start(new ProcessStartInfo("cmd.exe") { WorkingDirectory = path, UseShellExecute = true });
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					Process.Start(new ProcessStartInfo("open") { ArgumentList = { "-a", "Terminal", path } });
				}
				else
				{
					// Try the common terminal emulators; they all start in the working directory.
					foreach (var terminal in new[] { "x-terminal-emulator", "gnome-terminal", "konsole", "xfce4-terminal", "xterm" }) {
						try {
							Process.Start(new ProcessStartInfo(terminal) { WorkingDirectory = path });
							return;
						} catch (System.ComponentModel.Win32Exception) {
							// not installed, try the next one
						}
					}
				}
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
			} catch (Exception) {
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
				// Process.Start can throw several errors (not all of them documented),
				// just ignore all of them.
			}
		}
		#endregion

		#region Open/Refresh
		async void OpenCommandExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			e.Handled = true;
			var filenames = await FileDialogs.OpenFilesAsync(GetResourceString(nameof(Properties.Resources._Open)), new[] {
				FileDialogs.Filter(".NET assemblies", "dll", "exe", "winmd"),
				FileDialogs.Filter("NuGet packages (*.nupkg)", "nupkg"),
				FileDialogs.AllFiles,
			});
			if (filenames.Length > 0)
			{
				OpenFiles(filenames);
			}
		}
		
		public void OpenFiles(string[] fileNames, bool focusNode = true)
		{
			if (fileNames == null)
				throw new ArgumentNullException(nameof(fileNames));

			if (focusNode)
				treeView.UnselectAll();

			LoadAssemblies(fileNames, focusNode: focusNode);
		}

		async void LoadAssemblies(IEnumerable<string> fileNames, List<LoadedAssembly> loadedAssemblies = null, bool focusNode = true)
		{
			SharpTreeNode lastNode = null;
			foreach (string file in fileNames) {
				switch (Path.GetExtension(file)) {
					case ".nupkg":
						LoadedNugetPackage package;
						try {
							package = new LoadedNugetPackage(file);
						} catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException) {
							await MessageBox.Show(ex.Message, "Cannot open NuGet package", MessageBoxButton.OK, MessageBoxImage.Error);
							break;
						}
						var selectionDialog = new NugetPackageBrowserDialog(package);
						if (await selectionDialog.ShowDialog<bool>(this) != true)
							break;
						foreach (var entry in selectionDialog.SelectedItems)
						{
							var nugetAsm = assemblyList.OpenAssembly("nupkg://" + file + ";" + entry.Name, entry.Stream, true);
							if (nugetAsm != null)
							{
								if (loadedAssemblies != null)
									loadedAssemblies.Add(nugetAsm);
								else
								{
									var node = assemblyListTreeNode.FindAssemblyNode(nugetAsm);
									if (node != null && focusNode)
									{
										lastNode = node;
									}
								}
							}
						}
						break;
					default:
						var asm = assemblyList.OpenAssembly(file);
						if (asm != null) {
							if (loadedAssemblies != null)
								loadedAssemblies.Add(asm);
							else {
								var node = assemblyListTreeNode.FindAssemblyNode(asm);
								if (node != null && focusNode) {
									lastNode = node;
								}
							}
						}
						break;
				}

				if (lastNode != null && focusNode)
					treeView.FocusNode(lastNode);
			}

			
			// Select only the last node to avoid multi selection
			if (lastNode != null) {
				treeView.SelectedItem = lastNode;
			}
		}
		
		void RefreshCommandExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			try
			{
				refreshInProgress = true;
				var path = GetPathForNode(treeView.SelectedItem as SharpTreeNode);
				ShowAssemblyList(assemblyListManager.LoadList(ILSpySettings.Load(), assemblyList.ListName));
				SelectNode(FindNodeByPath(path, true));
			} finally {
				refreshInProgress = false;
			}

		}
		
		void SearchCommandExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			SearchPane.Instance.Show();
		}
		#endregion
		
		#region Decompile (TreeView_SelectionChanged)
		void TreeView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			DecompileSelectedNodes();
			CommandManager.InvalidateRequerySuggested();

			if (SelectionChanged != null)
				SelectionChanged(sender, e);
		}

		Task decompilationTask;
		bool ignoreDecompilationRequests;

		void DecompileSelectedNodes(DecompilerTextViewState state = null, bool recordHistory = true)
		{
			if (ignoreDecompilationRequests)
				return;

			if (treeView.SelectedItems.Count == 0 && refreshInProgress)
				return;

			if (decompilerTextView == null)
				return;

			if (recordHistory) {
				var dtState = decompilerTextView.GetState();
				if(dtState != null)
					history.UpdateCurrent(new NavigationState(dtState));
				history.Record(new NavigationState(treeView.SelectedItems.OfType<SharpTreeNode>()));
				CommandManager.InvalidateRequerySuggested();
			}
			
			if (treeView.SelectedItems.Count == 1) {
				ILSpyTreeNode node = treeView.SelectedItem as ILSpyTreeNode;
				if (node != null && node.View(decompilerTextView))
					return;
			}
			decompilationTask = decompilerTextView.DecompileAsync(this.CurrentLanguage, this.SelectedNodes, new DecompilationOptions() { TextViewState = state });
		}

		void SaveCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.Handled = true;
			e.CanExecute = SaveCodeContextMenuEntry.CanExecute(SelectedNodes.ToList());
		}

		void SaveCommandExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			SaveCodeContextMenuEntry.Execute(SelectedNodes.ToList());
		}

		public void RefreshDecompiledView()
		{
			RefreshDecompiledView(recordHistory: true);
		}

		void RefreshDecompiledView(bool recordHistory)
		{
			try
			{
				refreshInProgress = true;
				DecompileSelectedNodes(recordHistory: recordHistory);
			} finally {
				refreshInProgress = false;
			}
		}
		
		public DecompilerTextView TextView {
			get { return decompilerTextView; }
		}

		public Language CurrentLanguage => sessionSettings.FilterSettings.Language;
		public LanguageVersion CurrentLanguageVersion => sessionSettings.FilterSettings.LanguageVersion;

		public event EventHandler<SelectionChangedEventArgs> SelectionChanged;

		public IEnumerable<ILSpyTreeNode> SelectedNodes {
			get {
				return treeView.GetTopLevelSelection().OfType<ILSpyTreeNode>();
			}
		}
		#endregion

		#region Back/Forward navigation
		void BackCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.Handled = true;
			e.CanExecute = history.CanNavigateBack;
		}

		void BackCommandExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			if (history.CanNavigateBack) {
				e.Handled = true;
				NavigateHistory(false);
			}
		}

		void ForwardCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.Handled = true;
			e.CanExecute = history.CanNavigateForward;
		}

		void ForwardCommandExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			if (history.CanNavigateForward) {
				e.Handled = true;
				NavigateHistory(true);
			}
		}

		void NavigateHistory(bool forward)
		{
			var dtState = decompilerTextView.GetState();
			if (dtState != null)
				history.UpdateCurrent(new NavigationState(dtState));
			var newState = forward ? history.GoForward() : history.GoBack();

			ignoreDecompilationRequests = true;
			try {
				treeView.SetSelectedNodes(newState.TreeNodes);
				if (newState.TreeNodes.Any())
					treeView.FocusNode(newState.TreeNodes.First());
			} finally {
				ignoreDecompilationRequests = false;
			}
			DecompileSelectedNodes(newState.ViewState, false);
			CommandManager.InvalidateRequerySuggested();
		}

		#endregion

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			// store window state in settings only if it's not minimized
			if (change.Property == WindowStateProperty && this.WindowState != WindowState.Minimized && sessionSettings != null)
				sessionSettings.WindowState = this.WindowState;
		}

		protected override void OnClosing(WindowClosingEventArgs e)
		{
			base.OnClosing(e);
			if (e.Cancel)
				return;
			if (assemblyList != null)
				sessionSettings.ActiveAssemblyList = assemblyList.ListName;
			sessionSettings.ActiveTreeViewPath = GetPathForNode(treeView.SelectedItem as SharpTreeNode);
			sessionSettings.ActiveAutoLoadedAssembly = GetAutoLoadedAssemblyNode(treeView.SelectedItem as SharpTreeNode);
			if (WindowState == WindowState.Normal)
				sessionSettings.WindowBounds = new Rect(Position.ToPoint(DesktopScaling), ClientSize);
			sessionSettings.SplitterPosition = GetRatio(leftColumn.ActualWidth, rightColumn.ActualWidth, sessionSettings.SplitterPosition);
			if (topPane.IsVisible == true)
				sessionSettings.TopPaneSplitterPosition = GetRatio(topPaneRow.ActualHeight, textViewRow.ActualHeight, sessionSettings.TopPaneSplitterPosition);
			if (bottomPane.IsVisible == true)
				sessionSettings.BottomPaneSplitterPosition = GetRatio(bottomPaneRow.ActualHeight, textViewRow.ActualHeight, sessionSettings.BottomPaneSplitterPosition);
			sessionSettings.Save();
		}

		static double GetRatio(double first, double second, double fallback)
		{
			double total = first + second;
			return total > 0 ? first / total : fallback;
		}

		private string GetAutoLoadedAssemblyNode(SharpTreeNode node)
		{
			if (node == null)
				return null;
			while (!(node is TreeNodes.AssemblyTreeNode) && node.Parent != null) {
				node = node.Parent;
			}
			//this should be an assembly node
			var assyNode = node as TreeNodes.AssemblyTreeNode;
			if (assyNode == null)
				return null;
			var loadedAssy = assyNode.LoadedAssembly;
			if (!(loadedAssy.IsLoaded && loadedAssy.IsAutoLoaded))
				return null;

			return loadedAssy.FileName;
		}

		#region Top/Bottom Pane management

		/// <summary>
		///   When grid is resized using splitter, row height value could become greater than 1.
		///   As result, when a new pane is shown, both textView and pane could become very small.
		///   This method normalizes two rows and ensures that height is less then 1.
		/// </summary>
		void NormalizePaneRowHeightValues(RowDefinition pane1Row, RowDefinition pane2Row)
		{
			var pane1Height = pane1Row.Height;
			var pane2Height = pane2Row.Height;

			//only star height values are normalized.
			if (!pane1Height.IsStar || !pane2Height.IsStar) {
				return;
			}

			var totalHeight = pane1Height.Value + pane2Height.Value;
			if (totalHeight == 0) {
				return;
			}

			pane1Row.Height = new GridLength(pane1Height.Value / totalHeight, GridUnitType.Star);
			pane2Row.Height = new GridLength(pane2Height.Value / totalHeight, GridUnitType.Star);
		}

		public void ShowInTopPane(string title, object content)
		{
			topPaneRow.MinHeight = 100;
			if (sessionSettings.TopPaneSplitterPosition > 0 && sessionSettings.TopPaneSplitterPosition < 1) {
				//Ensure all 3 blocks are in fair conditions
				NormalizePaneRowHeightValues(bottomPaneRow, textViewRow);

				textViewRow.Height = new GridLength(1 - sessionSettings.TopPaneSplitterPosition, GridUnitType.Star);
				topPaneRow.Height = new GridLength(sessionSettings.TopPaneSplitterPosition, GridUnitType.Star);
			}
			topPane.Title = title;
			if (topPane.Content != content) {
				IPane pane = topPane.Content as IPane;
				if (pane != null)
					pane.Closed();
				topPane.Content = content;
			}
			topPane.IsVisible = true;
		}
		
		void TopPane_CloseButtonClicked(object sender, EventArgs e)
		{
			sessionSettings.TopPaneSplitterPosition = GetRatio(topPaneRow.ActualHeight, textViewRow.ActualHeight, sessionSettings.TopPaneSplitterPosition);
			topPaneRow.MinHeight = 0;
			topPaneRow.Height = new GridLength(0);
			topPane.IsVisible = false;
			
			IPane pane = topPane.Content as IPane;
			topPane.Content = null;
			if (pane != null)
				pane.Closed();
		}
		
		public void ShowInBottomPane(string title, object content)
		{
			bottomPaneRow.MinHeight = 100;
			if (sessionSettings.BottomPaneSplitterPosition > 0 && sessionSettings.BottomPaneSplitterPosition < 1) {
				//Ensure all 3 blocks are in fair conditions
				NormalizePaneRowHeightValues(topPaneRow, textViewRow);

				textViewRow.Height = new GridLength(1 - sessionSettings.BottomPaneSplitterPosition, GridUnitType.Star);
				bottomPaneRow.Height = new GridLength(sessionSettings.BottomPaneSplitterPosition, GridUnitType.Star);
			}
			bottomPane.Title = title;
			if (bottomPane.Content != content) {
				IPane pane = bottomPane.Content as IPane;
				if (pane != null)
					pane.Closed();
				bottomPane.Content = content;
			}
			bottomPane.IsVisible = true;
		}
		
		void BottomPane_CloseButtonClicked(object sender, EventArgs e)
		{
			sessionSettings.BottomPaneSplitterPosition = GetRatio(bottomPaneRow.ActualHeight, textViewRow.ActualHeight, sessionSettings.BottomPaneSplitterPosition);
			bottomPaneRow.MinHeight = 0;
			bottomPaneRow.Height = new GridLength(0);
			bottomPane.IsVisible = false;
			
			IPane pane = bottomPane.Content as IPane;
			bottomPane.Content = null;
			if (pane != null)
				pane.Closed();
		}
		#endregion
		
		public void UnselectAll()
		{
			treeView.UnselectAll();
		}
		
		public void SetStatus(string status, IBrush foreground)
		{
			if (this.statusBar.IsVisible == false)
				this.statusBar.IsVisible = true;
			this.StatusLabel.Foreground = foreground;
			this.StatusLabel.Text = status;
		}
		
		public IEnumerable GetMainMenuItems()
		{
			return mainMenu.Items;
		}
		
		public IEnumerable GetToolBarItems()
		{
			return toolBar.Children;
		}
	}
}
