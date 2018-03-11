using System;
using System.IO;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Hexapi.Shared.Imu;

namespace Hexapi.Service.Hardware
{
    public class RazorImu
    {
        private SerialDevice _serialDevice;

        private DataReader _inputStream;

        private readonly ISubject<ImuData> _imuDataSyncSubject = new BehaviorSubject<ImuData>(new ImuData());
        internal ISubject<ImuData> ImuDataSubject { get; private set; } 

        internal async Task InitializeAsync()
        {
            ImuDataSubject = Subject.Synchronize(_imuDataSyncSubject);

            _serialDevice = await HexapiService.SerialDeviceHelper.GetSerialDeviceAsync("N01E09J", 57600, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));

            if (_serialDevice == null)
                return;

            _inputStream = new DataReader(_serialDevice.InputStream) { InputStreamOptions = InputStreamOptions.Partial };
        }

        internal async Task StartAsync(CancellationToken cancellationToken)
        {
            while (_inputStream == null)
            {
                await Task.Delay(500);
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                var byteCount = await _inputStream.LoadAsync(64);

                var buffer = new byte[byteCount];

                _inputStream.ReadBytes(buffer);

                var readings = Encoding.ASCII.GetString(buffer);

                var yprReadings = readings.Replace("\r", "").Replace("\n", "").Replace("YPR=", "").Split('#');

                foreach (var reading in yprReadings)
                {
                    if (string.IsNullOrEmpty(reading))
                        continue;

                    try
                    {
                        var splitYpr = reading.Split(',');

                        if (splitYpr.Length != 3)
                            continue;

                        var imuData = new ImuData
                        {
                            Yaw = Convert.ToDouble(splitYpr[0]),
                            Pitch = Convert.ToDouble(splitYpr[1]),
                            Roll = Convert.ToDouble(splitYpr[2])
                        };

                        ImuDataSubject.OnNext(imuData);
                    }
                    catch (Exception e)
                    {
                        //
                    }
                        
                }
                
            }
        }

    }
}