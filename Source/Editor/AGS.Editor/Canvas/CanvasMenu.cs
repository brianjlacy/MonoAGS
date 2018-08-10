﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AGS.API;
using AGS.Engine;
using GuiLabs.Undo;

namespace AGS.Editor
{
    public class CanvasMenu
    {
        private readonly AGSEditor _editor;
        private readonly GameToolbar _toolbar;
        private Menu _topMenu;
        private static int _lastId;
        private ICheckboxComponent _guiButton, _parentButton;
        private IObject _potentialParent;
        private IRadioGroup _targetRadioGroup;

        private enum Target 
        {
            Room,
            UI,
            Area
        }

        public CanvasMenu(AGSEditor editor, GameToolbar toolbar)
        {
            _editor = editor;
            _toolbar = toolbar;
        }

        public void Load()
        {
            Action noop = () => {};
            Menu guisMenu = new Menu(_editor.GameResolver, _editor.Editor.Settings, "GuisMenu", 180f,
                                     new MenuItem("Button", showButtonWizard),
                                     new MenuItem("Label", showLabelWizard),
                                     new MenuItem("ComboBox", showComboboxWizard),
                                     new MenuItem("TextBox", showTextboxWizard),
                                     new MenuItem("Inventory Window", showInventoryWindowWizard),
                                     new MenuItem("Checkbox", showCheckboxWizard),
                                     new MenuItem("Listbox", showListboxWizard),
                                     new MenuItem("Panel", showPanelWizard),
                                     new MenuItem("Slider", showSliderWizard));
            Menu presetsMenu = new Menu(_editor.GameResolver, _editor.Editor.Settings,"PresetsMenu", 100f,
                                        new MenuItem("Object", showObjectWizard),
                                        new MenuItem("Character", showCharacterWizard),
                                        new MenuItem("Area", showAreaWizard),
                                        new MenuItem("GUIs", guisMenu));
            _topMenu = new Menu(_editor.GameResolver, _editor.Editor.Settings, "CanvasMenu", 100f, new MenuItem("Create", presetsMenu));
            _topMenu.Load(_editor.Editor.Factory, _editor.Editor.Settings.Defaults);

            _editor.Editor.Input.MouseUp.Subscribe((MouseButtonEventArgs args) => 
            {
                if (args.Button == MouseButton.Right)
                {
                    if (!_toolbar.IsPaused) return;
                    if (!_editor.IsEditorPositionInGameWindow(args.MousePosition.XMainViewport, args.MousePosition.YMainViewport))
                    {
                        return;
                    }
                    _potentialParent = _editor.Game.HitTest.ObjectAtMousePosition;
                    _topMenu.Position = (args.MousePosition.XMainViewport, args.MousePosition.YMainViewport);
                    _topMenu.Visible = true;
                }
                else if (args.ClickedEntity == null) 
                {
                    _topMenu.Visible = false;
                } 
            });
        }

        private void showButtonWizard() => showWizard("button", Target.UI, _editor.Game.Factory.UI, nameof(IUIFactory.GetButton));
        private void showLabelWizard() => showWizard("label", Target.UI, _editor.Game.Factory.UI, nameof(IUIFactory.GetLabel));
        private void showCheckboxWizard() => showWizard("checkbox", Target.UI, _editor.Game.Factory.UI, nameof(IUIFactory.GetCheckBox));
        private void showComboboxWizard() => showWizard("combobox", Target.UI, _editor.Game.Factory.UI, nameof(IUIFactory.GetComboBox));
        private void showPanelWizard() => showWizard("panel", Target.UI, _editor.Game.Factory.UI, nameof(IUIFactory.GetPanel));
        private void showSliderWizard() => showWizard("slider", Target.UI, _editor.Game.Factory.UI, nameof(IUIFactory.GetSlider));
        private void showTextboxWizard() => showWizard("textbox", Target.UI, _editor.Game.Factory.UI, nameof(IUIFactory.GetTextBox));
        private void showInventoryWindowWizard() => showWizard("invWindow", Target.UI, _editor.Game.Factory.Inventory, nameof(IInventoryFactory.GetInventoryWindow));
        private void showListboxWizard() => showWizard("listbox", Target.UI, _editor.Game.Factory.UI, nameof(IUIFactory.GetListBox));
        private void showObjectWizard() => showWizard("object", Target.Room, _editor.Game.Factory.Object, nameof(IObjectFactory.GetAdventureObject));
        private void showCharacterWizard() => showWizard("character", Target.Room, _editor.Game.Factory.Object, nameof(IObjectFactory.GetCharacter));
        private void showAreaWizard() => showWizard("area", Target.Area, _editor.Game.Factory.Room, nameof(IRoomFactory.GetArea));

