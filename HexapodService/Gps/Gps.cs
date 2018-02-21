using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

namespace Hexapod.Hardware
{
    internal sealed class Gps
    {
        private SerialDevice _gpsSerialDevice;

        private DataReader _inputStream;
        private DataWriter _outputStream;

        private readonly ISubject<GpsFixData> _gpsFixSubject = new BehaviorSubject<GpsFixData>(new GpsFixData());


        internal async Task InitializeAsync()
        {
            _gpsSerialDevice = await HexapodService.SerialDeviceHelper.GetSerialDeviceAsync("AH03F3RYA", 57600, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));

            if (_gpsSerialDevice == null)
                return;

            _inputStream = new DataReader(_gpsSerialDevice.InputStream) {InputStreamOptions = InputStreamOptions.Partial};
            _outputStream = new DataWriter(_gpsSerialDevice.OutputStream);
        }

        internal IObservable<GpsFixData> GetObservable()
        {
            var observable = _gpsFixSubject.AsObservable();

            return observable;
        }

        internal async Task StartAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_inputStream == null)
                {
                    await Task.Delay(500);
                    continue;
                }

                var byteCount = await _inputStream.LoadAsync(1024);
                var bytes = new byte[byteCount];
                _inputStream.ReadBytes(bytes);

                var sentences = Encoding.ASCII.GetString(bytes).Split('\n');

                if (sentences.Length == 0)
                    continue;

                foreach (var sentence in sentences)
                {
                    if (!sentence.StartsWith("$"))
                        continue;

                    var data = sentence.ParseNmea();

                    if (data == null)
                        continue;

                    _gpsFixSubject.OnNext(data);
                }
            }
        }

        internal async Task StartRtkUdpFeedAsync()
        {
            var receivingUdpClient = new UdpClient(8000);

            while (true)
            {
                var udpReceiveResult = await receivingUdpClient.ReceiveAsync();
                _outputStream.WriteBytes(udpReceiveResult.Buffer);
                await _outputStream.StoreAsync();
            }
        }

        //private Timer _statusTimer = new Timer(async sender =>
        //{
        //    if (CurrentGpsFixData == null)
        //        return;

        //    await _display.WriteAsync($"{CurrentGpsFixData.Quality} S{CurrentGpsFixData.SatellitesInView}", 1);
        //    await _display.WriteAsync($"RR{CurrentGpsFixData.RtkRatio}, RA{CurrentGpsFixData.RtkAge}, SNR{CurrentGpsFixData.SignalToNoiseRatio}", 2);

        //    //Debug.WriteLine($"{CurrentGpsFixData.Quality} fix, satellites {CurrentGpsFixData.SatellitesInView}");
        //    //Debug.WriteLine($"{CurrentGpsFixData.Lat},{CurrentGpsFixData.Lon}");
        //    //Debug.WriteLine($"Rtk ratio {CurrentGpsFixData.RtkRatio}, rtk age {CurrentGpsFixData.RtkAge}, SNR {CurrentGpsFixData.SignalToNoiseRatio}");
        //    //Debug.WriteLine($"{byteCount}");
        //    //byteCount = 0;
        //}, null, 0, 3000);
    }
}