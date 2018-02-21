using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Hexapod.Helpers;
using Hexapod.Enums;
using Hexapod.Enums.Xbox;
using Hexapod.Hardware;
using Hexapod.IK;
using Hexapod.Navigation;

//using HexapiBackground.Iot;

// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace Hexapod
{
    public class XboxController
    {


        private GaitType _gaitType = GaitType.Tripod8;

        private SelectedIkFunction _selectedIkFunction = SelectedIkFunction.GaitSpeed;
        private SelectedGpsFunction _selectedGpsFunction = SelectedGpsFunction.GpsDisabled;

        private readonly Stopwatch _functionStopwatch = new Stopwatch();//Basically a "debounce"
        private readonly Stopwatch _dpadStopwatch = new Stopwatch();//Basically a "debounce"

        private readonly XboxDevice _xboxDevice = new XboxDevice();

        public bool IsConnected => _xboxDevice.IsConnected;

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public XboxController()
        {
            //_ik = ikFilter;
            //_xboxController = xboxController;
            //_display = display;

            //_functionStopwatch.Start();
            //_dpadStopwatch.Start();


        }

        private IkLimits _ikLimits = new IkLimits();

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await _xboxDevice.InitializeAsync(cancellationToken);

            _xboxDevice.GetObservable().Subscribe(XboxDeviceEvent);
        }

        public ISubject<IkParams> IkParamSubject { get; set; } = new BehaviorSubject<IkParams>(new IkParams());

        private bool _movementEnabled;

        private void XboxController_DisconnectedEvent(object sender, ConnectionEventArgs e)
        {
            IkParamSubject.OnNext(new IkParams{MovementEnabled = e.IsConnected});
        }

        internal void XboxDeviceEvent(XboxEvent xboxEvent)
        {
            if (xboxEvent == null) return;

            var ikParams = GetIkParams(xboxEvent);

            if (xboxEvent.FunctionButtons.Contains(FunctionButton.Start))
            {
                if (_movementEnabled)
                    _movementEnabled = false;
                else
                    _movementEnabled = true;
            }

            ikParams.MovementEnabled = _movementEnabled;

            IkParamSubject.OnNext(ikParams);
        }

        //internal async void DpadDirectionChanged(XboxEvent xboxEvent)
        //{


        //    switch (sender.Action)
        //    {
        //        case EventType.Left:

        //            switch (_selectedIkFunction)
        //            {
        //                //case SelectedIkFunction.Behavior:
        //                //    _selectedBehavior--;

        //                //    if ((int)_selectedBehavior <= 0)
        //                //        _selectedBehavior = 0;

        //                //    await _display.WriteAsync($"{Enum.GetName(typeof(Behavior), _selectedBehavior)}", 2);
        //                //    break;
        //                case SelectedIkFunction.GaitType:
        //                    if (_gaitType > 0)
        //                        _gaitType--;

        //                    await _display.WriteAsync(Enum.GetName(typeof(GaitType), _gaitType), 2);
        //                    SetGaitOptions();

        //                    _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
        //                    _ik.RequestSetGaitType(_gaitType);
        //                    break;
        //                case SelectedIkFunction.GaitSpeed:
        //                    _gaitSpeed = _gaitSpeed - 2;
        //                    if (_gaitSpeed < GaitSpeedMin)
        //                        _gaitSpeed = GaitSpeedMin;
        //                    _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
        //                    await _display.WriteAsync($"_gaitSpeed = {_gaitSpeed}", 2);
        //                    break;
        //                //case SelectedIkFunction.SetFootHeightOffset:
        //                //    _selectedLeg--;
        //                //    if (_selectedLeg < 0)
        //                //        _selectedLeg = 0;
        //                //    await _display.WriteAsync($"_selectedLeg = {_selectedLeg}", 2);
        //                //    break;
        //            }
        //            break;
        //        case EventType.Right:
        //            switch (_selectedIkFunction)
        //            {
        //                //case SelectedIkFunction.Behavior:
        //                //    _selectedBehavior++;

        //                //    if ((int)_selectedBehavior > 4)
        //                //        _selectedBehavior = (Behavior)4;

        //                //    await _display.WriteAsync($"{Enum.GetName(typeof(Behavior), _selectedBehavior)}", 2);
        //                //    break;
        //                case SelectedIkFunction.GaitType:

        //                    if ((int)_gaitType < 4)
        //                        _gaitType++;

        //                    await _display.WriteAsync(Enum.GetName(typeof(GaitType), _gaitType), 2);
        //                    SetGaitOptions();
        //                    _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
        //                    _ik.RequestSetGaitType(_gaitType);
        //                    break;
        //                case SelectedIkFunction.GaitSpeed:
        //                    _gaitSpeed = _gaitSpeed + 2;
        //                    if (_gaitSpeed > GaitSpeedMax)
        //                        _gaitSpeed = GaitSpeedMax;
        //                    _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
        //                    await _display.WriteAsync($"_gaitSpeed = {_gaitSpeed}", 2);
        //                    break;
        //                //case SelectedIkFunction.SetFootHeightOffset:
        //                //    _selectedLeg++;
        //                //    if (_selectedLeg == 5)
        //                //        _selectedLeg = 5;

        //                //    await _display.WriteAsync($"_selectedLeg = {_selectedLeg}", 2);
        //                //    break;
        //            }
        //            break;
        //        case EventType.Up:
        //            switch (_selectedIkFunction)
        //            {
        //                case SelectedIkFunction.LegLiftHeight:
        //                    _legLiftHeight++;

        //                    if (_legLiftHeight > LegLiftHeightUpperLimit)
        //                        _legLiftHeight = LegLiftHeightUpperLimit;

        //                    await _display.WriteAsync($"Height = {_legLiftHeight}", 2);

        //                    _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
        //                    break;
        //                //case SelectedIkFunction.SetFootHeightOffset:
        //                //    _legPosY = _legPosY + 1;
        //                //    _ik.RequestLegYHeight(_selectedLeg, _legPosY);
        //                //    break;
        //                case SelectedIkFunction.PingSetup:
        //                    _ik.RequestNewPerimeter(true);
        //                    break;
        //                case SelectedIkFunction.BodyHeight:
        //                    _bodyPosY = _bodyPosY + 5;
        //                    if (_bodyPosY > 110)
        //                        _bodyPosY = 110;
        //                    _ik.RequestBodyPosition(_bodyRotX, _bodyRotZ, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY);
        //                    await _display.WriteAsync($"_bodyPosY = {_bodyPosY}", 2);
        //                    break;
        //                //case SelectedIkFunction.Posture:
        //                //    _posture++;
        //                //    await SetPosture();
        //                //    break;
        //                //case SelectedIkFunction.Behavior:
        //                //    _ik.RequestBehavior(_selectedBehavior, true);
        //                //    await _display.WriteAsync($"{nameof(_selectedBehavior)} start");
        //                //    break;
        //            }
        //            break;
        //        case EventType.Down:
        //            switch (_selectedIkFunction)
        //            {
        //                case SelectedIkFunction.LegLiftHeight:
        //                    _legLiftHeight--;

        //                    if (_legLiftHeight < LegLiftHeightLowerLimit)
        //                        _legLiftHeight = LegLiftHeightLowerLimit;

        //                    await _display.WriteAsync($"Height = {_legLiftHeight}", 2);

        //                    _ik.RequestSetGaitOptions(_gaitSpeed, _legLiftHeight);
        //                    break;
        //                //case SelectedIkFunction.SetFootHeightOffset:
        //                //    _legPosY = _legPosY - 1;
        //                //    _ik.RequestLegYHeight(_selectedLeg, _legPosY);
        //                //    break;
        //                case SelectedIkFunction.PingSetup:
        //                    _ik.RequestNewPerimeter(false);
        //                    break;
        //                case SelectedIkFunction.BodyHeight:
        //                    _bodyPosY = _bodyPosY - 5;
        //                    if (_bodyPosY < 10)
        //                        _bodyPosY = 10;
        //                    _ik.RequestBodyPosition(_bodyRotX, _bodyRotZ, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY);
        //                    await _display.WriteAsync($"_bodyPosY = {_bodyPosY}", 2);
        //                    break;
        //                //case SelectedIkFunction.Posture:
        //                //    _posture--;
        //                //    await SetPosture();
        //                //    break;
        //                //case SelectedIkFunction.Behavior:
        //                //    _ik.RequestBehavior(_selectedBehavior, false);
        //                //    await _display.WriteAsync($"{nameof(_selectedBehavior)} stop");
        //                //    break;
        //            }
        //            break;
        //    }
        //}

        private IkParams GetIkParams(XboxEvent xboxEvent)
        {
            var ikParams = new IkParams();

            var rightStick = xboxEvent.RightStick;
            var leftStick = xboxEvent.LeftStick;

            switch (rightStick.Direction)
            {
                case Direction.Left:
                    ikParams.RotationY = -rightStick.Magnitude.Map(0, 10000, 0, _ikLimits.TravelRotationYlimit);
                    break;
                case Direction.UpLeft:
                    ikParams.RotationY = -rightStick.Magnitude.Map(0, 10000, 0, _ikLimits.TravelRotationYlimit);
                    ikParams.LengthZ = -rightStick.Magnitude.Map(0, 10000, 0, _ikLimits.TravelLengthZlowerLimit);
                    break;
                case Direction.DownLeft:
                    ikParams.RotationY = -rightStick.Magnitude.Map(0, 10000, 0, _ikLimits.TravelRotationYlimit);
                    ikParams.LengthZ = rightStick.Magnitude.Map(0, 10000, 0, _ikLimits.TravelLengthZlowerLimit); //110
                    break;
                case Direction.Right:
                    ikParams.RotationY = rightStick.Magnitude.Map(0, 10000, 0, _ikLimits.TravelRotationYlimit); //3
                    break;
                case Direction.UpRight:
                    ikParams.RotationY = rightStick.Magnitude.Map(0, 10000, 0, _ikLimits.TravelRotationYlimit);
                    ikParams.LengthZ = -rightStick.Magnitude.Map(0, 10000, 0, _ikLimits.TravelLengthZupperLimit); //190
                    break;
                case Direction.DownRight:
                    ikParams.RotationY = rightStick.Magnitude.Map(0, 10000, 0, _ikLimits.TravelRotationYlimit);
                    ikParams.LengthZ = rightStick.Magnitude.Map(0, 10000, 0, _ikLimits.TravelLengthZlowerLimit);
                    break;
                case Direction.Up:
                    ikParams.LengthZ = -rightStick.Magnitude.Map(0, 10000, 0, _ikLimits.TravelLengthZupperLimit);
                    break;
                case Direction.Down:
                    ikParams.LengthZ = rightStick.Magnitude.Map(0, 10000, 0, _ikLimits.TravelLengthZupperLimit);
                    break;
            }

            switch (leftStick.Direction)
            {
                case Direction.Left:
                    ikParams.BodyRotationZ = -leftStick.Magnitude.Map(0, 10000, 0, 10);
                    break;
                case Direction.UpLeft:
                    ikParams.BodyRotationX = leftStick.Magnitude.Map(0, 10000, 0, 10);
                    ikParams.BodyRotationZ = -leftStick.Magnitude.Map(0, 10000, 0, 10);
                    break;
                case Direction.UpRight:
                    ikParams.BodyRotationX = leftStick.Magnitude.Map(0, 10000, 0, 10);
                    ikParams.BodyRotationZ = leftStick.Magnitude.Map(0, 10000, 0, 10);
                    break;
                case Direction.Right:
                    ikParams.BodyRotationZ = leftStick.Magnitude.Map(0, 10000, 0, 10);
                    break;
                case Direction.Up:
                    ikParams.BodyRotationX = leftStick.Magnitude.Map(0, 10000, 0, 10);
                    break;
                case Direction.Down:
                    ikParams.BodyRotationX = -leftStick.Magnitude.Map(0, 10000, 0, 10);
                    break;
                case Direction.DownLeft:
                    ikParams.BodyRotationZ = -leftStick.Magnitude.Map(0, 10000, 0, 10);
                    ikParams.BodyRotationX = -leftStick.Magnitude.Map(0, 10000, 0, 10);
                    break;
                case Direction.DownRight:
                    ikParams.BodyRotationZ = leftStick.Magnitude.Map(0, 10000, 0, 10);
                    ikParams.BodyRotationX = -leftStick.Magnitude.Map(0, 10000, 0, 10);
                    break;
            }

            ikParams.LengthX = xboxEvent.LeftTrigger.Map(0, 10000, 0, _ikLimits.TravelLengthXlimit);

            ikParams.LengthX += -xboxEvent.RightTrigger.Map(0, 10000, 0, _ikLimits.TravelLengthXlimit);

            return ikParams;
        }

        //private void SetBodyHorizontalOffset(XboxEvent xboxEvent)
        //{
        //    switch (sender.Action)
        //    {
        //        case EventType.Left:
        //            _bodyPosX = sender.Magnitude.Map(0, 10000, 0, 30);
        //            _bodyPosZ = 0;
        //            break;
        //        case EventType.UpLeft:
        //            _bodyPosX = sender.Magnitude.Map(0, 10000, 0, 30);
        //            _bodyPosZ = -sender.Magnitude.Map(0, 10000, 0, 30);
        //            break;
        //        case EventType.UpRight:
        //            _bodyPosX = sender.Magnitude.Map(0, 10000, 0, 30);
        //            _bodyPosZ = sender.Magnitude.Map(0, 10000, 0, 30);
        //            break;
        //        case EventType.Right:
        //            _bodyPosX = -sender.Magnitude.Map(0, 10000, 0, 30);
        //            _bodyPosZ = 0;
        //            break;
        //        case EventType.Up:
        //            _bodyPosX = sender.Magnitude.Map(0, 10000, 0, 30);
        //            _bodyPosZ = 0;
        //            break;
        //        case EventType.Down:
        //            _bodyPosX = -sender.Magnitude.Map(0, 10000, 0, 30);
        //            _bodyPosZ = 0;
        //            break;
        //        case EventType.DownLeft:
        //            _bodyPosZ = -sender.Magnitude.Map(0, 10000, 0, 30);
        //            _bodyPosX = -sender.Magnitude.Map(0, 10000, 0, 30);
        //            break;
        //        case EventType.DownRight:
        //            _bodyPosZ = sender.Magnitude.Map(0, 10000, 0, 30);
        //            _bodyPosX = -sender.Magnitude.Map(0, 10000, 0, 30);
        //            break;
        //    }

        //    _ik.RequestBodyPosition(_bodyRotX, _bodyRotZ, _bodyPosX, _bodyPosZ, _bodyPosY, _bodyRotY);
        //}

    }
}