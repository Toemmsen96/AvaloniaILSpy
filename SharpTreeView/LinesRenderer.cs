// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using System.Diagnostics;

namespace ICSharpCode.TreeView
{
	class LinesRenderer : Control
	{
		static readonly IPen pen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(0x60, 0x80, 0x80, 0x80)), 1);

		SharpTreeNodeView NodeView
		{
			get { return TemplatedParent as SharpTreeNodeView; }
		}

		public override void Render(DrawingContext dc)
		{
			if (NodeView?.Node == null || NodeView.ParentItem?.ParentTreeView == null) {
				// This seems to happen sometimes with DataContext==DisconnectedItem,
				// though I'm not sure why WPF would call OnRender() on a disconnected node
				Debug.WriteLine($"LinesRenderer.OnRender() called with DataContext={NodeView.DataContext}");
				return;
			}

			var indent = NodeView.CalculateIndent();
			var p = new Point(indent + 6, 0);

			if (!NodeView.Node.IsRoot || NodeView.ParentTreeView.ShowRootExpander) {
				dc.DrawLine(pen, new Point(p.X, Bounds.Height / 2), new Point(p.X + 10, Bounds.Height / 2));
			}

			if (NodeView.Node.IsRoot) return;

			if (NodeView.Node.IsLast) {
				dc.DrawLine(pen, p, new Point(p.X, Bounds.Height / 2));
			}
			else {
				dc.DrawLine(pen, p, new Point(p.X, Bounds.Height));
			}

			var current = NodeView.Node;
			while (true) {
				p = p.WithX(p.X - 19);
				current = current.Parent;
				if (p.X < 0) break;
				if (current == null) break;
				if (!current.IsLast) {
					dc.DrawLine(pen, p, new Point(p.X, Bounds.Height));
				}
			}
		}
	}
}
