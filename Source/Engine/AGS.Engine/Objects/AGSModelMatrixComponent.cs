﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using AGS.API;

namespace AGS.Engine
{
    public class AGSModelMatrixComponent : AGSComponent, IModelMatrixComponent, ILockStep
    {
        private bool _isDirty;
        private int _shouldFireOnUnlock, _pendingLocks;
        private ModelMatrices _matrices, _preLockMatrices;

        private IAnimationComponent _animation;
        private IInObjectTreeComponent _tree;
        private IScaleComponent _scale;
        private ITranslateComponent _translate;
        private IRotateComponent _rotate;
        private IImageComponent _image;
        private IHasRoomComponent _room;
        private IDrawableInfoComponent _drawable;
        private IEntity _entity;
        private IObject _parent;
        private ISprite _sprite;
        private IJumpOffsetComponent _jump;

        private readonly Size _virtualResolution;
        private PointF _areaScaling;
        private SizeF? _customImageSize;
        private PointF? _customResolutionFactor;
        private readonly float? _nullFloat = null;

        public AGSModelMatrixComponent(IRuntimeSettings settings)
        {
            _isDirty = true;
            _matrices = new ModelMatrices();
            _virtualResolution = settings.VirtualResolution;
            OnMatrixChanged = new AGSEvent();
        }

        public static readonly PointF NoScaling = new PointF(1f, 1f);

        [Property(Browsable = false)]
        public ILockStep ModelMatrixLockStep { get { return this; } }

        public override void Init(IEntity entity)
        {
            base.Init(entity);
            _entity = entity;
        }

        public override void AfterInit()
        {
            _entity.Bind<IAnimationComponent>(
                c => { _animation = c; onSomethingChanged(); },
                c => { _animation = null; onSomethingChanged(); });
            _entity.Bind<IHasRoomComponent>(
                c => { _room = c; onSomethingChanged(); },
                c => { _room = null; onSomethingChanged(); });
            
            _entity.Bind<IScaleComponent>(
                c => { _scale = c; c.PropertyChanged += onScaleChanged; onSomethingChanged(); },
                c => { c.PropertyChanged -= onScaleChanged; _scale = null; onSomethingChanged();});
            _entity.Bind<ITranslateComponent>(
                c => { _translate = c; c.PropertyChanged += onTranslateChanged; onSomethingChanged(); },
                c => { c.PropertyChanged -= onTranslateChanged; _translate = null; onSomethingChanged();}
            );
            _entity.Bind<IJumpOffsetComponent>(
                c => { _jump = c; c.PropertyChanged += onJumpOffsetChanged; onSomethingChanged();},
                c => { c.PropertyChanged -= onJumpOffsetChanged; _jump = null; onSomethingChanged();}
            );
            _entity.Bind<IRotateComponent>(
                c => { _rotate = c; c.PropertyChanged += onRotateChanged; onSomethingChanged();},
                c => { c.PropertyChanged -= onRotateChanged; _rotate = null; onSomethingChanged();}
            );
			_entity.Bind<IImageComponent>(
                c => { _image = c; c.PropertyChanged += onPivotChanged; onSomethingChanged(); },
                c => { c.PropertyChanged -= onPivotChanged; _image = null; onSomethingChanged(); }
			);

            _entity.Bind<IDrawableInfoComponent>(
                c => 
            {
                _drawable = c;
                c.PropertyChanged += onDrawableChanged;
                onSomethingChanged();
            },c =>
            {
                c.PropertyChanged -= onDrawableChanged;
                _drawable = null;
				onSomethingChanged();
            });

			_entity.Bind<IInObjectTreeComponent>(
				c =>
			{
				_tree = c;
				_parent = _tree.TreeNode.Parent;
				_tree.TreeNode.OnParentChanged.Subscribe(onParentChanged);
				_parent?.OnMatrixChanged.Subscribe(onSomethingChanged);
				onSomethingChanged();
			}, c =>
			{
				c.TreeNode.OnParentChanged.Unsubscribe(onParentChanged);
				c.TreeNode.Parent?.OnMatrixChanged.Unsubscribe(onSomethingChanged);
				_tree = null;
				_parent = null;
				onSomethingChanged();
			});
        }

