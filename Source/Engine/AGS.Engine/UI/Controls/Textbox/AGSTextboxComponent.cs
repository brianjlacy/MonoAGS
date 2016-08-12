﻿using AGS.API;
using System.Diagnostics;

namespace AGS.Engine
{
    public class AGSTextBoxComponent : AGSComponent, ITextBoxComponent
    {
        private bool _isFocused;
        private ITextComponent _textComponent;
        private IUIEvents _uiEvents;
        private IAnimationContainer _obj;
        private IDrawableInfo _drawable;
        private IInObjectTree _tree;
        private IHasRoom _room;
        private readonly IGame _game;
        private static readonly PointF _noScaling = new PointF(1f, 1f);
        private float _spaceWidth;
        private int _caretFlashCounter;

        private int _endOfLine { get { return _textComponent.Text.Length; } }
        private bool _capslock {  get { return Hooks.KeyboardState.CapslockOn; } }
        private bool _leftShiftOn, _rightShiftOn;
        private bool _shiftOn { get { return _leftShiftOn || _rightShiftOn; } }

        private ILabel _caretMeasure, _caret;

        public AGSTextBoxComponent(IEvent<AGSEventArgs> onFocusChanged, IEvent<TextBoxKeyPressingEventArgs> onPressingKey,
            IInput input, IGame game)
        {
            CaretOffset = -7f;
            CaretFlashDelay = 10;
            OnFocusChanged = onFocusChanged;
            OnPressingKey = onPressingKey;
            _game = game;
            
            input.KeyDown.Subscribe(onKeyDown);
            input.KeyUp.Subscribe(onKeyUp);                        
        }

        public override void Init(IEntity entity)
        {
            base.Init(entity);
            _textComponent = entity.GetComponent<ITextComponent>();
            _uiEvents = entity.GetComponent<IUIEvents>();
            _obj = entity.GetComponent<IAnimationContainer>();
            _drawable = entity.GetComponent<IDrawableInfo>();
            _tree = entity.GetComponent<IInObjectTree>();
            _room = entity.GetComponent<IHasRoom>();

            _caretFlashCounter = (int)CaretFlashDelay;
            _spaceWidth = measureSpace();
            _caret = _game.Factory.UI.GetLabel(entity.ID + " Caret", "|", 1f, 1f, 0f, 0f, new AGSTextConfig(autoFit: AutoFit.LabelShouldFitText));
            _caretMeasure = _game.Factory.UI.GetLabel(entity.ID + " Caret Measure", "", 1f, 1f, 0f, 0f, addToUi: false);
            _caretMeasure.TreeNode.SetParent(_tree.TreeNode);
            _caret.Anchor = new PointF(0f, 0f);
            ILabelRenderer labelRenderer = _caretMeasure.CustomRenderer as ILabelRenderer;
            if (labelRenderer != null) labelRenderer.CalculationOnly = true;

            _game.Events.OnBeforeRender.Subscribe(onBeforeRender);
            _uiEvents.MouseDown.Subscribe(onMouseDown);
            _uiEvents.LostFocus.Subscribe(onMouseDownOutside);
        }

        public bool IsFocused
        {
            get { return _isFocused; }
            set
            {
                if (_isFocused == value) return;
                _isFocused = value;
                OnFocusChanged.Invoke(this, new AGSEventArgs());
            }
        }

        public int CaretPosition { get; set; }
        public float CaretOffset { get; set; }        
        public uint CaretFlashDelay { get; set; }

        public IEvent<AGSEventArgs> OnFocusChanged { get; private set; }

        public IEvent<TextBoxKeyPressingEventArgs> OnPressingKey { get; private set; }

        private void onMouseDown(object sender, MouseButtonEventArgs args)
        {
            Debug.WriteLine("{0} is focused", _textComponent.Text);
            IsFocused = true;
        }

        private void onMouseDownOutside(object sender, MouseButtonEventArgs args)
        {
            IsFocused = false;
        }

        private void onBeforeRender(object sender, AGSEventArgs args)
        {
            if (_room.Room != null && _room.Room != _game.State.Player.Character.Room) return;
            if (_caret.TreeNode.Parent == null) _caret.TreeNode.SetParent(_tree.TreeNode);
            bool isVisible = IsFocused;
            if (isVisible)
            {
                _caretFlashCounter--;
                if (_caretFlashCounter < 0)
                {
                    isVisible = false;
                    if (_caretFlashCounter < -CaretFlashDelay)
                    {
                        _caretFlashCounter = (int)CaretFlashDelay;
                    }                    
                }
            }
            _caret.Visible = isVisible;
            if (!isVisible) return;

            _caretMeasure.LabelRenderSize = _textComponent.LabelRenderSize;
            _caretMeasure.TextConfig = _textComponent.TextConfig;
            _caretMeasure.Text = _textComponent.Text.Substring(0, CaretPosition);//.Replace(' ', '_'); 

            //spaces are not measured in end of line for some reason
            float spaceOffset = 0f;            
            if (_caretMeasure.Text.EndsWith(" ")) spaceOffset = _spaceWidth * (_caretMeasure.Text.Length - _caretMeasure.Text.TrimEnd().Length);

            _caretMeasure.CustomRenderer.Prepare(_obj, _drawable, _tree, _game.State.Player.Character.Room.Viewport, getAreaScaling());
            _caret.X = _caretMeasure.TextWidth + CaretOffset + spaceOffset;
        }

