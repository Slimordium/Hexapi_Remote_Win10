using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Hexapi.Shared.Ik;

namespace Hexapi.Service.IK
{
    /// <summary>
    /// Sanitizes travel requests
    /// </summary>
    internal class IkFilter
    {
        private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private readonly InverseKinematics _inverseKinematics = new InverseKinematics();

        private int _perimeterInInches = 20;

        private double _leftInches;
        private double _farLeftInches;
        private double _centerInches;
        private double _rightInches;
        private double _farRightInches;


        private double _yaw;
        private double _pitch;
        private double _roll;

        private double _accelX;
        private double _accelY;
        private double _accelZ;

        private readonly ISubject<IkParams> _ikSubject = new BehaviorSubject<IkParams>(null);

        private IkParams _ikParamsOverride = new IkParams(true);

        internal IObservable<IkParams> IkObservable { private get; set; }

        internal ISubject<IkParams> IkRemoteSubject { get; } = new BehaviorSubject<IkParams>(null);

        private IObservable<IkParams> _ikOverrideObservable;

        private readonly Stopwatch _oscillateStopwatch = new Stopwatch();

        private IDisposable _ikDisposable;

        private readonly object _lock = new object();
        private double _bodyRotZ;
        private double _bodyRotX;
        private int _restAccelY;
        private int _oscillations;
        private int _travelLengthZ;
        private GpioController _gpioController;
        private List<GpioPin> _legGpioPins = new List<GpioPin>();

        internal async Task InitializeAsync()
        {
            await _inverseKinematics.InitializeAsync();
        }

        internal Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _inverseKinematics.IkObservable = _ikSubject.AsObservable();

            if (IkObservable != null)
                _ikDisposable = IkObservable.Subscribe(OnNextIkParams);

            if (IkRemoteSubject == null)
                return _inverseKinematics.StartAsync(_cancellationTokenSource.Token);

            _ikOverrideObservable = IkRemoteSubject.AsObservable();
            _ikOverrideObservable.Subscribe(OnNextIkRemote);

            return _inverseKinematics.StartAsync(_cancellationTokenSource.Token);
        }

        /// <summary>
        /// This is where incoming travel requests will be validated. Is there something in the travel path, adjust based on IMU, adjust for other settings...
        /// </summary>
        /// <param name="ikParams"></param>
        private void OnNextIkParams(IkParams ikParams)
        {
            if (ikParams == null || _cancellationTokenSource.IsCancellationRequested)
                return;

            //if (_ikParamsOverride.GaitSpeedMs > 0)
            //    ikParams.GaitSpeedMs = _ikParamsOverride.GaitSpeedMs;

            //if (_ikParamsOverride.LegLiftHeight > 0)
            //    ikParams.LegLiftHeight = _ikParamsOverride.LegLiftHeight;

            //if (_ikParamsOverride.GaitType != GaitType.None)
            //    ikParams.GaitType = _ikParamsOverride.GaitType;

            _ikSubject.OnNext(ikParams);
        }

        private void OnNextIkRemote(IkParams ikParams)
        {
            if (ikParams == null || _cancellationTokenSource.IsCancellationRequested)
                return;

            //_ikParamsOverride = ikParams;

            _ikSubject.OnNext(ikParams);
        }

        //private async void ImuEventHandler(object sender, ImuDataEventArgs e)
        //{
        //    //Pitch/Roll correction
        //    var newBodyRotZ = 0d;
        //    var newBodyRotX = 0d;

        //    if (e.Roll < 0)
        //        e.Roll = e.Roll + 7; //7 is the error in degrees of the Roll calculation from the Razor
        //    else
        //        e.Roll = e.Roll - 7;

        //    if (e.Roll < 0)
        //        newBodyRotZ = Math.Round(e.Roll.Map(0, 25, 0, 10), 2);
        //    else
        //        newBodyRotZ = -Math.Round(e.Roll.Map(0, 25, 0, 10), 2);

        //    if (newBodyRotZ < -10)
        //        newBodyRotZ = -10;

        //    if (newBodyRotZ > 10)
        //        newBodyRotZ = 10;

        //    if (e.Pitch < 0)
        //        e.Pitch = e.Pitch + 3; //3 is the error in degrees of the Pitch calculation from the Razor
        //    else
        //        e.Pitch = e.Pitch - 3;

        //    if (e.Pitch < 0)
        //        newBodyRotX = Math.Round(e.Pitch.Map(0, 25, 0, 10), 2);
        //    else
        //        newBodyRotX = -Math.Round(e.Pitch.Map(0, 25, 0, 10), 2);

