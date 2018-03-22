using System;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

namespace Hexapi.Service.Hardware
{
    public class MaxbotixSonar
    {
        private SerialDevice _serialDevice;

        private DataReader _inputStream;

        private readonly ISubject<int> _sonarSyncSubject = new BehaviorSubject<int>(0);
        internal ISubject<int> SonarSubject { get; private set; }

        internal async Task InitializeAsync()
        {
            SonarSubject = Subject.Synchronize(_sonarSyncSubject);

            _serialDevice = await HexapiService.SerialDeviceHelper.GetSerialDeviceAsync("504WY", 57600, TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1000));

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
                var byteCount = await _inputStream.LoadAsync(12);

                var buffer = new byte[byteCount];

                _inputStream.ReadBytes(buffer);

                var readings = Encoding.ASCII.GetString(buffer);
                
                var inches = readings.Split('|');

                foreach (var inch in inches)
                {
                    if (string.IsNullOrEmpty(inch) || !inch.StartsWith(">") || !inch.EndsWith("<"))
                        continue;

                    try
                    {
                        SonarSubject.OnNext(Convert.ToInt32(inch.Replace(">","").Replace("<", "")));
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