        private float measureSpace()
        {
            //hack to measure the size of spaces. For some reason MeasureString returns bad results when string ends with a space.
            IFont font = (_textComponent.TextConfig ?? new AGSTextConfig()).Font;
            return font.MeasureString(" a").Width - font.MeasureString("a").Width;
        }

        private PointF getAreaScaling()
        {
            if (_drawable.IgnoreScalingArea) return _noScaling;
            foreach (var area in _room.Room.ScalingAreas)
            {
                if (!area.IsInArea(_obj.Location.XY)) continue;
                float scaling = area.GetScaling(_obj.Y);
                return new PointF(scaling, scaling);
            }
            return _noScaling;
        }

        private void onKeyUp(object sender, KeyboardEventArgs args)
        {
            if (args.Key == Key.ShiftLeft) { _leftShiftOn = false; return; }
            if (args.Key == Key.ShiftRight) { _rightShiftOn = false; return; }
        }

        private void onKeyDown(object sender, KeyboardEventArgs args)
        {
            if (args.Key == Key.ShiftLeft) { _leftShiftOn = true; return; }
            if (args.Key == Key.ShiftRight) { _rightShiftOn = true; return; }

            if (!IsFocused) return;
            TextBoxKeyPressingEventArgs pressingArgs = new TextBoxKeyPressingEventArgs(args.Key);
            OnPressingKey.Invoke(this, pressingArgs);
            if (pressingArgs.ShouldCancel) return;

            processKey(args.Key);
        }

        private void processKey(Key key)
        {
            switch (key)
            {
                case Key.Home: CaretPosition = 0; break;
                case Key.End: CaretPosition = _endOfLine; break;
                case Key.Left: CaretPosition = CaretPosition > 0 ? CaretPosition - 1 : 0; break;
                case Key.Right: CaretPosition = CaretPosition < _endOfLine ? CaretPosition + 1 : _endOfLine; break;
                case Key.BackSpace: processBackspace(); break;
                case Key.Delete: processDelete(); break;

                case Key.Tilde: addCharacter(_shiftOn ? '~' : '`'); break;
                case Key.Minus: addCharacter(_shiftOn ? '_' : '-'); break;
                case Key.Plus: addCharacter(_shiftOn ? '+' : '='); break;
                case Key.BracketLeft: addCharacter(_shiftOn ? '{' : '['); break;
                case Key.BracketRight: addCharacter(_shiftOn ? '}' : ']'); break;
                case Key.BackSlash: case Key.NonUSBackSlash: addCharacter(_shiftOn ? '|' : '\\'); break;
                case Key.Semicolon: addCharacter(_shiftOn ? ':' : ';'); break;
                case Key.Quote: addCharacter(_shiftOn ? '"' : '\''); break;
                case Key.Comma: addCharacter(_shiftOn ? '<' : ','); break;
                case Key.Period: addCharacter(_shiftOn ? '>' : '.'); break;
                case Key.Slash: addCharacter(_shiftOn ? '?' : '/'); break;
                case Key.KeypadAdd: addCharacter('+'); break;
                case Key.KeypadPeriod: addCharacter('.'); break;
                case Key.KeypadDivide: addCharacter('/'); break;
                case Key.KeypadMinus: addCharacter('-'); break;
                case Key.KeypadMultiply: addCharacter('*'); break;
                case Key.Space: addCharacter(' '); break;
                default:
                    if (key >= Key.A && key <= Key.Z)
                    {
                        processLetter(key);
                    }
                    if (key >= Key.Number0 && key <= Key.Number9)
                    {
                        processDigit((char)((int)'0' + (key - Key.Number0)));
                    }
                    if (key >= Key.Keypad0 && key <= Key.Keypad9)
                    {
                        addCharacter((char)((int)'0' + (key - Key.Keypad0)));
                    }
                    break;
            }
        }

        private void processDigit(char c)
        {
            if (!_shiftOn)
            {
                addCharacter(c);
                return;
            }
            switch (c)
            {
                case '1': addCharacter('!'); break;
                case '2': addCharacter('@'); break;
                case '3': addCharacter('#'); break;
                case '4': addCharacter('$'); break;
                case '5': addCharacter('%');  break;
                case '6': addCharacter('^'); break;
                case '7': addCharacter('&'); break;
                case '8': addCharacter('*'); break;
                case '9': addCharacter('('); break;
                case '0': addCharacter(')'); break;
            }
        }

        private void processLetter(Key key)
        {
            char c = (char)((int)'a' + (key - Key.A));
            if (_capslock || _shiftOn) c = char.ToUpperInvariant(c);
            addCharacter(c);
        }

        private void addCharacter(char c)
        {
            string text = _textComponent.Text;
            _textComponent.Text = string.Format("{0}{1}{2}", text.Substring(0, CaretPosition), c, text.Substring(CaretPosition));
            CaretPosition++;
        }

        private void processBackspace()
        {
            if (CaretPosition == 0) return;            
            string text = _textComponent.Text;
            _textComponent.Text = string.Format("{0}{1}", text.Substring(0, CaretPosition - 1), text.Substring(CaretPosition));
            CaretPosition--;
        }

        private void processDelete()
        {
            if (CaretPosition == _endOfLine) return;
            string text = _textComponent.Text;
            _textComponent.Text = string.Format("{0}{1}", text.Substring(0, CaretPosition), text.Substring(CaretPosition + 1));            
        }
    }
}