        private object get(string key, Dictionary<string, object> parameters) => parameters.TryGetValue(key, out var val) ? val : null;

        private async void showWizard(string name, Target target, object factory, string methodName)
        {
            var (method, methodAttribute) = getMethod(factory, methodName);
            HashSet<string> hideProperties = new HashSet<string>();
            Dictionary<string, object> overrideDefaults = new Dictionary<string, object>();
            foreach (var param in method.GetParameters())
            {
                var attr = param.GetCustomAttribute<MethodParamAttribute>();
                if (attr == null) continue;
                if (!attr.Browsable) hideProperties.Add(param.Name);
                if (attr.DefaultProvider != null)
                {
                    var resolver = _editor.GameResolver;
                    var provider = factory.GetType().GetMethod(attr.DefaultProvider);
                    if (provider == null)
                    {
                        throw new NullReferenceException($"Failed to find method with name: {attr.DefaultProvider ?? "null"}");
                    }
                    overrideDefaults[param.Name] = provider.Invoke(null, new[] { resolver });
                }
                else if (attr.Default != null) overrideDefaults[param.Name] = attr.Default;
            }

            _topMenu.Visible = false;

            var (x, y) = _editor.ToGameResolution(_topMenu.OriginalPosition.x, _topMenu.OriginalPosition.y, null);
            overrideDefaults["x"] = x;
            overrideDefaults["y"] = y;
            overrideDefaults["id"] = $"{name}{++_lastId}";
            var editorProvider = new EditorProvider(_editor.Editor.Factory, new ActionManager(), new StateModel(), _editor.Editor.State, _editor.Editor.Settings);
            var wizard = new MethodWizard(method, hideProperties, overrideDefaults, panel => addTargetUIForCreate(panel, target), editorProvider, _editor);
            wizard.Load();
            var parameters = await wizard.ShowAsync();
            if (parameters == null) return;
            foreach (var param in overrideDefaults.Keys)
            {
                parameters[param] = get(param, parameters) ?? overrideDefaults[param];
            }
            (object result, MethodModel model) = runMethod(method, factory, parameters);
            List<object> entities = getEntities(factory, result, methodAttribute);
            addNewEntities(entities, model);
        }

        private (MethodInfo, MethodWizardAttribute) getMethod(object factory, string methodName)
        {
            foreach (var method in factory.GetType().GetMethods())
            {
                if (method.Name != methodName) continue;
                var attr = method.GetCustomAttribute<MethodWizardAttribute>();
                if (attr == null) continue;
                return (method, attr);
            }
            throw new InvalidOperationException($"Failed to find method name {methodName} in {factory.GetType()}");
        }

        private List<object> getEntities(object factory, object result, MethodWizardAttribute attr)
        {
            if (attr.EntitiesProvider == null) return new List<object>{result};

            var provider = factory.GetType().GetMethod(attr.EntitiesProvider);
            if (provider == null)
            {
                throw new NullReferenceException($"Failed to find entity provider method with name: {attr.EntitiesProvider ?? "null"}");
            }
            return (List<object>)provider.Invoke(null, new[] { result });
        }