        public ref ModelMatrices GetModelMatrices() 
        {
            if (_pendingLocks > 0) return ref _preLockMatrices;
            if (!shouldRecalculate())
            {
                return ref _matrices;
            }
            return ref recalculateMatrices();
        }

        public void Lock()
        {
            _preLockMatrices = _matrices;
            Interlocked.Increment(ref _pendingLocks);
        }

        public void PrepareForUnlock()
        {
            _shouldFireOnUnlock += (_isDirty ? 1 : 0);
            if (!_isDirty) return;
            recalculate();
        }

        public void Unlock()
        {
            if (Interlocked.Decrement(ref _pendingLocks) > 0) return;
            if (Interlocked.Exchange(ref _shouldFireOnUnlock, 0) >= 1)
            {
                OnMatrixChanged.Invoke();
            }
        }

        public IBlockingEvent OnMatrixChanged { get; private set; }

        public static bool GetVirtualResolution(bool flattenLayerResolution, Size virtualResolution, IDrawableInfoComponent drawable, 
                                         PointF? customResolutionFactor, out PointF resolutionFactor, out Size resolution)
        {
            //Priorities for virtual resolution: layer's resolution comes first, if not then the custom resolution (which is the text scaling resolution for text, otherwise null),
            //and if not use the virtual resolution.
            var renderLayer = drawable == null ? null : drawable.RenderLayer;
            var layerResolution = renderLayer == null ? null : renderLayer.IndependentResolution;
            if (layerResolution != null)
            {
                if (flattenLayerResolution)
                {
                    resolutionFactor = NoScaling;
                    resolution = virtualResolution;
                    return false;
                }
                resolution = layerResolution.Value;
                resolutionFactor = new PointF(resolution.Width / (float)virtualResolution.Width, resolution.Height / (float)virtualResolution.Height);
                return layerResolution.Value.Equals(virtualResolution);
            }
            else if (customResolutionFactor != null)
            {
                resolutionFactor = customResolutionFactor.Value;
                resolution = new Size((int)(virtualResolution.Width * customResolutionFactor.Value.X), 
                                      (int)(virtualResolution.Height * customResolutionFactor.Value.Y));
                return customResolutionFactor.Value.Equals(NoScaling);
            }
            else
            {
                resolutionFactor = NoScaling;
                resolution = virtualResolution;
                return true;
            }
        }

