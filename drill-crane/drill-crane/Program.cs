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
        readonly string prefix = "Drill";
        readonly List<IMyPistonBase> horizontalPistons;
        readonly List<IMyPistonBase> verticalPistons;
        readonly IMyMotorAdvancedStator rotor;
        readonly IMyShipDrill drill;

        Boolean isActive = false;
        string actionInProgress;

        // TODO: add config for amount that is safe to go quickly vertically

        float safeVerticalDrillSpeed = 0.3f;
        float safeHorizontalSpeed = 0.5f;
        float fastSpeed = 1f;
        float safeAngleRPM = 1f;

        float horizontalIncrement = 0.5f;
        float angleIncrement = 3f/180f * (float) Math.PI;

        // assume all pistons extend uniformly
        float targetHorizontalPosition;
        float targetAngle;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;

            horizontalPistons = new List<IMyPistonBase>();
            verticalPistons = new List<IMyPistonBase>();

            List<IMyPistonBase> pistons = new List<IMyPistonBase>();
            GridTerminalSystem.GetBlocksOfType(pistons);

            foreach (IMyPistonBase piston in pistons)
            {
                if (piston.CustomName == String.Format("{0} Piston H", prefix))
                {
                    horizontalPistons.Add(piston);

                }
                else if (piston.CustomName == String.Format("{0} Piston V", prefix))
                {
                    verticalPistons.Add(piston);

                }
            }

            rotor = GridTerminalSystem.GetBlockWithName(String.Format("{0} Rotor", prefix)) as IMyMotorAdvancedStator;
            drill = GridTerminalSystem.GetBlockWithName(String.Format("{0} Drill", prefix)) as IMyShipDrill;

        }

        public void Main(string argument, UpdateType updateSource)
        {
            // short circuit conditions
            if (rotor == null || drill == null || horizontalPistons.Count == 0 || verticalPistons.Count == 0)
            {
                Echo("no rotor or drill found");
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
                    drill.Enabled = true;
                    RetractVertical();

                    isActive = true;
                    targetHorizontalPosition = horizontalPistons[0].CurrentPosition - horizontalIncrement;
                    Echo(String.Format("[retract]: target horizontal position {0}", targetHorizontalPosition));
                    break;
                case "extendAndDrill":
                    drill.Enabled = true;
                    RetractVertical();

                    isActive = true;
                    targetHorizontalPosition = horizontalPistons[0].CurrentPosition + horizontalIncrement;
                    Echo(String.Format("[extend]: target horizontal position {0}", targetHorizontalPosition));
                    break;
                case "rotateCCWAndDrill":
                    drill.Enabled = true;
                    RetractVertical();

                    isActive = true;
                    targetAngle = rotor.Angle - angleIncrement;
                    Echo(String.Format("[ccw]: target angle {0}", targetAngle));
                    break;
                case "rotateCWAndDrill":
                    drill.Enabled = true;
                    RetractVertical();

                    isActive = true;
                    targetAngle = rotor.Angle + angleIncrement;
                    Echo(String.Format("[cw]: target angle {0}", targetAngle));
                    break;
                default:
                    Echo(String.Format("unknown action: {0}", action));
                    break;
            }

            if (isActive)
            {
                Runtime.UpdateFrequency = UpdateFrequency.Update100;
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

                    if (fullVerticalExtension && fullHorizontalRetraction)
                    {
                        isActive = false;
                        DisableAll();
                        break;
                    }

                    if (fullVerticalExtension)
                    {
                        SetPistonsEnabled(verticalPistons, false);
                        SetPistonsSpeed(horizontalPistons, -fastSpeed);
                        SetPistonsEnabled(horizontalPistons, true);
                        rotor.TargetVelocityRPM = 100f; // TODO: rotate all the way positive
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

        private void MoveHorizontalAndDrill(float speed, string echoPrefix)
        {
            Boolean fullVerticalRetraction = FullVerticalRetraction();
            Boolean reachedHorizontal = ReachedTargetHorizontalExtension(speed > 0);

            if (fullVerticalRetraction && reachedHorizontal)
            {
                Echo(String.Format("[{0}]: complete. shutting off...", echoPrefix));
                DisableAll();
                return;
            }
            // TODO: need safe condition where dont start moving horizontally until full vertical extended

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

        private void StartDrill()
        {
            SetPistonsSpeed(verticalPistons, -SafeVerticalDrillSpeed());
            SetPistonsEnabled(verticalPistons, true);
            SetPistonsEnabled(horizontalPistons, false);
            rotor.Enabled = false;
        }

        // extension = all the way up, e.g. drill out of ground
        private Boolean FullVerticalExtension()
        {
            return verticalPistons.Aggregate(
                true,
                (extended, piston) => extended && (piston.CurrentPosition == piston.HighestPosition)
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
            return (float) (safeVerticalDrillSpeed / (double) verticalPistons.Count);
        }

        private void DisableAll()
        {
            SetPistonsEnabled(horizontalPistons, false);
            SetPistonsEnabled(verticalPistons, false);
            rotor.Enabled = false;
            drill.Enabled = false;
            isActive = false;
            actionInProgress = "";
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
