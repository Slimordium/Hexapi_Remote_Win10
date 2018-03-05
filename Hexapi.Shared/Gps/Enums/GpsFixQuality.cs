using System;
// ReSharper disable InconsistentNaming

namespace Hexapi.Shared.Gps.Enums
{
    [Flags]
    public enum GpsFixQuality
    {
        NoFix,
        StandardGps,
        DiffGps,
        PPS,
        RTK,
        FloatRTK,
        Estimated,
        Manual,
        Simulation
    }
}