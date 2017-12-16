﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using AGS.API;

namespace AGS.Engine
{
    public class AGSStackLayoutComponent : AGSComponent, IStackLayoutComponent
    {
        private IBoundingBoxWithChildrenComponent _boundingBoxWithChildren;
        private IInObjectTreeComponent _tree;
        private float _absoluteSpacing, _relativeSpacing, _startLocation;
        private LayoutDirection _direction;
        private bool _isPaused;
        private IEntity _entity;
        private int _pendingLayouts;

        public AGSStackLayoutComponent()
        {
            _isPaused = true;
            OnLayoutChanged = new AGSEvent();
            EntitiesToIgnore = new AGSConcurrentHashSet<string>();
            _direction = LayoutDirection.Vertical;
            _relativeSpacing = -1f; //a simple vertical layout top to bottom by default.
        }

        public LayoutDirection Direction { get => _direction; set { _direction = value; adjustLayout(); } }
        public float AbsoluteSpacing { get => _absoluteSpacing; set { _absoluteSpacing = value; adjustLayout(); } }
        public float RelativeSpacing { get => _relativeSpacing; set { _relativeSpacing = value; adjustLayout(); } }
        public float StartLocation { get { return _startLocation; } set { _startLocation = value; adjustLayout(); } }
        public IBlockingEvent OnLayoutChanged { get; }
        public IConcurrentHashSet<string> EntitiesToIgnore { get; }

        public override void Init(IEntity entity)
        {
            _entity = entity;
            base.Init(entity);
            entity.Bind<IBoundingBoxWithChildrenComponent>(c => { _boundingBoxWithChildren = c; c.OnBoundingBoxWithChildrenChanged.Subscribe(onSizeChanged); adjustLayout(); }, 
                                                    c => { c.OnBoundingBoxWithChildrenChanged.Unsubscribe(onSizeChanged); _boundingBoxWithChildren = null; });
            entity.Bind<IInObjectTreeComponent>(c => { _tree = c; c.TreeNode.Children.OnListChanged.Subscribe(onChildrenChanged); adjustLayout(); },
                                       c => { _tree = null; c.TreeNode.Children.OnListChanged.Unsubscribe(onChildrenChanged); });
            EntitiesToIgnore.OnListChanged.Subscribe(onEntitiesToIgnoreChanged);
        }

        public void StartLayout()
        {
            _isPaused = false;
            adjustLayout();
        }

        public void StopLayout()
        {
            _isPaused = true;
        }

        private void onEntitiesToIgnoreChanged(AGSHashSetChangedEventArgs<string> args)
        {
            adjustLayout();
        }

        private void onChildrenChanged(AGSListChangedEventArgs<IObject> args)
        {
            adjustLayout();
        }

        private void onSizeChanged()
        {
            adjustLayout();
        }

        private void adjustLayout()
        {
            if (_isPaused) return;

            var tree = _tree;
            if (tree == null) return;

            int pendingLayouts = Interlocked.Increment(ref _pendingLayouts);
            if (pendingLayouts > 1)
            {
                return;
            }

            adjustLayout(tree);
        }

        private void adjustLayout(IInObjectTreeComponent tree)
        {
            try
            {
                float location = StartLocation;
                var lockStep = new TreeLockStep(tree, obj => obj.UnderlyingVisible && !EntitiesToIgnore.Contains(obj.ID));
                lockStep.Lock();
                foreach (var child in tree.TreeNode.Children)
                {
                    if (!child.UnderlyingVisible || EntitiesToIgnore.Contains(child.ID)) continue;
                    float step;
                    if (Direction == LayoutDirection.Vertical)
                    {
                        child.Y = location;
                        step = child.AddComponent<IBoundingBoxWithChildrenComponent>().PreCropBoundingBoxWithChildren.Height;
                    }
                    else
                    {
                        child.X = location;
                        step = child.AddComponent<IBoundingBoxWithChildrenComponent>().PreCropBoundingBoxWithChildren.Width;
                    }
                    location += step * RelativeSpacing + AbsoluteSpacing;
                }
                lockStep.Unlock();
            }
            finally
            {
                if (Interlocked.Exchange(ref _pendingLayouts, 0) <= 1)
                {
                    OnLayoutChanged.Invoke();
                }
                else
                {
                    adjustLayout(tree);
                }
            }
        }
	}
}