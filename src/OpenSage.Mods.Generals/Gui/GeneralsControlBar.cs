﻿using System;
using System.Linq;
using OpenSage.Content;
using OpenSage.Core;
using OpenSage.Gui;
using OpenSage.Gui.ControlBar;
using OpenSage.Gui.Wnd.Controls;
using OpenSage.Gui.Wnd.Images;
using OpenSage.Logic;
using OpenSage.Logic.Object;
using OpenSage.Logic.Orders;
using OpenSage.Logic.Object.Production;
using OpenSage.Mathematics;

namespace OpenSage.Mods.Generals.Gui
{
    public sealed class GeneralsControlBar : IControlBar
    {
        private enum ControlBarSize
        {
            Maximized,
            Minimized
        }

        // How much the control bar should be moved down when minimized?
        private const int MinimizeOffset = 120;

        private readonly ControlBarScheme _scheme;

        public Control SpecialPowerBar => _powerShortcutWindow.Root;

        public ControlBarScheme Scheme => _scheme;

        private ControlBarState _state;

        private ControlBarState State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    _state.OnEnterState(this);
                }
            }
        }

        private readonly Window _background;
        private readonly Window _window;
        private readonly Window _descriptionWindow;
        private readonly Window _powerShortcutWindow;

        private readonly Control _center;
        private readonly Control _right;

        private readonly Control _commandWindow;

        private readonly Label _moneyDisplay;
        // TODO: Change this to a ProgressBar when they are implemented.
        private readonly Control _powerBar;
        // TODO: Change this to a ProgressBar when they are implemented.
        private readonly Control _expBar;

        private readonly Button _resize;

        private readonly Image _resizeDownBackground;
        private readonly Image _resizeDownHover;
        private readonly Image _resizeDownPushed;

        private readonly Image _resizeUpBackground;
        private readonly Image _resizeUpHover;
        private readonly Image _resizeUpPushed;

        private readonly Image _commandButtonHover;
        private readonly Image _commandButtonPushed;

        public Image CommandButtonHover => _commandButtonHover;
        public Image CommandButtonPush => _commandButtonPushed;

        private ControlBarSize _size = ControlBarSize.Maximized;

        private Control FindControl(string name) => _window.Controls.FindControl($"ControlBar.wnd:{name}");

        private void ApplyProgress(string name, string coordPrefix, float progress = 1.0f)
        {
            var control = FindControl(name);

            if (control == null)
            {
                return;
            }

            var schemeType = _scheme.GetType();

            var ul = (Point2D) schemeType.GetProperty($"{coordPrefix}UL").GetValue(_scheme);
            var lr = (Point2D) schemeType.GetProperty($"{coordPrefix}LR").GetValue(_scheme);
            var width = (int) (progress * (lr.X - ul.X));
            lr = new Point2D(ul.X + width, lr.Y);

            control.Bounds = Rectangle.FromCorners(ul - _window.Bounds.Location, lr - _window.Bounds.Location);
        }

        public void ShowDescription(string name, string cost, string description)
        {
            var lblName = _descriptionWindow.Controls.FindControl("ControlBarPopupDescription.wnd:StaticTextName") as Label;
            lblName.Text = name;
            var lblCost = _descriptionWindow.Controls.FindControl("ControlBarPopupDescription.wnd:StaticTextCost") as Label;
            lblCost.Text = cost;
            var lblDesc = _descriptionWindow.Controls.FindControl("ControlBarPopupDescription.wnd:StaticTextDescription") as Label;
            lblDesc.Text = description;
            _descriptionWindow.Show();
        }

        public void HideDescription()
        {
            _descriptionWindow.Hide();
        }

        public GeneralsControlBar(Window background, Window window, Window descriptionWindow, Window powerShortcutWindow, ControlBarScheme scheme, ContentManager contentManager, AssetStore assetStore)
        {
            _background = background;
            _window = window;
            _descriptionWindow = descriptionWindow;
            _descriptionWindow.Hide();
            _powerShortcutWindow = powerShortcutWindow;
            _scheme = scheme;

            // Disable all specialpower buttons
            var buttonRoot = _powerShortcutWindow.Controls[0];
            foreach (var control in buttonRoot.Controls.AsList())
            {
                control.Hide();
            }

            _center = FindControl("CenterBackground");
            _right = FindControl("RightHUD");

            _commandWindow = FindControl("CommandWindow");

            _moneyDisplay = FindControl("MoneyDisplay") as Label;
            _moneyDisplay.Text = "$ 0";
            _powerBar = FindControl("PowerWindow");
            _expBar = FindControl("GeneralsExp");

            _resize = FindControl("ButtonLarge") as Button;

            _resizeDownBackground = window.ImageLoader.CreateFromMappedImageReference(_scheme.ToggleButtonDownOn);
            _resizeDownHover = window.ImageLoader.CreateFromMappedImageReference(_scheme.ToggleButtonDownIn);
            _resizeDownPushed = window.ImageLoader.CreateFromMappedImageReference(_scheme.ToggleButtonDownPushed);

            _resizeUpBackground = window.ImageLoader.CreateFromMappedImageReference(_scheme.ToggleButtonUpOn);
            _resizeUpHover = window.ImageLoader.CreateFromMappedImageReference(_scheme.ToggleButtonUpIn);
            _resizeUpPushed = window.ImageLoader.CreateFromMappedImageReference(_scheme.ToggleButtonUpPushed);

            _commandButtonHover = window.ImageLoader.CreateFromMappedImageReference(assetStore.MappedImages.GetLazyAssetReferenceByName("Cameo_hilited"));
            _commandButtonPushed = window.ImageLoader.CreateFromMappedImageReference(assetStore.MappedImages.GetLazyAssetReferenceByName("Cameo_push"));

            UpdateResizeButtonStyle();

            State = ControlBarState.Default;
        }

        // TODO: This should be called at every logic tick.
        // TODO: This takes a player as the state information. Do we need any other state?
        public void Update(Player player)
        {
            if (player == null)
            {
                return;
            }

            _moneyDisplay.Text = $"$ {player.BankAccount.Money}";

            var powerBarProgress = player.GetEnergy(this._window.Game.Scene3D.GameObjects) / 100.0f;
            ApplyProgress("PowerWindow", "PowerBar", Math.Clamp(powerBarProgress, 0.0f, 1.0f));

            if (player.SelectedUnits.Count > 0 && player.SelectedUnits.First().Owner == player)
            {
                var unit = player.SelectedUnits.First();
                if (player.SelectedUnits.Count == 1 && unit.IsBeingConstructed())
                {
                    State = ControlBarState.Construction;
                }
                else
                {
                    State = ControlBarState.Selected;
                }
            }
            else
            {
                State = ControlBarState.Default;
            }

            // TODO: Only do this when command set changes.
            GeneralsExpPointsCallbacks.Update(player, this);
            State.Update(player, this);
        }

        public void ToggleSize()
        {
            if (_size == ControlBarSize.Maximized)
            {
                _window.Top += MinimizeOffset;
                _background.Top += MinimizeOffset;
                _size = ControlBarSize.Minimized;
            }
            else
            {
                _window.Top -= MinimizeOffset;
                _background.Top -= MinimizeOffset;
                _size = ControlBarSize.Maximized;
            }

            UpdateResizeButtonStyle();
        }

        private void UpdateResizeButtonStyle()
        {
            if (_size == ControlBarSize.Maximized)
            {
                _resize.BackgroundImage = _resizeDownBackground;
                _resize.HoverBackgroundImage = _resizeDownHover;
                _resize.PushedBackgroundImage = _resizeDownPushed;
            }
            else
            {
                _resize.BackgroundImage = _resizeUpBackground;
                _resize.HoverBackgroundImage = _resizeUpHover;
                _resize.PushedBackgroundImage = _resizeUpPushed;
            }
        }

        public void AddToScene(Scene2D scene2D)
        {
            scene2D.WndWindowManager.PushWindow(_background);
            scene2D.WndWindowManager.PushWindow(_descriptionWindow);
            scene2D.WndWindowManager.PushWindow(_powerShortcutWindow);
            scene2D.WndWindowManager.PushWindow(_window);
        }

        private abstract class ControlBarState
        {
            public abstract void OnEnterState(GeneralsControlBar controlBar);
            public abstract void Update(Player player, GeneralsControlBar controlBar);

            public static ControlBarState Default { get; } = new DefaultControlBarState();
            public static ControlBarState Selected { get; } = new SelectedControlBarState();
            public static ControlBarState Construction { get; } = new UnderConstructionControlBarState();

            private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

            protected void ClearControls(GeneralsControlBar controlBar)
            {
                foreach (var control in controlBar._center.Controls.AsList())
                {
                    if (control.Name == "ControlBar.wnd:CommandWindow")
                    {
                        foreach (var child in control.Controls.AsList())
                        {
                            child.Hide();
                        }
                        control.Show();
                    }
                    else
                    {
                        control.Hide();
                    }
                }

                foreach (var control in controlBar._right.Controls.AsList())
                {
                    control.Hide();
                }
            }

            protected void ApplySpecialPowers(Player player, GeneralsControlBar controlBar)
            {
                var commandSet = player.Template.SpecialPowerShortcutCommandSet.Value;
                for (int i = 0; i < player.Template.SpecialPowerShortcutButtonCount; i++)
                {
                    if (!commandSet.Buttons.TryGetValue(i + 1, out var commandButtonReference))
                    {
                        continue;
                    }

                    var commandButton = commandButtonReference.Value;

                    bool specialPowerAvailable = player.SpecialPowerAvailable(commandButton.SpecialPower.Value);

                    var parentControl = controlBar.SpecialPowerBar.Controls[i];
                    parentControl.Visible = specialPowerAvailable;

                    var buttonControl = parentControl.Controls[0] as Button;
                    CommandButtonUtils.SetCommandButton(buttonControl, commandButton, controlBar);
                }
            }

            protected void ApplyCommandSet(GameObject selectedUnit, GeneralsControlBar controlBar, CommandSet commandSet)
            {
                for (var i = 1; i < 100; i++) // Generals has 12 buttons and Zero Hour has 14, but there's no need to set those values as the limit in the code
                {
                    var buttonControl = controlBar._commandWindow.Controls.FindControl($"ControlBar.wnd:ButtonCommand{i:D2}") as Button;

                    // the amount of ButtonCommand children in ControlBar.wnd defines how many buttons the game will have in-game
                    if (controlBar._commandWindow.Controls.FindControl($"ControlBar.wnd:ButtonCommand{i:D2}") == null)
                        break;

                    if (commandSet != null && commandSet.Buttons.TryGetValue(i, out var commandButtonReference))
                    {
                        var commandButton = commandButtonReference.Value;

                        CommandButtonUtils.SetCommandButton(buttonControl, commandButton, controlBar);

                        var objectDefinition = commandButton.Object?.Value;

                        switch (commandButton.Command)
                        {
                            // Disable the button when the unit is not produceable
                            case CommandType.DozerConstruct:
                            case CommandType.UnitBuild:
                                buttonControl.Enabled = selectedUnit.CanConstructUnit(objectDefinition);
                                buttonControl.Show();
                                break;
                            // Disable the button when the object already has it etc.
                            case CommandType.PlayerUpgrade:
                            case CommandType.ObjectUpgrade:
                                buttonControl.Enabled = selectedUnit.CanEnqueueUpgrade(commandButton.Upgrade.Value);
                                buttonControl.Show();
                                break;
                            case CommandType.SpecialPower:
                                buttonControl.Visible = selectedUnit.Owner.SpecialPowerAvailable(commandButton.SpecialPower.Value);
                                break;
                            default:
                                buttonControl.Enabled = true;
                                buttonControl.Show();
                                break;
                        }
                    }
                    else
                    {
                        buttonControl.Hide();
                    }
                }
            }
        }

        private sealed class DefaultControlBarState : ControlBarState
        {
            public override void OnEnterState(GeneralsControlBar controlBar)
            {
                ClearControls(controlBar);
            }

            public override void Update(Player player, GeneralsControlBar controlBar)
            {
            }
        }

        private sealed class SelectedControlBarState : ControlBarState
        {
            public override void OnEnterState(GeneralsControlBar controlBar)
            {
                ClearControls(controlBar);
            }

            private NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

            const int PRODUCTION_QUEUE_SIZE = 9;

            public override void Update(Player player, GeneralsControlBar controlBar)
            {
                // TODO: Handle multiple selection.
                var unit = player.SelectedUnits.First();

                if (unit.Definition.CommandSet == null) return;

                var commandSet = unit.Definition.CommandSet.Value;
                ApplySpecialPowers(player, controlBar);

                // TODO: Only do this when command set changes.
                ApplyCommandSet(unit, controlBar, commandSet);

                var unitSelectedControl = controlBar._right.Controls.FindControl("ControlBar.wnd:WinUnitSelected");

                var isProducing = unit.ProductionUpdate?.IsProducing ?? false;

                var productionQueueWindow = controlBar._right.Controls.FindControl("ControlBar.wnd:ProductionQueueWindow");

                unitSelectedControl.Visible = !isProducing;
                productionQueueWindow.Visible = isProducing;

                if (isProducing)
                {
                    var queue = unit.ProductionUpdate.ProductionQueue;

                    for (var pos = 0; pos < PRODUCTION_QUEUE_SIZE; pos++)
                    {
                        var queueButton = productionQueueWindow.Controls.FindControl($"ControlBar.wnd:ButtonQueue0{pos + 1}");

                        if (queueButton == null)
                        {
                            _logger.Warn($"Could not find the right control (ControlBar.wnd:ButtonQueue0{pos + 1})");
                            continue;
                        }

                        Image img = null;
                        if (queue.Count > pos)
                        {
                            var job = queue[pos];
                            if (job != null)
                            {
                                queueButton.DrawCallback = (control, drawingContext) =>
                                {
                                    queueButton.DefaultDraw(control, drawingContext);

                                    // Draw radial progress indicator.
                                    drawingContext.FillRectangleRadial360(
                                        control.ClientRectangle,
                                        controlBar._scheme.BuildUpClockColor.ToColorRgbaF(),
                                        job.Progress);
                                };

                                if (job.Type == ProductionJobType.Unit)
                                {
                                    img = controlBar._window.ImageLoader.CreateFromMappedImageReference(job.ObjectDefinition.SelectPortrait);
                                }
                                else if (job.Type == ProductionJobType.Upgrade)
                                {
                                    img = controlBar._window.ImageLoader.CreateFromMappedImageReference(job.UpgradeDefinition.ButtonImage);
                                }
                                var posCopy = pos;

                                queueButton.SystemCallback = (control, message, context) =>
                                {
                                    var playerIndex = context.Game.Scene3D.GetPlayerIndex(context.Game.Scene3D.LocalPlayer);
                                    if (job.Type == ProductionJobType.Unit)
                                    {
                                        var order = new Order(playerIndex, OrderType.CancelUnit);
                                        order.AddIntegerArgument(posCopy);
                                        context.Game.NetworkMessageBuffer.AddLocalOrder(order);
                                    }
                                    else if (job.Type == ProductionJobType.Upgrade)
                                    {
                                        var order = new Order(playerIndex, OrderType.CancelUpgrade);
                                        order.AddIntegerArgument(job.UpgradeDefinition.InternalId);
                                        context.Game.NetworkMessageBuffer.AddLocalOrder(order);
                                    }
                                };
                            }
                        }
                        queueButton.BackgroundImage = img;

                        if (img == null)
                        {
                            queueButton.DrawCallback = queueButton.DefaultDraw;
                            queueButton.SystemCallback = null;
                        }
                    }
                }

                var iconControl = unitSelectedControl.Controls.FindControl("ControlBar.wnd:CameoWindow");
                var cameoImg = controlBar._window.ImageLoader.CreateFromMappedImageReference(unit.Definition.SelectPortrait);
                iconControl.BackgroundImage = cameoImg;
                iconControl.Visible = !isProducing;

                void ApplyUpgradeImage(GameObject unit, string upgradeControlName, LazyAssetReference<UpgradeTemplate> upgradeReference)
                {
                    var upgrade = upgradeReference?.Value;
                    var upgradeControl = unitSelectedControl.Controls.FindControl($"ControlBar.wnd:{upgradeControlName}");

                    upgradeControl.BackgroundImage = upgrade != null
                        ? controlBar._window.ImageLoader.CreateFromMappedImageReference(upgrade.ButtonImage)
                        : null;
                    upgradeControl.DisabledBackgroundImage = upgradeControl.BackgroundImage?.WithGrayscale(true);

                    upgradeControl.Enabled = unit.HasUpgrade(upgrade);
                }

                ApplyUpgradeImage(unit, "UnitUpgrade1", unit.Definition.UpgradeCameo1);
                ApplyUpgradeImage(unit, "UnitUpgrade2", unit.Definition.UpgradeCameo2);
                ApplyUpgradeImage(unit, "UnitUpgrade3", unit.Definition.UpgradeCameo3);
                ApplyUpgradeImage(unit, "UnitUpgrade4", unit.Definition.UpgradeCameo4);
                ApplyUpgradeImage(unit, "UnitUpgrade5", unit.Definition.UpgradeCameo5);
            }
        }

        private sealed class UnderConstructionControlBarState : ControlBarState
        {
            Control _window;
            Control _progressText;
            string _baseText;


            public override void OnEnterState(GeneralsControlBar controlBar)
            {
                ClearControls(controlBar);

                _window = controlBar._center.Controls.FindControl("ControlBar.wnd:UnderConstructionWindow");
                _window.Show();
                _progressText = _window.Controls.FindControl("ControlBar.wnd:UnderConstructionDesc");

                if (string.IsNullOrEmpty(_baseText))
                {
                    _baseText = StringConverter.FromPrintf(_progressText.Text);
                }

                Button cancelButton = _window.Controls.FindControl("ControlBar.wnd:ButtonCancelConstruction") as Button;
                // Is that CommandButton hardcoded or defined somewhere?
                var commandButton = controlBar._window.Game.AssetStore.CommandButtons.GetByName("Command_CancelConstruction");
                CommandButtonUtils.SetCommandButton(cancelButton, commandButton, controlBar);
            }

            public override void Update(Player player, GeneralsControlBar controlBar)
            {
                var unit = player.SelectedUnits.First();
                var percent = unit.BuildProgress * 100.0f;
                //TODO: the formatting should be taken from the printf string
                var text = string.Format(_baseText, percent.ToString("0.00"));
                _progressText.Text = text;
            }
        }
    }

    public sealed class GeneralsControlBarSource : IControlBarSource
    {
        public IControlBar Create(string side, Game game)
        {
            var scheme = game.AssetStore.ControlBarSchemes.FindBySide(side);

            // TODO: Support multiple image parts?
            // Generals always uses exactly one image part.
            var imagePart = scheme.ImageParts[0];

            var background = new Control
            {
                Name = "OpenSAGE:ControlBarBackground",
                Bounds = new Rectangle(imagePart.Position, imagePart.Size),
            };

            var backgroundWindow = new Window(scheme.ScreenCreationRes, background, game);
            backgroundWindow.Enabled = false;
            var controlBarWindow = game.LoadWindow("ControlBar.wnd");
            var controlBarDescriptionWindow = game.LoadWindow("ControlBarPopupDescription.wnd");

            background.BackgroundImage = backgroundWindow.ImageLoader.CreateFromMappedImageReference(imagePart.ImageName);

            Control FindControl(string name) => controlBarWindow.Controls.FindControl($"ControlBar.wnd:{name}");

            // TODO: Implement under attack indicator.
            FindControl("WinUAttack").Hide();

            // TODO: What is this?
            FindControl("OnTopDraw").Hide();

            var windowOrigin = controlBarWindow.Bounds.Location;
            var schemeType = scheme.GetType();

            Control ApplyBounds(string name, string coordPrefix)
            {
                var control = FindControl(name);

                var ul = (Point2D) schemeType.GetProperty($"{coordPrefix}UL").GetValue(scheme);
                var lr = (Point2D) schemeType.GetProperty($"{coordPrefix}LR").GetValue(scheme);

                control.Bounds = Rectangle.FromCorners(ul - windowOrigin, lr - windowOrigin);

                return control;
            }

            void ApplyButtonScheme(string name, string coordPrefix, string texturePrefix)
            {
                var button = ApplyBounds(name, coordPrefix) as Button;

                Image LoadImageForState(string state) =>
                    controlBarWindow.ImageLoader.CreateFromMappedImageReference(
                        (LazyAssetReference<MappedImage>) schemeType.GetProperty($"{texturePrefix}{state}")?.GetValue(scheme));

                button.BackgroundImage = LoadImageForState("Enable");
                button.DisabledBackgroundImage = LoadImageForState("Disabled");
                button.HoverBackgroundImage = LoadImageForState("Highlighted");
                button.PushedBackgroundImage = LoadImageForState("Pushed");
            }

            ApplyBounds("MoneyDisplay", "Money");
            ApplyBounds("PowerWindow", "PowerBar");

            ApplyButtonScheme("ButtonOptions", "Options", "OptionsButton");
            ApplyButtonScheme("ButtonPlaceBeacon", "Beacon", "BeaconButton");
            ApplyButtonScheme("PopupCommunicator", "Chat", "BuddyButton");
            ApplyButtonScheme("ButtonIdleWorker", "Worker", "IdleWorkerButton");

            ApplyButtonScheme("ButtonGeneral", "General", "GeneralButton");
            // Textures are set by ControlBar
            ApplyBounds("ButtonLarge", "MinMax");

            var rightHud = FindControl("RightHUD");
            rightHud.BorderWidth = 0;
            rightHud.BackgroundColor = ColorRgbaF.Transparent;
            rightHud.BackgroundImage = controlBarWindow.ImageLoader.CreateFromMappedImageReference(scheme.RightHudImage);

            FindControl("ExpBarForeground").BackgroundImage = controlBarWindow.ImageLoader.CreateFromMappedImageReference(scheme.ExpBarForegroundImage);

            var powersShortcutBar = game.LoadWindow(game.Scene3D.LocalPlayer.Template.SpecialPowerShortcutWinName);
            return new GeneralsControlBar(backgroundWindow, controlBarWindow, controlBarDescriptionWindow, powersShortcutBar, scheme, game.ContentManager, game.AssetStore);
        }
    }
}
