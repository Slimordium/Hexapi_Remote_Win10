using Hexapi.Shared.Imu;

namespace Hexapi.Shared
{
    public class HexapiTelemetry
    {
        public ImuData ImuData { get; set; }

        public double LeftRange { get; set; }

        public double RightRange { get; set; }
    }
}