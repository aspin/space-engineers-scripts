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
        // config for naming
        readonly string prefix = "Drill Rig";

        // config for amount of clearance to allow fast movement
        readonly float verticalClearance = 1.5f;

        // config for speed of drill movement
        readonly float safeVerticalDrillSpeed = 0.3f;
        readonly float safeHorizontalSpeed = 0.5f;
        readonly float safeAngleRPM = 1f;
        readonly float fastSpeed = 1f;

        // config for amount drill should move with each action
        readonly float horizontalIncrement = 0.5f;
        readonly float angleIncrement = 3f / 180f * (float)Math.PI;

        readonly List<IMyPistonBase> horizontalPistons;
        readonly List<IMyPistonBase> verticalPistons;
        readonly IMyMotorAdvancedStator rotor;
        readonly IMyShipDrill drill;

        // state machine
        Boolean isActive = false;
        string actionInProgress;
        string actionStage;
        float targetHorizontalPosition;
        float targetAngle;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;

            horizontalPistons = new List<IMyPistonBase>();
            verticalPistons = new List<IMyPistonBase>();

            string hPistonGroupName = String.Format("{0} Pistons H", prefix);
            string vPistonGroupName = String.Format("{0} Pistons V", prefix);

            IMyBlockGroup hPistonGroup = GridTerminalSystem.GetBlockGroupWithName(hPistonGroupName);
            if (hPistonGroup == null)
            {
                Echo(String.Format("horizontal piston group ({0}) not found", hPistonGroupName));
                return;
            }
            IMyBlockGroup vPistonGroup = GridTerminalSystem.GetBlockGroupWithName(String.Format("{0} Pistons V", prefix));
            if (vPistonGroup == null)
            {
                Echo(String.Format("vertical piston group ({0}) not found", vPistonGroupName));
                return;
            }

            hPistonGroup.GetBlocksOfType(horizontalPistons);
            vPistonGroup.GetBlocksOfType(verticalPistons);

            rotor = GridTerminalSystem.GetBlockWithName(String.Format("{0} Rotor", prefix)) as IMyMotorAdvancedStator;
            drill = GridTerminalSystem.GetBlockWithName(String.Format("{0} Drill", prefix)) as IMyShipDrill;

        }

        public void Main(string argument, UpdateType updateSource)
        {
            // short circuit conditions
            if (rotor == null || drill == null || horizontalPistons.Count == 0 || verticalPistons.Count == 0)
            {
                Echo(String.Format("required components missing: [rotor {0}] [drill {1}] [hPistons {2}] [vPistons {3}]", rotor == null, drill == null, horizontalPistons.Count, verticalPistons.Count));
                return;
            }

            if (!isActive || argument == "stop" || argument == "retractAll")
            {
                HandleNewAction(argument);
            }
            else
            {
                ContinueAction(actionInProgress);
            }

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
                    targetHorizontalPosition = horizontalPistons[0].CurrentPosition - horizontalIncrement;
                    Echo(String.Format("[retract]: target horizontal position {0}", targetHorizontalPosition));
                    break;
                case "extendAndDrill":
                    RetractVertical();

                    isActive = true;
                    targetHorizontalPosition = horizontalPistons[0].CurrentPosition + horizontalIncrement;
                    Echo(String.Format("[extend]: target horizontal position {0}", targetHorizontalPosition));
                    break;
                case "rotateCCWAndDrill":
                    RetractVertical();

                    isActive = true;
                    targetAngle = rotor.Angle - angleIncrement;
                    Echo(String.Format("[ccw]: target angle {0}", targetAngle));
                    break;
                case "rotateCWAndDrill":
                    RetractVertical();

                    isActive = true;
                    targetAngle = rotor.Angle + angleIncrement;
                    Echo(String.Format("[cw]: target angle {0}", targetAngle));
                    break;

                case "extendDrillLine":
                    RetractVertical();
                    isActive = true;
                    Echo("[edl]");
                    break;
                default:
                    Echo(String.Format("unknown action: {0}", action));
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
                    Boolean fullVerticalExtension = FullVerticalExtension();
                    Boolean fullHorizontalRetraction = FullHorizontalRetraction();
                    Boolean reachedAngle = rotor.Angle == rotor.UpperLimitRad;

                    if (fullVerticalExtension && fullHorizontalRetraction && reachedAngle)
                    {
                        DisableAll();
                        break;
                    }

                    if (fullVerticalExtension)
                    {
                        SetPistonsEnabled(verticalPistons, false);
                        SetPistonsSpeed(horizontalPistons, -fastSpeed);
                        SetPistonsEnabled(horizontalPistons, true);
                        rotor.TargetVelocityRPM = 100f;
                        break;
                    }

                    break;
                case "retractAndDrill":
                    MoveHorizontalAndDrill(-safeHorizontalSpeed, "retract");
                    break;
                case "extendAndDrill":
                    MoveHorizontalAndDrill(safeHorizontalSpeed, "extend");
                    break;
                case "rotateCCWAndDrill":
                    MoveAngleAndDrill(-safeAngleRPM, "ccw");
                    break;
                case "rotateCWAndDrill":
                    MoveAngleAndDrill(safeAngleRPM, "cw");
                    break;
                default:
                    Echo(String.Format("unknown action: {0}", action));
                    break;
            }

            if (!isActive)
            {
                actionInProgress = "";
                Runtime.UpdateFrequency = UpdateFrequency.None;
            }
        }

        // extension = all the way up, e.g. drill out of ground
        private Boolean FullVerticalExtension()
        {
            return verticalPistons.Aggregate(
                true,
                (extended, piston) => extended && (piston.CurrentPosition == piston.HighestPosition)
            );
        }

        // how deep the drill is in the ground right now
        private float VerticalRetraction()
        {
            return verticalPistons.Aggregate(
                0f,
                (sum, piston) => sum + (piston.HighestPosition - piston.CurrentPosition)
            );
        }

        // extension = all the way down, e.g. drill all the way in ground
        private Boolean FullVerticalRetraction()
        {
            return verticalPistons.Aggregate(
                true,
                (extended, piston) => extended && (piston.CurrentPosition == piston.LowestPosition)
            );
        }

        // retraction = all the way in, e.g. drill closest to origin
        private Boolean FullHorizontalRetraction()
        {
            return horizontalPistons.Aggregate(
                true,
                (sum, piston) => sum && (piston.CurrentPosition == piston.LowestPosition)
            );
        }

        // assume all pistons must be extended to same number
        private Boolean ReachedTargetHorizontalExtension(Boolean greater)
        {
            if (greater)
            {
                return horizontalPistons.Aggregate(
                    true,
                    (sum, piston) => sum && (piston.CurrentPosition >= targetHorizontalPosition)
                );
            }
            else
            {
                return horizontalPistons.Aggregate(
                    true,
                    (sum, piston) => sum && (piston.CurrentPosition <= targetHorizontalPosition)
                );
            }
        }

        private float SafeVerticalDrillSpeed()
        {
            return (float)(safeVerticalDrillSpeed / (double)verticalPistons.Count);
        }

        private void DisableAll()
        {
            SetPistonsEnabled(horizontalPistons, false);
            SetPistonsEnabled(verticalPistons, false);
            rotor.Enabled = false;
            drill.Enabled = false;
            isActive = false;
            actionInProgress = "";
            actionStage = "";
        }
        private void StartDrill()
        {
            Echo(String.Format("checking drill speed {0}", VerticalRetraction()));
            drill.Enabled = true;

            float speed = -SafeVerticalDrillSpeed();
            if (VerticalRetraction() < verticalClearance)
            {
                speed = -fastSpeed;
            }

            SetPistonsSpeed(verticalPistons, speed);
            SetPistonsEnabled(verticalPistons, true);
            SetPistonsEnabled(horizontalPistons, false);
            rotor.Enabled = false;
        }
        private void MoveHorizontalAndDrill(float speed, string echoPrefix)
        {
            Boolean fullVerticalRetraction = FullVerticalRetraction();
            Boolean fullVerticalExtension = FullVerticalExtension();
            Boolean reachedHorizontal = ReachedTargetHorizontalExtension(speed > 0);

            if (actionStage == "drill" && fullVerticalRetraction && reachedHorizontal)
            {
                Echo(String.Format("[{0}]: complete. shutting off...", echoPrefix));
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
                Echo(String.Format("[{0}]: horizontal to target {1}...", echoPrefix, targetHorizontalPosition));
                foreach (IMyPistonBase piston in horizontalPistons)
                {
                    Echo(String.Format("[{0}]: current position {0}...", echoPrefix, piston.CurrentPosition));
                }
                SetPistonsSpeed(horizontalPistons, speed);
                SetPistonsEnabled(horizontalPistons, true);
                return;
            }
            else
            {
                Echo(String.Format("[{0}]: drilling down at speed {1}...", echoPrefix, SafeVerticalDrillSpeed()));
                StartDrill();
                return;
            }
        }

        // positive speed is CW
        private void MoveAngleAndDrill(float rpm, string echoPrefix)
        {
            Boolean fullVerticalRetraction = FullVerticalRetraction();
            Boolean fullVerticalExtension = FullVerticalExtension();
            Boolean reachedAngle;
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
                Echo(String.Format("[{0}]: complete. shutting off...", echoPrefix));
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
                Echo(String.Format("[{0}]: current angle {1} to target {2}...", echoPrefix, rotor.Angle, targetAngle));
                rotor.TargetVelocityRPM = rpm;
                rotor.Enabled = true;
                return;
            }
            else
            {
                Echo(String.Format("[{0}]: drilling down at speed {1}...", echoPrefix, SafeVerticalDrillSpeed()));
                StartDrill();
                return;
            }
        }


        private void RetractVertical()
        {
            SetPistonsSpeed(verticalPistons, fastSpeed); // extension since it's reversed
            SetPistonsEnabled(verticalPistons, true);
        }

        private void SetPistonsEnabled(List<IMyPistonBase> pistons, Boolean enabled)
        {
            pistons.ForEach(piston => piston.Enabled = enabled);
        }

        private void SetPistonsSpeed(List<IMyPistonBase> pistons, float velocity)
        {
            pistons.ForEach(piston => piston.Velocity = velocity);
        }
    }
}