        private void onSpriteChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(ISprite.X) && args.PropertyName != nameof(ISprite.Y) &&
                args.PropertyName != nameof(ISprite.ScaleX) && args.PropertyName != nameof(ISprite.ScaleY) &&
                args.PropertyName != nameof(ISprite.Width) && args.PropertyName != nameof(ISprite.Height) &&
                args.PropertyName != nameof(ISprite.Angle) && args.PropertyName != nameof(ISprite.Pivot)) return;
            onSomethingChanged();
        }

        private void onTranslateChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(ITranslateComponent.X) && args.PropertyName != nameof(ITranslateComponent.Y)) return;
            onSomethingChanged();
        }

        private void onScaleChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(IScale.ScaleX) && args.PropertyName != nameof(IScale.ScaleY) &&
                args.PropertyName != nameof(IScale.Width) && args.PropertyName != nameof(IScale.Height)) return;
            onSomethingChanged();
        }

        private void onRotateChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(IRotateComponent.Angle)) return;
            onSomethingChanged();
        }

        private void onJumpOffsetChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(IJumpOffsetComponent.JumpOffset)) return;
            onSomethingChanged();
        }

        private void onPivotChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(IImageComponent.Pivot)) return;
            onSomethingChanged();
        }

        private void onDrawableChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(IDrawableInfoComponent.RenderLayer) &&
                args.PropertyName != nameof(IDrawableInfoComponent.IgnoreScalingArea)) return;
            onSomethingChanged();
        }

        private void onSomethingChanged()
        {
            _isDirty = true;
        }

        private void onParentChanged()
        {
            _parent?.OnMatrixChanged.Unsubscribe(onSomethingChanged);
            _parent = _tree == null ? null : _tree.TreeNode.Parent;
            _parent?.OnMatrixChanged.Subscribe(onSomethingChanged);
            onSomethingChanged();
        }

        private ISprite getSprite()
        {
            return _animation == null || _animation.Animation == null ? null : _animation.Animation.Sprite;
        }

        private void subscribeSprite(ISprite sprite)
        {
            if (sprite == null) return;
            sprite.PropertyChanged += onSpriteChanged;
        }

        private void unsubscribeSprite(ISprite sprite)
        {
            if (sprite == null) return;
            sprite.PropertyChanged -= onSpriteChanged;
        }

        private bool shouldRecalculate() 
        {
            PointF areaScaling = getAreaScaling();
            if (!_areaScaling.Equals(areaScaling)) 
            {
                _areaScaling = areaScaling;
                _isDirty = true;
            }
            var currentSprite = getSprite();
            if (currentSprite != _sprite)
            {
                unsubscribeSprite(_sprite);
                _sprite = currentSprite;
                subscribeSprite(_sprite);
                _isDirty = true;
            }
            var renderer = _image.CustomRenderer;
            if (renderer != null)
            {
                var customImageSize = renderer?.CustomImageSize;
                if ((customImageSize == null && _customImageSize != null) || 
                    (customImageSize != null && _customImageSize == null) ||
                    (customImageSize != null && _customImageSize != null && 
                     !customImageSize.Value.Equals(_customImageSize.Value)))
                {
                    _customImageSize = customImageSize;
                    _isDirty = true;
                }
                var customFactor = renderer?.CustomImageResolutionFactor;
                if ((customFactor == null && _customResolutionFactor != null) ||
                    (customFactor != null && _customResolutionFactor == null) ||
                    (customFactor != null && _customResolutionFactor != null &&
                     !customFactor.Value.Equals(_customResolutionFactor.Value)))
                {
                    _customResolutionFactor = customFactor;
                    _isDirty = true;
                }
            }
            return _isDirty;
        }

        private ref ModelMatrices recalculateMatrices()
        {
            recalculate();
            OnMatrixChanged.Invoke();
            return ref _matrices;
        }

        private void recalculate()
        {
            PointF resolutionFactor;
            Size resolution;
            _isDirty = false;
            bool resolutionMatches = GetVirtualResolution(true, _virtualResolution, _drawable, _customResolutionFactor,
                                                   out resolutionFactor, out resolution);

            var renderMatrix = getMatrix(resolutionFactor);
            var hitTestMatrix = resolutionMatches ? renderMatrix : resolutionFactor.Equals(NoScaling) ? getMatrix(new PointF((float)_virtualResolution.Width/_drawable.RenderLayer.IndependentResolution.Value.Width,
                                                                                                                             (float)_virtualResolution.Height/_drawable.RenderLayer.IndependentResolution.Value.Height)) : getMatrix(NoScaling);
            _matrices.InObjResolutionMatrix = renderMatrix;
            _matrices.InVirtualResolutionMatrix = hitTestMatrix;
        }

        private Matrix4 getMatrix(PointF resolutionFactor) 
        {
            var animation = _animation;
            if (animation == null || animation.Animation == null) return Matrix4.Identity;
            var sprite = animation.Animation.Sprite;
            Matrix4 spriteMatrix = getModelMatrix(sprite, sprite, sprite, sprite, null,
                                                  NoScaling, resolutionFactor, true);
            Matrix4 objMatrix = getModelMatrix(_scale, _rotate, _translate, _image,
                                               _jump, _areaScaling, resolutionFactor, true);

            var modelMatrix = spriteMatrix * objMatrix;
            var parent = _tree == null ? null : _tree.TreeNode.Parent;
            while (parent != null)
            {
                //var parentMatrices = parent.GetModelMatrices(); todo: figure out how to reuse parent matrix instead of recalculating it here (need to change the while to if)
                //Matrix4 parentMatrix = resolutionFactor.Equals(NoScaling) ? parentMatrices.InVirtualResolutionMatrix : parentMatrices.InObjResolutionMatrix;
                Matrix4 parentMatrix = getModelMatrix(parent, parent, parent, parent, parent.GetComponent<IJumpOffsetComponent>(),
                    NoScaling, resolutionFactor, false);
                modelMatrix = modelMatrix * parentMatrix;
                parent = parent.TreeNode.Parent;
            }
            return modelMatrix;
        }

        private Matrix4 getModelMatrix(IScale scale, IRotate rotate, ITranslate translate, IHasImage image,
                                       IJumpOffsetComponent jump, PointF areaScaling, PointF resolutionTransform, bool useCustomImageSize)
        {
            if (scale == null) return Matrix4.Identity;
            float? customWidth = _customImageSize == null || !useCustomImageSize ? 
                _nullFloat : (_customImageSize.Value.Width * scale.ScaleX);
            float? customHeight = _customImageSize == null || !useCustomImageSize ? 
                _nullFloat : (_customImageSize.Value.Height * scale.ScaleY);
            float width = (customWidth ?? scale.Width) * areaScaling.X * resolutionTransform.X;
            float height = (customHeight ?? scale.Height) * areaScaling.Y * resolutionTransform.Y;
            PointF pivotOffsets = getPivotOffsets(image == null ? PointF.Empty : image.Pivot, 
                                                    width, height);
            Matrix4 pivotMat = Matrix4.CreateTranslation(new Vector3(-pivotOffsets.X, -pivotOffsets.Y, 0f));
            Matrix4 scaleMat = Matrix4.CreateScale(new Vector3(scale.ScaleX * areaScaling.X,
                scale.ScaleY * areaScaling.Y, 1f));
            float radians = rotate == null ? 0f : MathUtils.DegreesToRadians(rotate.Angle);
            Matrix4 rotationMat = Matrix4.CreateRotationZ(radians);
            float x = translate == null ? 0f : translate.X * resolutionTransform.X;
            float y = translate == null ? 0f : translate.Y * resolutionTransform.Y;
            if (jump != null)
            {
                x += jump.JumpOffset.X * resolutionTransform.X;
                y += jump.JumpOffset.Y * resolutionTransform.Y;
            }
            Matrix4 translateMat = Matrix4.CreateTranslation(new Vector3(x, y, 0f));
            return scaleMat * pivotMat * rotationMat * translateMat;
        }

        private PointF getPivotOffsets(PointF pivot, float width, float height)
        {
            float x = MathUtils.Lerp(0f, 0f, 1f, width, pivot.X);
            float y = MathUtils.Lerp(0f, 0f, 1f, height, pivot.Y);
            return new PointF(x, y);
        }

        private PointF getAreaScaling()
        {
            if (_room == null || (_drawable?.IgnoreScalingArea ?? false)) return NoScaling;
            foreach (IArea area in _room.Room.GetMatchingAreas(_translate.Location.XY, _entity.ID))
            {
                IScalingArea scaleArea = area.GetComponent<IScalingArea>();
                if (scaleArea == null || (!scaleArea.ScaleObjectsX && !scaleArea.ScaleObjectsY)) continue;
                float scale = scaleArea.GetScaling(scaleArea.Axis == ScalingAxis.X ? _translate.X : _translate.Y);
                return new PointF(scaleArea.ScaleObjectsX ? scale : 1f, scaleArea.ScaleObjectsY ? scale : 1f);
            }
            return NoScaling;
        }
    }
}