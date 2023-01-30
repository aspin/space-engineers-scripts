using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IngameScript
{
    class Config
    {
        // component naming prefix
        public string GroupPrefix = "Drill Rig";

        // component inversion configuration
        public bool InvertVertical = true; // default: false; extension = moving drill into ground
        public bool InvertHorizontal = false; // default: false; extension = moving drill further from base

        // distance from drill head to ground, e.g. how far drill can move with fast speed before touching grass
        public float VerticalClearance = 1.5f;

        // speed control parameters
        public float SafeVerticalDrillSpeed = 0.3f;
        public float SafeHorizontalSpeed = 0.5f;
        public float SafeAngleRPM = 1f;
        public float FastSpeed = 1f;

        // distance between each incremental movement of drill head
        public float HorizontalIncrement = 0.5f;
        public float AngleIncrement = 3f / 180f * (float)Math.PI;
    }
}
