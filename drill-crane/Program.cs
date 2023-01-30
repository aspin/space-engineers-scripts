using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // state machine
        bool isActive = false;
        string actionInProgress;
        string actionStage;
        float targetHorizontalPosition;
        float targetAngle;

        readonly DrillStateMachine sm;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;
            Config config = new Config();

            List<IMyPistonBase> horizontalPistons = new List<IMyPistonBase>();
            List<IMyPistonBase> verticalPistons = new List<IMyPistonBase>();

            string hPistonGroupName = string.Format("{0} Pistons H", config.GroupPrefix);
            string vPistonGroupName = string.Format("{0} Pistons V", config.GroupPrefix);

            IMyBlockGroup hPistonGroup = GridTerminalSystem.GetBlockGroupWithName(hPistonGroupName);
            if (hPistonGroup == null)
            {
                Echo(string.Format("horizontal piston group ({0}) not found", hPistonGroupName));
                return;
            }
            IMyBlockGroup vPistonGroup = GridTerminalSystem.GetBlockGroupWithName(string.Format("{0} Pistons V", config.GroupPrefix));
            if (vPistonGroup == null)
            {
                Echo(string.Format("vertical piston group ({0}) not found", vPistonGroupName));
                return;
            }

            hPistonGroup.GetBlocksOfType(horizontalPistons);
            vPistonGroup.GetBlocksOfType(verticalPistons);


            IMyMotorAdvancedStator rotor = GridTerminalSystem.GetBlockWithName(string.Format("{0} Rotor", config.GroupPrefix)) as IMyMotorAdvancedStator;
            IMyShipDrill drill = GridTerminalSystem.GetBlockWithName(string.Format("{0} Drill", config.GroupPrefix)) as IMyShipDrill;

            DrillState state = new DrillState(horizontalPistons, verticalPistons, rotor, drill, config);
            sm = new DrillStateMachine(state);
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (!sm.State.IsComplete())
            {
                Echo(sm.State.Error());
                return;
            }

            DrillAction action;
            if (Enum.TryParse(argument, out action))
            {
                Echo(string.Format("invalid action {0}", action));
                return;
            }

            sm.Process(action);
        }

        private void HandleNewAction(string action)
        {
            actionInProgress = action;

            switch (action)
            {
                case "stop":
                    DisableAll();
                    break;
                case "retractAll":
                    drill.Enabled = false;
                    RetractVertical();
                    isActive = true;
                    break;
                case "retractAndDrill":
                    RetractVertical();

                    isActive = true;
                    targetHorizontalPosition = horizontalPistons[0].CurrentPosition - config.HorizontalIncrement;
                    Echo(string.Format("[retract]: target horizontal position {0}", targetHorizontalPosition));
                    break;
                case "extendAndDrill":
                    RetractVertical();

                    isActive = true;
                    targetHorizontalPosition = horizontalPistons[0].CurrentPosition + config.HorizontalIncrement;
                    Echo(string.Format("[extend]: target horizontal position {0}", targetHorizontalPosition));
                    break;
                case "rotateCCWAndDrill":
                    RetractVertical();

                    isActive = true;
                    targetAngle = rotor.Angle - config.AngleIncrement;
                    Echo(string.Format("[ccw]: target angle {0}", targetAngle));
                    break;
                case "rotateCWAndDrill":
                    RetractVertical();

                    isActive = true;
                    targetAngle = rotor.Angle + config.AngleIncrement;
                    Echo(string.Format("[cw]: target angle {0}", targetAngle));
                    break;

                case "extendDrillLine":
                    RetractVertical();
                    isActive = true;
                    Echo("[edl]");
                    break;
                default:
                    Echo(string.Format("unknown action: {0}", action));
                    break;
            }

            if (isActive)
            {
                Runtime.UpdateFrequency = UpdateFrequency.Update100;
                actionStage = "initial";
            }
        }

        private void ContinueAction(string action)
        {
            switch (action)
            {
                case "stop":
                    break;
                case "retractAll":
                    bool fullVerticalExtension = FullVerticalExtension();
                    bool fullHorizontalRetraction = FullHorizontalRetraction();
                    bool reachedAngle = rotor.Angle == rotor.UpperLimitRad;

                    if (fullVerticalExtension && fullHorizontalRetraction && reachedAngle)
                    {
                        DisableAll();
                        break;
                    }

                    if (fullVerticalExtension)
                    {
                        SetPistonsEnabled(verticalPistons, false);
                        SetPistonsSpeed(horizontalPistons, -config.FastSpeed);
                        SetPistonsEnabled(horizontalPistons, true);
                        rotor.TargetVelocityRPM = 100f;
                        break;
                    }

                    break;
                case "retractAndDrill":
                    MoveHorizontalAndDrill(-config.SafeHorizontalSpeed, "retract");
                    break;
                case "extendAndDrill":
                    MoveHorizontalAndDrill(config.SafeHorizontalSpeed, "extend");
                    break;
                case "rotateCCWAndDrill":
                    MoveAngleAndDrill(-config.SafeAngleRPM, "ccw");
                    break;
                case "rotateCWAndDrill":
                    MoveAngleAndDrill(config.SafeAngleRPM, "cw");
                    break;
                default:
                    Echo(string.Format("unknown action: {0}", action));
                    break;
            }

            if (!isActive)
            {
                actionInProgress = "";
                Runtime.UpdateFrequency = UpdateFrequency.None;
            }
        }

        private void StartDrill()
        {
            Echo(string.Format("checking drill speed {0}", VerticalRetraction()));
            drill.Enabled = true;

            float speed = -SafeVerticalDrillSpeed();
            if (VerticalRetraction() < config.VerticalClearance)
            {
                speed = -config.FastSpeed;
            }

            SetPistonsSpeed(verticalPistons, speed);
            SetPistonsEnabled(verticalPistons, true);
            SetPistonsEnabled(horizontalPistons, false);
            rotor.Enabled = false;
        }
        private void MoveHorizontalAndDrill(float speed, string echoPrefix)
        {
            bool fullVerticalRetraction = FullVerticalRetraction();
            bool fullVerticalExtension = FullVerticalExtension();
            bool reachedHorizontal = ReachedTargetHorizontalExtension(speed > 0);

            if (actionStage == "drill" && fullVerticalRetraction && reachedHorizontal)
            {
                Echo(string.Format("[{0}]: complete. shutting off...", echoPrefix));
                DisableAll();
                return;
            }

            // still retracting drill; wait for full extension
            if (actionStage == "initial" && !fullVerticalExtension)
            {
                return;
            }

            actionStage = "drill";
            if (!reachedHorizontal)
            {
                Echo(string.Format("[{0}]: horizontal to target {1}...", echoPrefix, targetHorizontalPosition));
                foreach (IMyPistonBase piston in horizontalPistons)
                {
                    Echo(string.Format("[{0}]: current position {0}...", echoPrefix, piston.CurrentPosition));
                }
                SetPistonsSpeed(horizontalPistons, speed);
                SetPistonsEnabled(horizontalPistons, true);
                return;
            }
            else
            {
                Echo(string.Format("[{0}]: drilling down at speed {1}...", echoPrefix, SafeVerticalDrillSpeed()));
                StartDrill();
                return;
            }
        }

        // positive speed is CW
        private void MoveAngleAndDrill(float rpm, string echoPrefix)
        {
            bool fullVerticalRetraction = FullVerticalRetraction();
            bool fullVerticalExtension = FullVerticalExtension();
            bool reachedAngle;
            if (rpm > 0)
            {
                reachedAngle = rotor.Angle >= targetAngle;
            }
            else
            {
                reachedAngle = rotor.Angle <= targetAngle;
            }

            if (fullVerticalRetraction && reachedAngle)
            {
                Echo(string.Format("[{0}]: complete. shutting off...", echoPrefix));
                isActive = false;
                DisableAll();
                return;
            }

            // still retracting drill; wait for full extension
            if (actionStage == "initial" && !fullVerticalExtension)
            {
                return;
            }

            actionStage = "drill";

            if (!reachedAngle)
            {
                Echo(string.Format("[{0}]: current angle {1} to target {2}...", echoPrefix, rotor.Angle, targetAngle));
                rotor.TargetVelocityRPM = rpm;
                rotor.Enabled = true;
                return;
            }
            else
            {
                Echo(string.Format("[{0}]: drilling down at speed {1}...", echoPrefix, SafeVerticalDrillSpeed()));
                StartDrill();
                return;
            }
        }


        private void RetractVertical()
        {
            SetPistonsSpeed(verticalPistons, config.FastSpeed); // extension since it's reversed
            SetPistonsEnabled(verticalPistons, true);
        }
    }
}
