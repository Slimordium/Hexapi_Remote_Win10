using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Hexapi.Shared.Ik;
using Hexapi.Shared.Ik.Enums;

namespace Hexapi.Service.IK
{
    /// <summary>
    ///     This is a port of the "Phoenix" 3DOF Hexapod code in C#. Uses CH3-R body from Lynxmotion/robotshop.com
    ///     https://github.com/KurtE/Arduino_Phoenix_Parts/tree/master/Phoenix
    ///     http://www.robotshop.com/en/lynxmotion-aexapod-ch3-r-combo-kit-body-frame.html
    /// </summary>
    internal class InverseKinematics
    {
        private static DataReader _inputStream;
        private static DataWriter _outputStream;

        private static readonly IkMath _ikMath = new IkMath();
        private static readonly byte[] _querySsc = {0x51, 0x0d}; //0x51 = Q, 0x0d = carriage return
        
        private static SerialDevice _serialDevice;

        private bool _movementEnabled;

        private double _bodyPosX;
        private double _bodyPosY = 42; //Controls height of the body from the ground
        private double _bodyPosZ;

        private double _bodyRotX; //Global Input pitch of the body
        private double _bodyRotY; //Global Input rotation of the body
        private double _bodyRotZ; //Global Input roll of the body

        private static GaitType _gaitType = GaitType.TripleTripod16;
        private static double _gaitSpeedInMs = 60; //Nominal speed of the gait in ms

        private double _travelLengthX; //Current Travel length X - Left/Right
        private double _travelLengthZ; //Current Travel length Z - Negative numbers = "forward" movement.
        private double _travelRotationY; //Current Travel Rotation Y 
        private double _legLiftHeight;

        private IDisposable _ikDisposable; //Without this, the subscription may be garbage collected

        internal IObservable<IkParams> IkObservable { private get; set; }

        internal async Task InitializeAsync()
        {
            //AI03GJKCA
            _serialDevice = await HexapiService.SerialDeviceHelper.GetSerialDeviceAsync("AI03GJKCA", 115200, TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1000)); 

            if (_serialDevice == null)
                return;

            _inputStream = new DataReader(_serialDevice.InputStream);
            _outputStream = new DataWriter(_serialDevice.OutputStream);

            GaitSelect(GaitType.Tripod8);
        }

        private void Subscribe()
        {
            _ikDisposable = IkObservable.Subscribe(ik =>
            {
                if (ik == null)
                    return;

                GaitSelect(ik.GaitType);

                _travelLengthX = ik.LengthX;
                _travelLengthZ = ik.LengthZ;

                _legLiftHeight = ik.LegLiftHeight;

                _gaitSpeedInMs = ik.GaitSpeedMs;
                _travelRotationY = ik.RotationY;

                _bodyRotX = ik.BodyRotationX;
                _bodyRotY = ik.BodyRotationY;
                _bodyRotZ = ik.BodyRotationZ;

                _bodyPosX = ik.BodyPositionX;
                _bodyPosY = ik.BodyPositionY;
                _bodyPosZ = ik.BodyPositionZ;

                _movementEnabled = ik.MovementEnabled;
            });
        }

        internal async Task StartAsync(CancellationToken cancellationToken)
        {
            while (_inputStream == null || _outputStream == null)
            {
                await Task.Delay(500, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                    return;
            }

            Subscribe();

            while (!cancellationToken.IsCancellationRequested)
            {
                if (_movementEnabled)
                {
                    await WriteAndWaitForResponse(_ikMath.CalculateServoPositions(_gaitSpeedInMs, 
                                                                                    _travelLengthX, _travelLengthZ, _travelRotationY, 
                                                                                    _legLiftHeight, 
                                                                                    _bodyPosX, _bodyPosY, _bodyPosZ, _bodyRotX, _bodyRotZ, _bodyRotY));
                }
                else
                {
                    await WriteAndWaitForResponse(IkMath.AllServosOff);
                }
            }

            _ikDisposable?.Dispose();
        }

        private static async Task<bool> WriteAndWaitForResponse(string toWrite)
        {
            _outputStream.WriteString(toWrite);

            var bw = await _outputStream.StoreAsync();

            if (bw == 0)
                return false;

            while (true)
            {
                var bytesIn = await _inputStream.LoadAsync(1);
                if (bytesIn > 0)
                {
                    if (_inputStream.ReadByte() == 0x2e)
                        break;
                }
                _outputStream.WriteBytes(_querySsc);
                var bytesOut = await _outputStream.StoreAsync();
            }

            return true;
        }

        private static void GaitSelect(GaitType gaitType)
        {
            if (_gaitType == gaitType)
                return;

            _gaitType = gaitType;

           _ikMath.GaitSelect(gaitType);
        }
    }
}