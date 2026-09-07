// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Files.App.UserControls.FilePreviews
{
	/// <summary>
	/// Metadata card shown instead of a content preview for items on protected cloud locations.
	/// Binds only to values already known from directory enumeration; nothing here reads the file.
	/// </summary>
	public sealed partial class DeferredPreview : UserControl
	{
		private readonly Action _loadPreview;

		public ListedItem Item { get; }

		public DeferredPreview(ListedItem item, Action loadPreview)
		{
			Item = item;
			_loadPreview = loadPreview;
			InitializeComponent();
		}

		private void LoadPreview_Click(object sender, RoutedEventArgs e)
			=> _loadPreview();
	}
}
