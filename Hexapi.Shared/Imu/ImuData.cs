using System;

namespace Hexapi.Shared.Imu
{
    public class ImuData 
    {
        public ImuData(string[] values)
        {
            if (values.Length < 6) return;

            AccelX = Convert.ToDouble(values[0]);
            AccelY = Convert.ToDouble(values[1]);
            AccelZ = Convert.ToDouble(values[2]);

            Yaw = Convert.ToDouble(values[3]);
            Pitch = Convert.ToDouble(values[4]);
            Roll = Convert.ToDouble(values[5]);
        }

        public double AccelX { get; set; } = 0;
        public double AccelY { get; set; } = 0;
        public double AccelZ { get; set; } = 0;

        public double Yaw { get; set; } = 0;
        public double Pitch { get; set; } = 0;
        public double Roll { get; set; } = 0;
    }
}