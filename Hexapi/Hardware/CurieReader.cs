using System;
using System.IO.Ports;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hexapi.Shared;
using Hexapi.Shared.Imu;
using NLog;

namespace Hexapi.Service.Hardware
{
    public class CurieReader
    {
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        public ISubject<HexapiTelemetry> HexapiTelemetrySubject { get; set; } = new BehaviorSubject<HexapiTelemetry>(null);

        private readonly SerialPort _serialPort = new SerialPort("COM3", 9600, Parity.None, 8, StopBits.One);

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(_serialPort.Dispose);

            return Task.Run(() =>
            {
                try
                {
                    _serialPort.Open();

                    _serialPort.DtrEnable = true;
                }
                catch (Exception e)
                {
                    //
                }
                //>-0.76,0.14,0.29,-0.10,0.00,0.97

                while (!cancellationToken.IsCancellationRequested && _serialPort.IsOpen)
                {
                    var data = string.Empty;

                    while (!data.StartsWith(">") && !data.EndsWith('\r'))
                    {
                        try
                        {
                            data = _serialPort.ReadLine();
                        }
                        catch (Exception e)
                        {
                            _logger.Log(LogLevel.Error, $"Read IMU => {e.Message}");
                        }
                    }

                    try
                    {
                        var values = data.TrimStart('>').TrimEnd('\r').Split(',');

                        if (values.Length <7)
                            continue;

                        var telemetry = new HexapiTelemetry
                        {
                            ImuData = new ImuData(values),
                            LeftRange = Convert.ToDouble(values[6]),
                            RightRange = Convert.ToDouble(values[7])
                        };

                        HexapiTelemetrySubject.OnNext(telemetry);
                    }
                    catch (Exception e)
                    {
                        _logger.Log(LogLevel.Error, $"Read IMU parse => {e.Message}");
                    }
                }
            }, cancellationToken);
        }
    }
}