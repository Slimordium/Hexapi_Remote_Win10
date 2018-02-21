using Hexapod.Enums.Xbox;
using System.Collections.Generic;

namespace Hexapod.Hardware
{
    public class XboxEvent
    {
        public XboxAnalog RightStick { get; set; }

        public XboxAnalog LeftStick { get; set; }

        public double RightTrigger { get; set; }

        public double LeftTrigger { get; set; }

        public Direction Dpad { get; set; }

        public IEnumerable<FunctionButton> FunctionButtons { get; set; }
    }

    public class XboxAnalog
    {
        public Direction Direction { get; set; } = Direction.None;

        public double Magnitude;
    }

}