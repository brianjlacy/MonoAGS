﻿using System;
using System.Collections.Generic;
using System.Linq;
using AGS.API;
using PropertyChanged;

namespace AGS.Engine
{
    public class AGSListboxComponent : AGSComponent, IListboxComponent
    {
        private AGSBindingList<IStringItem> _items;
        private List<IButton> _itemButtons;
        private IUIFactory _uiFactory;
        private int _selectedIndex;
        private float _minHeight, _maxHeight;
        private IScaleComponent _scale;
        private IInObjectTreeComponent _tree;
        private IImageComponent _image;
        private IGameState _state;
        private IStackLayoutComponent _layout;

        public AGSListboxComponent(IUIFactory factory, IGameState state)
        {
            _state = state;
            _uiFactory = factory;
            _itemButtons = new List<IButton>();
            _items = new AGSBindingList<IStringItem>(10);
            _items.OnListChanged.Subscribe(onListChanged);
            _selectedIndex = -1;
            _maxHeight = float.MaxValue;
            OnSelectedItemChanged = new AGSEvent<ListboxItemArgs>();
        }

        public override void Init(IEntity entity)
        {
            base.Init(entity);
            entity.Bind<IScaleComponent>(c => _scale = c, _ => _scale = null);
            entity.Bind<IInObjectTreeComponent>(c => _tree = c, _ => _tree = null);
            entity.Bind<IImageComponent>(c => _image = c, _ => _image = null);
            entity.Bind<IStackLayoutComponent>(c => 
            { 
                c.RelativeSpacing = -1f; 
                c.OnLayoutChanged.Subscribe(onLayoutChanged); 
                c.StartLayout();
                _layout = c;
            }, c => 
            {
                c.StopLayout();
                c.OnLayoutChanged.Unsubscribe(onLayoutChanged);
                _layout = null;
            });
        }

        public Func<string, IButton> ItemButtonFactory { get; set; }

        public IEnumerable<IButton> ItemButtons => _itemButtons;

        public IAGSBindingList<IStringItem> Items => _items;

        [DoNotCheckEquality] //we skip equality checks as we want a combo box drop down to close even when we select the already selected item
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                _selectedIndex = value;
                if (value >= 0 && value < Items.Count)
                {
                    var selectedItem = Items[value];
                    OnSelectedItemChanged.Invoke(new ListboxItemArgs(selectedItem, value));
                }
                else OnSelectedItemChanged.Invoke(new ListboxItemArgs(null, value));    
            }
        }

        public IStringItem SelectedItem
        {
            get
            {
                try
                {
                    if (SelectedIndex < 0) return null;
                    return Items[SelectedIndex];
                }
                catch (IndexOutOfRangeException)
                {
                    return null;
                }
                catch (ArgumentOutOfRangeException)
                {
                    return null;
                }
            }
        }

        public float MinHeight 
        {
            get => _minHeight;
            set 
            {
                if (MathUtils.FloatEquals(_minHeight, value)) return;
                _minHeight = value;
                refreshItemsLayout();
            }
        }

        public float MaxHeight
        {
            get => _maxHeight;
            set
            {
                if (MathUtils.FloatEquals(_maxHeight, value)) return;
                _maxHeight = value;
                refreshItemsLayout();
            }
        }

        public IBlockingEvent<ListboxItemArgs> OnSelectedItemChanged { get; }

        private void onListChanged(AGSListChangedEventArgs<IStringItem> args)
        {
            var tree = _tree;
            if (args.ChangeType == ListChangeType.Remove)
            {
                var items = args.Items.OrderByDescending(i => i.Index);
                var buttons = new List<IButton>(_itemButtons);
                foreach (var item in items)
                {
                    var button = _itemButtons[item.Index];
                    button.MouseClicked.Unsubscribe(onItemClicked);
                    tree?.TreeNode.RemoveChild(button);
                    _state.UI.Remove(button);
                    buttons.RemoveAt(item.Index);
                }
                _itemButtons = buttons;
            }
            else
            {
                var items = args.Items.OrderBy(i => i.Index);
                var newButtons = new List<IObject>(10);
                var buttons = new List<IButton>(_itemButtons);
                foreach (var item in items)
                {
                    string buttonText = item.Item.Text;
                    var newButton = ItemButtonFactory(buttonText);
                    newButton.Text = buttonText;
                    newButton.MouseClicked.Subscribe(onItemClicked);
                    buttons.Insert(item.Index, newButton);
                    newButtons.Add(newButton);
                }
                _itemButtons = buttons;
                tree?.TreeNode.AddChildren(newButtons);
            }
            refreshItemsLayout();
        }

        private void onLayoutChanged()
        {
            refreshItemsLayout();
        }

        private void refreshItemsLayout()
        {
            if (_itemButtons.Count == 0) return;
            var scale = _scale;
            if (scale == null) return;
            scale.BaseSize = new SizeF(_itemButtons.Max(i => Math.Max(i.Width, i.TextWidth)),
                                       MathUtils.Clamp(_itemButtons.Sum(i => Math.Max(i.Height, i.TextHeight)), _minHeight, _maxHeight));
            _layout.StartLocation = scale.Height;
            var image = _image;
            if (image == null) return;
            if (!MathUtils.FloatEquals(_image.Image.Width, scale.BaseSize.Width) ||
                !MathUtils.FloatEquals(_image.Image.Height, scale.BaseSize.Height))
            {
                _image.Image = new EmptyImage(scale.BaseSize.Width, scale.BaseSize.Height);
            }
        }

        private void onItemClicked(MouseButtonEventArgs args)
        {
            SelectedIndex = _itemButtons.IndexOf((IButton)args.ClickedEntity);
        }
    }
}