        private void addNewEntities(List<object> entities, MethodModel methodModel)
        {
            bool isParent = _potentialParent != null && _targetRadioGroup?.SelectedButton == _parentButton;
            bool isUi = _targetRadioGroup?.SelectedButton == _guiButton || (isParent && _editor.Game.State.UI.Contains(_potentialParent));
            bool isFirst = true;
            foreach (var entityObj in entities)
            {
                IEntity entity = (IEntity)entityObj;
                MethodModel initializer = null;
                if (isFirst)
                {
                    isFirst = false;
                    initializer = methodModel;
                }
                var entityModel = new EntityModel
                {
                    ID = entity.ID,
                    DisplayName = entity.DisplayName,
                    Initializer = initializer,
                    Components = new Dictionary<Type, ComponentModel>(),
                    EntityConcreteType = entity.GetType(),
                    IsDirty = true,
                };
                _editor.Project.Model.Entities.Add(entity.ID, entityModel);
                if (isParent)
                {
                    if (entity is IObject obj) _potentialParent.TreeNode.AddChild(obj);
                    else throw new Exception($"Unkown entity created: {entity?.GetType().Name ?? "null"}");
                }
                if (isUi)
                {
                    if (entity is IObject obj)
                    {
                        _editor.Game.State.UI.Add(obj);
                        _editor.Project.Model.Guis.Add(entity.ID);
                    }
                    else throw new Exception($"Unkown entity created: {entity?.GetType().Name ?? "null"}");
                }
                else
                {
                    var roomModel = _editor.Project.Model.Rooms.First(r => r.ID == _editor.Game.State.Room.ID);
                    roomModel.Entities.Add(entity.ID);
                    if (entity is IObject obj) _editor.Game.State.Room.Objects.Add(obj);
                    else if (entity is IArea area) _editor.Game.State.Room.Areas.Add(area);
                    else throw new Exception($"Unkown entity created: {entity?.GetType().Name ?? "null"}");
                }
            }
        }

        private (object, MethodModel) runMethod(MethodInfo method, object factory, Dictionary<string, object> parameters)
        {
            var methodParams = method.GetParameters();
            object[] values = methodParams.Select(m => parameters.TryGetValue(m.Name, out object val) ?
                                                  val : MethodParam.GetDefaultValue(m.ParameterType)).ToArray();
            var model = new MethodModel { InstanceName = getFactoryName(factory), Name = method.Name, Parameters = values, ReturnType = method.ReturnType };
            return (method.Invoke(factory, values), model);
        }

        private string getFactoryName(object factory)
        {
            if (factory == _editor.Game.Factory.UI) return "_factory.UI";
            if (factory == _editor.Game.Factory.Object) return "_factory.Object";
            if (factory == _editor.Game.Factory.Room) return "_factory.Room";
            throw new InvalidOperationException($"Unsupported factory of type {factory?.GetType().ToString() ?? "null"}");
        }

        private void addTargetUIForCreate(IPanel panel, Target target)
        {
            if (target == Target.Area) return;

            var factory = _editor.Editor.Factory;
            var buttonsPanel = factory.UI.GetPanel("MethodWizardTargetPanel", 100f, 45f, MethodWizard.MARGIN_HORIZONTAL, 50f, panel);
            buttonsPanel.Tint = Colors.Transparent;

            var font = _editor.Editor.Settings.Defaults.TextFont;
            var labelConfig = new AGSTextConfig(font: factory.Fonts.LoadFont(font.FontFamily, font.SizeInPoints, FontStyle.Underline));
            factory.UI.GetLabel("AddToLabel", "Add To:", 50f, 20f, 0f, 0f, buttonsPanel, labelConfig);

            const float buttonY = -40f;
            var roomButton = factory.UI.GetCheckBox("AddToRoomRadioButton", (ButtonAnimation)null, null, null, null, 10f, buttonY, buttonsPanel, "Room", width: 20f, height: 25f);
            var guiButton = factory.UI.GetCheckBox("AddToGUIRadioButton", (ButtonAnimation)null, null, null, null, 130f, buttonY, buttonsPanel, "GUI", width: 20f, height: 25f);

            _guiButton = guiButton.GetComponent<ICheckboxComponent>();
            _targetRadioGroup = new AGSRadioGroup();
            roomButton.RadioGroup = _targetRadioGroup;
            guiButton.RadioGroup = _targetRadioGroup;
            if (_potentialParent != null)
            {
                var parentButton = factory.UI.GetCheckBox("AddToParentRadioButton", (ButtonAnimation)null, null, null, null, 250f, buttonY, buttonsPanel, _potentialParent.GetFriendlyName(), width: 20f, height: 25f);
                parentButton.RadioGroup = _targetRadioGroup;
                _parentButton = parentButton.GetComponent<ICheckboxComponent>();
            }
            _targetRadioGroup.SelectedButton = target == Target.UI ? _guiButton : roomButton;
        }
    }
}