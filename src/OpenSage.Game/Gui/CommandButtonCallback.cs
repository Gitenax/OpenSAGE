﻿using System;
using System.Linq;
using OpenSage.Gui.ControlBar;
using OpenSage.Logic.Object;
using OpenSage.Logic.Orders;
using OpenSage.Mathematics;

namespace OpenSage.Gui
{
    public static class CommandButtonCallback
    {
        public static void HandleCommand(Game game, CommandButton commandButton, ObjectDefinition objectDefinition, bool cancel = false, int index = 0)
        {
            var playerIndex = game.Scene3D.GetPlayerIndex(game.Scene3D.LocalPlayer);
            Order CreateOrder(OrderType type) => new Order(playerIndex, type);

            var selection = game.Scene3D.LocalPlayer.SelectedUnits;
            var selectedObject = selection.FirstOrDefault();

            CastleBehavior castleBehavior;

            Order order = null;
            switch (commandButton.Command)
            {
                case CommandType.CastleUnpack:
                    //TODO: proper castleUnpack order & spend money of the player
                    castleBehavior = selectedObject.FindBehavior<CastleBehavior>();
                    castleBehavior.Unpack(selectedObject.Owner);
                    game.Scene3D.LocalPlayer.DeselectUnits();
                    break;

                case CommandType.CastleUnpackExplicitObject:
                case CommandType.FoundationConstruct:
                    castleBehavior = selectedObject.FindBehavior<CastleBehavior>();
                    if (castleBehavior != null)
                    {
                        castleBehavior.IsUnpacked = true;
                    }

                    order = CreateOrder(OrderType.BuildObject);
                    order.AddIntegerArgument(objectDefinition.InternalId);
                    order.AddPositionArgument(selectedObject.Translation);
                    order.AddFloatArgument(selectedObject.Yaw + MathUtility.ToRadians(objectDefinition.PlacementViewAngle));

                    game.Scene3D.LocalPlayer.DeselectUnits();
                    break;

                case CommandType.DozerConstruct:
                    game.OrderGenerator.StartConstructBuilding(objectDefinition);
                    break;
                case CommandType.DozerConstructCancel:
                    order = CreateOrder(OrderType.CancelBuild);
                    break;

                case CommandType.ToggleOvercharge:
                    order = CreateOrder(OrderType.ToggleOvercharge);
                    break;

                case CommandType.CancelUnitBuild:
                case CommandType.Sell:
                    order = CreateOrder(OrderType.Sell);
                    break;

                case CommandType.UnitBuild:
                    if (cancel)
                    {
                        order = CreateOrder(OrderType.CancelUnit);
                        order.AddIntegerArgument(index);
                        break;
                    }
                    order = CreateOrder(OrderType.CreateUnit);
                    order.AddIntegerArgument(objectDefinition.InternalId);
                    order.AddIntegerArgument(1);
                    break;

                case CommandType.SetRallyPoint:
                    game.OrderGenerator.SetRallyPoint();
                    break;

                case CommandType.PlayerUpgrade:
                case CommandType.ObjectUpgrade:
                    if (cancel)
                    {
                        order = CreateOrder(OrderType.CancelUpgrade);
                        order.AddIntegerArgument(index);
                        break;
                    }

                    order = CreateOrder(OrderType.BeginUpgrade);
                    //TODO: figure this out correctly
                    if (selection.Count == 0)
                    {
                        break;
                    }

                    order.AddIntegerArgument((int)selection.First().ID);

                    var upgrade = commandButton.Upgrade.Value;
                    order.AddIntegerArgument(upgrade.InternalId);
                    break;

                case CommandType.Stop:
                    // TODO: Also stop construction?
                    order = CreateOrder(OrderType.StopMoving);
                    break;

                case CommandType.SpecialPowerFromCommandCenter:
                    // TODO: This must somehow invoke the special power from the command center
                case CommandType.SpecialPower:
                    {
                        var specialPower = commandButton.SpecialPower.Value;
                        if (commandButton.Options != null)
                        {
                            var needsPos = commandButton.Options.Get(CommandButtonOption.NeedTargetPos);
                            var needsObject = commandButton.Options.Get(CommandButtonOption.NeedTargetAllyObject)
                                             || commandButton.Options.Get(CommandButtonOption.NeedTargetEnemyObject)
                                             || commandButton.Options.Get(CommandButtonOption.NeedTargetNeutralObject);

                            if (needsPos)
                            {
                                game.OrderGenerator.StartSpecialPowerAtLocation(specialPower);
                            }
                            else if (needsObject)
                            {
                                game.OrderGenerator.StartSpecialPowerAtObject(specialPower);
                            }
                        }
                        else
                        {
                            order = CreateOrder(OrderType.SpecialPower);
                            order.AddIntegerArgument(specialPower.InternalId);
                            order.AddIntegerArgument(0);
                            order.AddObjectIdArgument(0);
                        }
                    }
                    break;

                case CommandType.ToggleGate:
                    //TODO: proper toggle gate order
                    var gateOpenAndCloseBehavior = selectedObject.FindBehavior<GateOpenAndCloseBehavior>();
                    gateOpenAndCloseBehavior.Toggle();
                    break;

                case CommandType.PurchaseScience:
                    var science = commandButton.Science[0];
                    order = CreateOrder(OrderType.PurchaseScience);
                    order.AddIntegerArgument(science.Value.InternalId);
                    break;
                default:
                    throw new NotImplementedException();
            }

            if (order != null)
            {
                game.NetworkMessageBuffer.AddLocalOrder(order);
            }
        }
    }
}
