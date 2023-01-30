using Mixins;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameScript
{

    enum DrillAction
    {
        None,
        Stop,
        RetractAll,
    }

    class DrillState : IActionState
    {
        public readonly Config config;

        // components
        public readonly List<IMyPistonBase> horizontalPistons;
        public readonly List<IMyPistonBase> verticalPistons;
        public readonly IMyMotorAdvancedStator rotor;
        public readonly IMyShipDrill drill;

        // intended actions
        public float targetHorizontalPosition = 0;
        public float targetAngle = 0;

        public DrillState(List<IMyPistonBase> horizontalPistons, List<IMyPistonBase> verticalPistons, IMyMotorAdvancedStator rotor, IMyShipDrill drill, Config config)
        {
            this.horizontalPistons = horizontalPistons;
            this.verticalPistons = verticalPistons;
            this.rotor = rotor;
            this.drill = drill;
            this.config = config;
        }

        public bool IsComplete()
        {
            return rotor != null && drill != null && horizontalPistons.Count > 0 && verticalPistons.Count > 0;
        }

        public string Error()
        {
            return string.Format("required components missing: [rotor {0}] [drill {1}] [hPistons {2}] [vPistons {3}]", rotor == null, drill == null, horizontalPistons.Count, verticalPistons.Count);
        }

        // default: drill all the way into the ground; controlled by Config.InvertVertical
        internal bool FullVerticalExtension()
        {
            float factor = 1.;
            if (config.)
            return verticalPistons.Aggregate(
                true,
                (extended, piston) => extended && (piston.CurrentPosition == piston.HighestPosition)
            );
        }

        // how deep the drill is in the ground right now
        internal float VerticalRetraction()
        {
            return verticalPistons.Aggregate(
                0f,
                (sum, piston) => sum + (piston.HighestPosition - piston.CurrentPosition)
            );
        }

        // extension = all the way down, e.g. drill all the way in ground
        internal bool FullVerticalRetraction()
        {
            return verticalPistons.Aggregate(
                true,
                (extended, piston) => extended && (piston.CurrentPosition == piston.LowestPosition)
            );
        }

        // retraction = all the way in, e.g. drill closest to origin
        internal bool FullHorizontalRetraction()
        {
            return horizontalPistons.Aggregate(
                true,
                (sum, piston) => sum && (piston.CurrentPosition == piston.LowestPosition)
            );
        }

        // assume all pistons must be extended to same number
        internal bool ReachedTargetHorizontalExtension(bool greater)
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

        internal float SafeVerticalDrillSpeed()
        {
            return (float)(config.SafeVerticalDrillSpeed / (double)verticalPistons.Count);
        }


        internal void DisableAll()
        {
            SetPistonsEnabled(horizontalPistons, false);
            SetPistonsEnabled(verticalPistons, false);
            rotor.Enabled = false;
            drill.Enabled = false;
        }
        internal void SetPistonsEnabled(List<IMyPistonBase> pistons, bool enabled)
        {
            pistons.ForEach(piston => piston.Enabled = enabled);
        }

        internal void SetPistonsSpeed(List<IMyPistonBase> pistons, float velocity)
        {
            pistons.ForEach(piston => piston.Velocity = velocity);
        }
    }

    class DrillStateMachine : StateMachine<DrillAction, DrillState>
    {
        public DrillStateMachine(DrillState state) : base(Handlers(), state)
        {
        }

        private static Dictionary<DrillAction, ActionHandler<DrillState>> Handlers()
        {
            return new Dictionary<DrillAction, ActionHandler<DrillState>>
            {
                { DrillAction.Stop, new DrillActionStop() }
            };
        }

        public override DrillAction[] Actions()
        {
            return new DrillAction[] { DrillAction.Stop, DrillAction.RetractAll };
        }

        public override bool CanOverride(DrillAction action)
        {
            return action == DrillAction.Stop || action == DrillAction.RetractAll;
        }

        public override DrillAction EmptyAction()
        {
            return DrillAction.None;
        }
    }

    class DrillActionStop : ActionHandler<DrillState>
    {

        protected override Dictionary<int, ActionStageHandler<DrillState>> Handlers()
        {
            return new Dictionary<int, ActionStageHandler<DrillState>>
            {
                { 0, DoThing }
            };
        }

        private int DoThing(int stage, DrillState state)
        {
            return 1;
        }
    }

    class DrillActionRetractAll : ActionHandler<DrillState>
    {
        protected override Dictionary<int, ActionStageHandler<DrillState>> Handlers()
        {
            return new Dictionary<int, ActionStageHandler<DrillState>>
            {
                { 0, DoThing }
            };
        }

        private int DoThing(int stage, DrillState state)
        {
            return 1;
        }
    }

}
