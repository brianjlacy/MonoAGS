﻿using System.ComponentModel;
using AGS.API;
using GuiLabs.Undo;

namespace AGS.Editor
{
    public class InspectorPanel
    {
		private readonly IRenderLayer _layer;
		private readonly AGSEditor _editor;
        private readonly ActionManager _actions;
        private IPanel _treePanel, _scrollingPanel, _contentsPanel, _parent;
        private ITextBox _searchBox;
        private InspectorTreeNodeProvider _inspectorNodeView;
        private readonly string _idPrefix;

        const float _padding = 28f;
        const float _gutterSize = 15f;

        public InspectorPanel(AGSEditor editor, IRenderLayer layer, ActionManager actions, string idPrefix = "")
        {
            _idPrefix = idPrefix;
            _editor = editor;
            _actions = actions;
            _layer = layer;
        }

        public AGSInspector Inspector { get; private set; }

        public IPanel Panel => _scrollingPanel;

        public void Load(IPanel parent, IForm parentForm)
        {
            _parent = parent;
            var factory = _editor.Editor.Factory;

            _searchBox = factory.UI.GetTextBox($"{_idPrefix}_InspectorSearchBox", 0f, parent.Height, parent, "Search...", width: parent.Width, height: 30f);
            _searchBox.RenderLayer = _layer;
            _searchBox.Border = factory.Graphics.Borders.SolidColor(GameViewColors.Border, 2f);
            _searchBox.Tint = GameViewColors.Textbox;
            _searchBox.Pivot = new PointF(0f, 1f);
            _searchBox.GetComponent<ITextComponent>().PropertyChanged += onSearchPropertyChanged;

            var height = parent.Height - _searchBox.Height - _gutterSize;
            _scrollingPanel = factory.UI.GetPanel($"{_idPrefix}_InspectorScrollingPanel", parent.Width - _gutterSize, height, 0f, parent.Height - _searchBox.Height, parent);
			_scrollingPanel.RenderLayer = _layer;
			_scrollingPanel.Pivot = new PointF(0f, 1f);
			_scrollingPanel.Tint = Colors.Transparent;
            _scrollingPanel.Border = factory.Graphics.Borders.SolidColor(GameViewColors.Border, 2f);
            _contentsPanel = factory.UI.CreateScrollingPanel(_scrollingPanel);

            _treePanel = factory.UI.GetPanel($"{_idPrefix}_InspectorPanel", 0f, 0f, 0f, _contentsPanel.Height - _padding, _contentsPanel);
			_treePanel.Tint = Colors.Transparent;
            _treePanel.RenderLayer = _layer;
            _treePanel.Pivot = new PointF(0f, 1f);
			var treeView = _treePanel.AddComponent<ITreeViewComponent>();
            treeView.SkipRenderingRoot = true;

            Inspector = new AGSInspector(_editor.Editor.Factory, _editor.Game.Settings, _editor.Editor.Settings, _actions, _editor.Project.Model, _editor, parentForm);
            _treePanel.AddComponent<IInspectorComponent>(Inspector);
            Inspector.ScrollingContainer = _contentsPanel;

            _inspectorNodeView = new InspectorTreeNodeProvider(treeView.NodeViewProvider,
                                                               _editor.Editor.Events, _treePanel);
            _inspectorNodeView.Resize(_contentsPanel.Width);
            treeView.NodeViewProvider = _inspectorNodeView;

            _parent.Bind<IScaleComponent>(c => c.PropertyChanged += onParentPanelScaleChanged,
                                          c => c.PropertyChanged -= onParentPanelScaleChanged);
        }

		public void Resize()
		{
            _contentsPanel.BaseSize = new SizeF(_parent.Width - _gutterSize, _contentsPanel.Height);
            _searchBox.LabelRenderSize = new SizeF(_parent.Width, _searchBox.Height);
            _searchBox.Watermark.LabelRenderSize = new SizeF(_parent.Width, _searchBox.Height);
            _inspectorNodeView.Resize(_contentsPanel.Width);
		}

        public bool Show(object obj)
        {
            var cropChildren = _contentsPanel.GetComponent<ICropChildrenComponent>();
            cropChildren.CropChildrenEnabled = false;
            var scroll = _contentsPanel.GetComponent<IScrollingComponent>();
            scroll.VerticalScrollBar.Slider.Value = 0f;
            scroll.HorizontalScrollBar.Slider.Value = 0f;
            var result = Inspector.Show(obj);
            cropChildren.CropChildrenEnabled = true;
            return result;
        }

        private void onParentPanelScaleChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(IScaleComponent.Height)) return;

            _contentsPanel.BaseSize = new SizeF(_contentsPanel.Width, _parent.Height - _searchBox.Height - _gutterSize);
            _scrollingPanel.Y = _parent.Height - _searchBox.Height;
            _searchBox.Y = _parent.Height;
            _treePanel.Y = _contentsPanel.Height - _padding;
        }

        private void onSearchPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ITextComponent.Text)) return;
            Inspector.Tree.SearchFilter = _searchBox.Text;
        }
    }
}
