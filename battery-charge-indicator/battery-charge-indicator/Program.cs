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
        string prefix = "UM";

        List<IMyBatteryBlock> batteries;
        IMyTextSurface screen;

        public Program()
        {

            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            batteries = new List<IMyBatteryBlock>();

            IMyBatteryBlock battery = getBattery(prefix, 1);
            while (battery != null)
            {
                batteries.Add(battery);
                battery = getBattery(prefix, batteries.Count + 1);
            }

            IMyCockpit cockpit = GridTerminalSystem.GetBlockWithName(String.Format("{0} Cockpit", prefix)) as IMyCockpit;
            if (cockpit != null)
            {
                screen = cockpit.GetSurface(0);
                screen.ContentType = ContentType.TEXT_AND_IMAGE;
            }
        }


        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Echo(String.Format("checking status of {0} batteries and writing to cockpit: {1}", batteries.Count, screen != null));

            float currentCharge = batteries.AsEnumerable().Aggregate(0.0f, (sum, b) => b.CurrentStoredPower + sum);
            float totalCharge = batteries.AsEnumerable().Aggregate(0.0f, (sum, b) => b.MaxStoredPower + sum);
            Decimal percent = Math.Round((Decimal)(currentCharge / totalCharge * 100), 2);

            if (screen != null)
            {
                screen.WriteText(String.Format("Batteries:\n {0}%", percent), false);
            }
        }


        private IMyBatteryBlock getBattery(string prefix, int count)
        {
            string batteryName = String.Format("{0} Battery {1}", prefix, count);

            IMyBatteryBlock battery;
            battery = GridTerminalSystem.GetBlockWithName(batteryName) as IMyBatteryBlock;
            return battery;
        }
    }
} 