        //    if (newBodyRotX < -10)
        //        newBodyRotX = -10;

        //    if (newBodyRotX > 10)
        //        newBodyRotX = 10;

        //    _bodyRotZ = newBodyRotZ;
        //    _bodyRotX = newBodyRotX;
        //    //*********************

        //    //oscillation correction
        //    //if (_travelRequest)
        //    //    _oscillations = 0;

        //    if (e.AccelY < _restAccelY - 13 || e.AccelY > _restAccelY + 13)
        //        _oscillations++;

        //    if (_oscillateStopwatch.ElapsedMilliseconds > 10000)
        //    {
        //        _oscillations = 0;
        //        _oscillateStopwatch.Restart();
        //    }

        //    if (_oscillateStopwatch.ElapsedMilliseconds <= 2000 || !(_oscillations > 9))
        //        return;

        //    _travelLengthZ = -1;

        //    await Task.Delay(900).ContinueWith(t =>
        //    {
        //        _travelLengthZ = 0;
        //        _oscillations = 0;
        //        _oscillateStopwatch.Restart();
        //    }).ConfigureAwait(false);
        //    //*********************
        //}

        private async void ConfigureFootSwitches()
        {
            try
            {
                _gpioController = GpioController.GetDefault();
            }
            catch (Exception e)
            {
                //await _display.WriteAsync(e.Message);
                return;
            }

            if (_gpioController != null)
            {
                _legGpioPins[0] = _gpioController.OpenPin(26);
                _legGpioPins[1] = _gpioController.OpenPin(19);
                _legGpioPins[2] = _gpioController.OpenPin(13);
                _legGpioPins[3] = _gpioController.OpenPin(16);
                _legGpioPins[4] = _gpioController.OpenPin(20);
                _legGpioPins[5] = _gpioController.OpenPin(21);

                foreach (var legGpioPin in _legGpioPins)
                {
                    legGpioPin.DebounceTimeout = new TimeSpan(0, 0, 0, 0, 1);
                    legGpioPin.SetDriveMode(GpioPinDriveMode.InputPullUp);
                }
            }
            else
            {
                //await _display.WriteAsync("No Gpio?");
            }
        }

        //internal void CalibrateFootHeight()
        //{
        //    _selectedFunction = SelectedIkFunction.SetFootHeightOffset;

        //    Task.Factory.StartNew(() =>
        //    {
        //        var height = 0;

        //        for (var i = 0; i < 6; i++)
        //        {
        //            while (_legGpioPins[i].Read() != GpioPinValue.Low)
        //            {
        //                height += 2;

        //                RequestLegYHeight(i, height);
        //            }
        //        }

        //        _calibrated = true;
        //    });
        //}

        //internal void RequestLegYHeight(int leg, double yPos)
        //{
        //    _selectedFunctionLeg = leg;

        //    LegYHeightCorrector[leg] = yPos;
        //}

        //The idea here, is that if a foot hits an object, the corrector is set to the negative value of the current foot height,
        //then for that leg, the body height is adjusted accordingly. 
        //So if a foot is half-way to the floor when it contacts something, it would adjust the body height by half for that leg.
        //Not event sure if this will work!
        //The value will be stored in LegYHeightCorrector
        //IK Calculations will need to be modified to use this.
        //internal void RequestSaveLegYHeightCorrector()
        //{
        //    LegYHeightCorrector[_selectedFunctionLeg] = _bodyPosY - _lastBodyPosY;
        //}

        internal void Parse(string data)
        {
            lock (_lock)
            try
            {

                if (!data.EndsWith("\n"))
                    return;

                var newData = data.Replace("L", string.Empty).Replace("C", string.Empty).Replace("R", string.Empty).Replace("\n", string.Empty);

                double ping;

                if (!double.TryParse(newData, out ping))
                    return;

                if (ping <= 1)
                    return;

                var inches = GetInchesFromPingDuration(ping);

                if (data.StartsWith("R"))
                {
                    _rightInches = inches;
                }
                else if (data.StartsWith("C"))
                {
                    _centerInches = inches;
                }
                else if (data.StartsWith("L"))
                {
                    _leftInches = inches;
                }
            }
            catch
            {
                //
            }
        }

        private static double GetInchesFromPingDuration(double duration) //73.746 microseconds per inch
        {
            return Math.Round(duration / 73.746 / 2, 1);
        }
    }
}