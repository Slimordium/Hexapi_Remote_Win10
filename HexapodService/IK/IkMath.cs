using System;
using System.Text;
using Hexapod.Enums;

namespace Hexapod.IK
{
    /// <summary>
    ///     This is a port of the "Phoenix" 3DOF Hexapod code in C#. Uses CH3-R body from Lynxmotion/robotshop.com
    ///     https://github.com/KurtE/Arduino_Phoenix_Parts/tree/master/Phoenix
    ///     http://www.robotshop.com/en/lynxmotion-aexapod-ch3-r-combo-kit-body-frame.html
    /// </summary>
    internal class IkMath
    {
        private const double TenThousand = 10000;
        private const double OneMillion = 1000000;
        private const double CoxaLengthInMm = 33; //mm
        private const double FemurLengthInMm = 70; //mm
        private const double TibiaLengthInMm = 130; //mm

        private const double HexInitXz = CoxaLengthInMm + FemurLengthInMm;
        private const double HexInitXzCos45 = HexInitXz * .7071; //http://www.math.com/tables/trig/tables.htm
        private const double HexInitXzSin45 = HexInitXz * .7071;
        private const double HexInitY = 25;

        private static readonly double[] _coxaAngle = new double[6];
        private static readonly double[] _femurAngle = new double[6]; //Actual Angle of the vertical hip, decimals = 1
        private static readonly double[] _tibiaAngle = new double[6]; //Actual Angle of the knee, decimals = 1

        private const double RfInitPosX = HexInitXzCos45;
        private const double RfInitPosY = HexInitY;
        private const double RfInitPosZ = -HexInitXzSin45;

        private const double LrInitPosX = HexInitXzCos45;
        private const double LrInitPosY = HexInitY;
        private const double LrInitPosZ = HexInitXzCos45;

        private const double LmInitPosX = HexInitXz;
        private const double LmInitPosY = HexInitY;
        private const double LmInitPosZ = 0;

        private const double LfInitPosX = HexInitXzCos45;
        private const double LfInitPosY = HexInitY;
        private const double LfInitPosZ = -HexInitXzSin45;

        private const double RmInitPosX = HexInitXz;
        private const double RmInitPosY = HexInitY;
        private const double RmInitPosZ = 0;

        private const double RrInitPosX = HexInitXzCos45;
        private const double RrInitPosY = HexInitY;
        private const double RrInitPosZ = HexInitXzSin45;

        private static readonly double[] _initPosX = { RrInitPosX, RmInitPosX, RfInitPosX, LrInitPosX, LmInitPosX, LfInitPosX };
        private static readonly double[] _initPosY = { RrInitPosY, RmInitPosY, RfInitPosY, LrInitPosY, LmInitPosY, LfInitPosY };
        private static readonly double[] _initPosZ = { RrInitPosZ, RmInitPosZ, RfInitPosZ, LrInitPosZ, LmInitPosZ, LfInitPosZ };

        private static readonly double[] _legPosX = new double[6]; //Actual X Position of the Leg 
        private static readonly double[] _legPosY = new double[6]; //Actual Y Position of the Leg
        private static readonly double[] _legPosZ = new double[6]; //Actual Z Position of the Leg

        private static readonly double _pi1K;

        private const double Pi = 3.1415926535897932384626433832795028841971693993751058209749445923078164; //This seemed to help 

        private const double _coxaMinimumPwm = -620;
        private const double _coxaMaximumPwm = 620;
        private const double _femurMinimumPwm = -620;
        private const double _femurMaximumPwm = 620;
        private const double _tibiaMinimumPwm = -620;
        private const double _tibiaMaximumPwm = 620; //I think this is the "down" angle limit, meaning how far in relation to the femur can it point towards the center of the bot

        //For the Solar 772 or 771, PwmDiv = 1500 and PfConst = 900 works well. Not sure what this should be on any other servo
        private const double PfConst = 900;
        private const double PwmDiv = 1500;

        private static readonly double[] CoxaServoAngles = new double[6];
        private static readonly double[] FemurServoAngles = new double[6];
        private static readonly double[] TibiaServoAngles = new double[6];
        private static readonly int[][] LegServos = new int[6][]; //Leg index,

        private static readonly double[] _gaitPosX = new double[6]; //Array containing Relative X position corresponding to the SetIkValuesByGait
        private static readonly double[] _gaitPosY = new double[6]; //Array containing Relative Y position corresponding to the SetIkValuesByGait
        private static readonly double[] _gaitPosZ = new double[6]; //Array containing Relative Z position corresponding to the SetIkValuesByGait
        private static readonly double[] _gaitRotY = new double[6]; //Array containing Relative Y rotation corresponding to the SetIkValuesByGait

        private const double RrCoxaAngle = -450; //450 = 45 degrees off center
        private const double RmCoxaAngle = 0;
        private const double RfCoxaAngle = 450;
        private const double LrCoxaAngle = -450;
        private const double LmCoxaAngle = 0;
        private const double LfCoxaAngle = 450;

        private const double RfOffsetZ = -126; //Distance Z from center line that crosses from front/back of the body to the coxa (Z front/back)
        private const double RfOffsetX = -70; //Distance X from center line that crosses left/right of the body to the coxa (X side to side)
        private const double LfOffsetZ = -126;
        private const double LfOffsetX = 70;
        private const double RrOffsetZ = 126;
        private const double RrOffsetX = -70;
        private const double LrOffsetZ = 126;
        private const double LrOffsetX = 70;
        private const double RmOffsetZ = 0;
        private const double RmOffsetX = -135;
        private const double LmOffsetZ = 0;
        private const double LmOffsetX = 135;

        private static readonly double[] _offsetX = { RrOffsetX, RmOffsetX, RfOffsetX, LrOffsetX, LmOffsetX, LfOffsetX };
        private static readonly double[] _offsetZ = { RrOffsetZ, RmOffsetZ, RfOffsetZ, LrOffsetZ, LmOffsetZ, LfOffsetZ };

        private static readonly double[] _calculatedCoxaAngle = { RrCoxaAngle, RmCoxaAngle, RfCoxaAngle, LrCoxaAngle, LmCoxaAngle, LfCoxaAngle };

        private const int _travelDeadZone = 2; //Ignore move requst less than this value

        private const int Lf = 5;
        private const int Lm = 4;
        private const int Lr = 3;
        private const int Rf = 2;
        private const int Rm = 1;
        private const int Rr = 0;

        private static readonly int[] _gaitLegNumber = new int[6]; //Initial position of the leg

        private static int _liftDivisionFactor; //Normaly: 2, when NrLiftedPos=5: 4
        private static int _numberOfLiftedPositions; //Number of positions that a single leg is lifted [1-3]
        private static int _gaitSequenceCount; //Number of steps in gait
        private static int _tlDivisionFactor; //Number of steps that a leg is on the floor while walking
        private static int _halfLiftHeight; //If true the outer positions of the lifted legs will be half height    
        private static int _gaitSequence = 1;

        internal static string AllServosOff { get; }

        static IkMath()
        {
            _pi1K = Pi * 1000D;


            for (var i = 0; i < 6; i++)
                LegServos[i] = new int[3];

            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                _legPosX[legIndex] = _initPosX[legIndex]; //Set start positions for each leg
                _legPosY[legIndex] = _initPosY[legIndex];
                _legPosZ[legIndex] = _initPosZ[legIndex];

                //if (legIndex == 2)
                //    LegYHeightCorrector[legIndex] = 9;
                //else if (legIndex == 1)
                //    LegYHeightCorrector[legIndex] = 5;
                //else if (legIndex == 4)
                //    LegYHeightCorrector[legIndex] = 8;
                //else if (legIndex == 5)
                //   LegYHeightCorrector[legIndex] = 10;
                //else
                //    LegYHeightCorrector[legIndex] = 0;
            }

            LegServos[0][0] = 20;
            LegServos[0][1] = 19;
            LegServos[0][2] = 18;

            LegServos[1][0] = 24;
            LegServos[1][1] = 23;
            LegServos[1][2] = 22;

            LegServos[2][0] = 28;
            LegServos[2][1] = 27;
            LegServos[2][2] = 26;

            LegServos[3][0] = 4;
            LegServos[3][1] = 3;
            LegServos[3][2] = 2;

            LegServos[4][0] = 8;
            LegServos[4][1] = 7;
            LegServos[4][2] = 6;

            LegServos[5][0] = 12;
            LegServos[5][1] = 11;
            LegServos[5][2] = 10;

            var sb = new StringBuilder();

            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                sb.Append($"#{LegServos[legIndex][0]}P0");
                sb.Append($"#{LegServos[legIndex][1]}P0");
                sb.Append($"#{LegServos[legIndex][2]}P0");
            }

            sb.Append("T0\rQ\r");

            AllServosOff = sb.ToString();
        }

        internal IkMath()
        {
            GaitSelect(GaitType.TripleTripod16);
        }

        private static void BodyLegIk(int legIndex,
            double bodyPosX, double bodyPosY, double bodyPosZ,
            double bodyRotX, double bodyRotZ, double bodyRotY)
        {
           
            double posX;
            if (legIndex <= 2)
                posX = -_legPosX[legIndex] + bodyPosX + _gaitPosX[legIndex];
            else
                posX = _legPosX[legIndex] - bodyPosX + _gaitPosX[legIndex];

            var posY = (_legPosY[legIndex] + bodyPosY + _gaitPosY[legIndex]) * 100d;
            var posZ = _legPosZ[legIndex] + bodyPosZ + _gaitPosZ[legIndex];

            var centerOfBodyToFeetX = (_offsetX[legIndex] + posX) * 100d;
            var centerOfBodyToFeetZ = (_offsetZ[legIndex] + posZ) * 100d;

            double bodyRotYSin, bodyRotYCos, bodyRotZSin, bodyRotZCos, bodyRotXSin, bodyRotXCos;

            GetSinCos(bodyRotY + _gaitRotY[legIndex], out bodyRotYSin, out bodyRotYCos);
            GetSinCos(bodyRotZ, out bodyRotZSin, out bodyRotZCos);
            GetSinCos(bodyRotX, out bodyRotXSin, out bodyRotXCos);

            //Calculation of rotation matrix: 
            var bodyFkPosX = (centerOfBodyToFeetX -
                                (centerOfBodyToFeetX * bodyRotYCos * bodyRotZCos -
                                centerOfBodyToFeetZ * bodyRotZCos * bodyRotYSin +
                                posY * bodyRotZSin)) / 100d;

            var bodyFkPosZ = (centerOfBodyToFeetZ -
                                (centerOfBodyToFeetX * bodyRotXCos * bodyRotYSin +
                                centerOfBodyToFeetX * bodyRotYCos * bodyRotZSin * bodyRotXSin +
                                centerOfBodyToFeetZ * bodyRotYCos * bodyRotXCos -
                                centerOfBodyToFeetZ * bodyRotYSin * bodyRotZSin * bodyRotXSin -
                                posY * bodyRotZCos * bodyRotXSin)) / 100d;

            var bodyFkPosY = (posY -
                                (centerOfBodyToFeetX * bodyRotYSin * bodyRotXSin -
                                centerOfBodyToFeetX * bodyRotYCos * bodyRotXCos * bodyRotZSin +
                                centerOfBodyToFeetZ * bodyRotYCos * bodyRotXSin +
                                centerOfBodyToFeetZ * bodyRotXCos * bodyRotYSin * bodyRotZSin +
                                posY * bodyRotZCos * bodyRotXCos)) / 100d;

            double feetPosX;
            if (legIndex <= 2)
                feetPosX = _legPosX[legIndex] - bodyPosX + bodyFkPosX - _gaitPosX[legIndex];
            else
                feetPosX = _legPosX[legIndex] + bodyPosX - bodyFkPosX + _gaitPosX[legIndex];

            var feetPosY = _legPosY[legIndex] + bodyPosY - bodyFkPosY + _gaitPosY[legIndex];
            var feetPosZ = _legPosZ[legIndex] + bodyPosZ - bodyFkPosZ + _gaitPosZ[legIndex];

            double xyhyp;
            var atan2 = GetATan2(feetPosX, feetPosZ, out xyhyp);

            var coxaServoAngle = (atan2 * 180d) / _pi1K + _calculatedCoxaAngle[legIndex];

            var ikFeetPosXz = xyhyp / 100d;
            var ika14 = GetATan2(feetPosY, ikFeetPosXz - CoxaLengthInMm, out xyhyp);
            var ika24 = GetArcCos(((FemurLengthInMm * FemurLengthInMm - TibiaLengthInMm * TibiaLengthInMm) * TenThousand + Math.Pow(xyhyp, 2d)) / (2 * FemurLengthInMm * 100d * xyhyp / TenThousand));

            var femurServoAngle = -(ika14 + ika24) * 180d / _pi1K + 900d;

            var tibiaServoAngle = -(900d - GetArcCos(((FemurLengthInMm * FemurLengthInMm + TibiaLengthInMm * TibiaLengthInMm) * TenThousand - Math.Pow(xyhyp, 2d)) / (2d * FemurLengthInMm * TibiaLengthInMm)) * 180d / _pi1K);

            _coxaAngle[legIndex] = coxaServoAngle;
            _femurAngle[legIndex] = femurServoAngle;
            _tibiaAngle[legIndex] = tibiaServoAngle;
        }

        /// <summary>
        /// Returns positions for a sequnce in gait for all legs
        /// </summary>
        /// <param name="gaitSpeed"></param>
        /// <param name="travelLengthX"></param>
        /// <param name="travelLengthZ"></param>
        /// <param name="travelRotationY"></param>
        /// <param name="legLiftHeight"></param>
        /// <param name="bodyPosX"></param>
        /// <param name="bodyPosY"></param>
        /// <param name="bodyPosZ"></param>
        /// <param name="bodyRotX"></param>
        /// <param name="bodyRotZ"></param>
        /// <param name="bodyRotY"></param>
        /// <returns></returns>
        internal string CalculateServoPositions(double gaitSpeed,
            double travelLengthX, double travelLengthZ, double travelRotationY, double legLiftHeight,
            double bodyPosX, double bodyPosY, double bodyPosZ,
            double bodyRotX, double bodyRotZ, double bodyRotY)
        {
            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                SetIkValuesByGait(legIndex, travelLengthX, travelLengthZ, travelRotationY, legLiftHeight);

                BodyLegIk(legIndex, bodyPosX, bodyPosY, bodyPosZ, bodyRotX, bodyRotZ, bodyRotY);
            }

            return GetAllServoPositions(gaitSpeed);
        }

        private static void SetIkValuesByGait(int legIndex, double travelLengthX, double travelLengthZ, double travelRotationY, double legLiftHeight)
        {
            var travelRequest = Math.Abs(travelLengthX) > _travelDeadZone || Math.Abs(travelLengthZ) > _travelDeadZone || Math.Abs(travelRotationY) > _travelDeadZone;

           
            if ((travelRequest &&
                        (_numberOfLiftedPositions == 1 || _numberOfLiftedPositions == 3 || _numberOfLiftedPositions == 5) && _gaitSequence == _gaitLegNumber[legIndex]) ||
                        !travelRequest &&
                        _gaitSequence == _gaitLegNumber[legIndex] && (Math.Abs(_gaitPosX[legIndex]) > 2 || Math.Abs(_gaitPosZ[legIndex]) > 2 || Math.Abs(_gaitRotY[legIndex]) > 2))
            {
                _gaitPosX[legIndex] = 0;
                _gaitPosY[legIndex] = -legLiftHeight;
                _gaitPosZ[legIndex] = 0;
                _gaitRotY[legIndex] = 0;
            }
            //Optional Half height Rear (2, 3, 5 lifted positions)
            else if (travelRequest &&
                        (_numberOfLiftedPositions == 2 && _gaitSequence == _gaitLegNumber[legIndex] || _numberOfLiftedPositions >= 3 && (_gaitSequence == _gaitLegNumber[legIndex] - 1 || _gaitSequence == _gaitLegNumber[legIndex] + (_gaitSequenceCount - 1))))
            {
                _gaitPosX[legIndex] = -travelLengthX / _liftDivisionFactor;
                _gaitPosY[legIndex] = -3 * legLiftHeight / (3 + _halfLiftHeight);
                _gaitPosZ[legIndex] = -travelLengthZ / _liftDivisionFactor;
                _gaitRotY[legIndex] = -travelRotationY / _liftDivisionFactor;
            }
            // Optional Half height front (2, 3, 5 lifted positions)
            else if (travelRequest &&
                        _numberOfLiftedPositions >= 2 &&
                        (_gaitSequence == _gaitLegNumber[legIndex] + 1 || _gaitSequence == _gaitLegNumber[legIndex] - (_gaitSequenceCount - 1)))
            {
                _gaitPosX[legIndex] = travelLengthX / _liftDivisionFactor;
                _gaitPosY[legIndex] = -3 * legLiftHeight / (3 + _halfLiftHeight);
                _gaitPosZ[legIndex] = travelLengthZ / _liftDivisionFactor;
                _gaitRotY[legIndex] = travelRotationY / _liftDivisionFactor;
            }
            //Optional Half heigth Rear 5 LiftedPos (5 lifted positions)
            else if (travelRequest && _numberOfLiftedPositions == 5 && (_gaitSequence == _gaitLegNumber[legIndex] - 2))
            {
                _gaitPosX[legIndex] = -travelLengthX / 2;
                _gaitPosY[legIndex] = -legLiftHeight / 2;
                _gaitPosZ[legIndex] = -travelLengthZ / 2;
                _gaitRotY[legIndex] = -travelRotationY / 2;
            }
            //Optional Half heigth Front 5 LiftedPos (5 lifted positions)
            else if (travelRequest &&
                        _numberOfLiftedPositions == 5 &&
                        (_gaitSequence == _gaitLegNumber[legIndex] + 2 || _gaitSequence == _gaitLegNumber[legIndex] - (_gaitSequenceCount - 2)))
            {
                _gaitPosX[legIndex] = travelLengthX / 2;
                _gaitPosY[legIndex] = -legLiftHeight / 2;
                _gaitPosZ[legIndex] = travelLengthZ / 2;
                _gaitRotY[legIndex] = travelRotationY / 2;
            }
            //Leg front down position
            else if ((_gaitSequence == _gaitLegNumber[legIndex] + _numberOfLiftedPositions || _gaitSequence == _gaitLegNumber[legIndex] - (_gaitSequenceCount - _numberOfLiftedPositions)) &&
                        _gaitPosY[legIndex] < 0)
            {
                _gaitPosX[legIndex] = travelLengthX / 2;
                _gaitPosZ[legIndex] = travelLengthZ / 2;
                _gaitRotY[legIndex] = travelRotationY / 2;
                _gaitPosY[legIndex] = 0;
            }
            //Move body forward      
            else
            {
                _gaitPosX[legIndex] = _gaitPosX[legIndex] - travelLengthX / _tlDivisionFactor;
                _gaitPosY[legIndex] = 0;
                _gaitPosZ[legIndex] = _gaitPosZ[legIndex] - travelLengthZ / _tlDivisionFactor;
                _gaitRotY[legIndex] = _gaitRotY[legIndex] - travelRotationY / _tlDivisionFactor;
            }

            if (legIndex != 5)
            {
                return;
            }

            _gaitSequence++; //After calculating positions of all joints, move to the next step in the sequence of moves in this gait

            if (_gaitSequence > _gaitSequenceCount) //The last leg in this step
                _gaitSequence = 1;
        }

        /// <summary>
        /// Sets parameters for the selected gait
        /// </summary>
        /// <param name="gaitType"></param>
        internal void GaitSelect(GaitType gaitType)
        {
            switch (gaitType)
            {
                case GaitType.Ripple12:
                    _gaitLegNumber[Lf] = 9;
                    _gaitLegNumber[Lm] = 5;
                    _gaitLegNumber[Lr] = 1;

                    _gaitLegNumber[Rf] = 3;
                    _gaitLegNumber[Rm] = 11;
                    _gaitLegNumber[Rr] = 7;

                    _numberOfLiftedPositions = 3;
                    _halfLiftHeight = 3;
                    _tlDivisionFactor = 8;
                    _gaitSequenceCount = 12;
                    break;
                case GaitType.Tripod8:
                    _gaitLegNumber[Lf] = 5;
                    _gaitLegNumber[Lm] = 1;
                    _gaitLegNumber[Lr] = 5;

                    _gaitLegNumber[Rf] = 1;
                    _gaitLegNumber[Rm] = 5;
                    _gaitLegNumber[Rr] = 1;

                    _numberOfLiftedPositions = 3;
                    _halfLiftHeight = 3;
                    _tlDivisionFactor = 4;
                    _gaitSequenceCount = 8;
                    break;
                case GaitType.TripleTripod12:
                    _gaitLegNumber[Lf] = 9;
                    _gaitLegNumber[Lm] = 4;
                    _gaitLegNumber[Lr] = 11;

                    _gaitLegNumber[Rf] = 3;
                    _gaitLegNumber[Rm] = 10;
                    _gaitLegNumber[Rr] = 5;

                    _numberOfLiftedPositions = 3;
                    _halfLiftHeight = 3;
                    _tlDivisionFactor = 8;
                    _gaitSequenceCount = 12;
                    break;
                case GaitType.TripleTripod16:
                    _gaitLegNumber[Lf] = 12;
                    _gaitLegNumber[Lm] = 5;
                    _gaitLegNumber[Lr] = 14;

                    _gaitLegNumber[Rf] = 4;
                    _gaitLegNumber[Rm] = 13;
                    _gaitLegNumber[Rr] = 6;

                    _numberOfLiftedPositions = 5;
                    _halfLiftHeight = 1;
                    _tlDivisionFactor = 10;
                    _gaitSequenceCount = 16;
                    break;
                case GaitType.Wave24:
                    _gaitLegNumber[Lf] = 9;
                    _gaitLegNumber[Lm] = 5;
                    _gaitLegNumber[Lr] = 1;

                    _gaitLegNumber[Rf] = 21;
                    _gaitLegNumber[Rm] = 17;
                    _gaitLegNumber[Rr] = 13;

                    _numberOfLiftedPositions = 3;
                    _halfLiftHeight = 3;
                    _tlDivisionFactor = 20;
                    _gaitSequenceCount = 24;
                    break;
            }

            _liftDivisionFactor = _numberOfLiftedPositions == 5 ? 4 : 2;
        }

        /// <summary>
        /// Gets the PWM values for all servos
        /// </summary>
        /// <param name="gaitSpeed">This move needs to complete in this many milliseconds</param>
        /// <returns></returns>
        private static string GetAllServoPositions(double gaitSpeed)
        {
            var sb = new StringBuilder();

            for (var legIndex = 0; legIndex <= 5; legIndex++)
            {
                _coxaAngle[legIndex] = Math.Min(Math.Max(_coxaAngle[legIndex], _coxaMinimumPwm), _coxaMaximumPwm);
                _femurAngle[legIndex] = Math.Min(Math.Max(_femurAngle[legIndex], _femurMinimumPwm), _femurMaximumPwm);
                _tibiaAngle[legIndex] = Math.Min(Math.Max(_tibiaAngle[legIndex], _tibiaMinimumPwm), _tibiaMaximumPwm);

                double coxaPosition;
                double femurPosition;
                double tibiaPosition;

                //Just dump everything after the decimal
                if (legIndex < 3)
                {
                    coxaPosition = (int)((-_coxaAngle[legIndex] + 900) * 1000 / PwmDiv + PfConst);
                    femurPosition = (int)((-_femurAngle[legIndex] + 900) * 1000 / PwmDiv + PfConst);
                    tibiaPosition = (int)((-_tibiaAngle[legIndex] + 900) * 1000 / PwmDiv + PfConst);
                }
                else
                {
                    coxaPosition = (int)((_coxaAngle[legIndex] + 900) * 1000 / PwmDiv + PfConst);
                    femurPosition = (int)((_femurAngle[legIndex] + 900) * 1000 / PwmDiv + PfConst);
                    tibiaPosition = (int)((_tibiaAngle[legIndex] + 900) * 1000 / PwmDiv + PfConst);
                }

                CoxaServoAngles[legIndex] = coxaPosition;
                FemurServoAngles[legIndex] = femurPosition;
                TibiaServoAngles[legIndex] = tibiaPosition;

                sb.Append($"#{LegServos[legIndex][0]}P{coxaPosition}");
                sb.Append($"#{LegServos[legIndex][1]}P{femurPosition}");
                sb.Append($"#{LegServos[legIndex][2]}P{tibiaPosition}");
            }

            sb.Append($"T{gaitSpeed}\rQ\r");

            return sb.ToString();
        }

        private static void GetSinCos(double angleDeg, out double sin, out double cos)
        {
            var angle = Pi * angleDeg / 180.0;

            sin = Math.Sin(angle);
            cos = Math.Cos(angle);
        }

        private static double GetArcCos(double cos)
        {
            var c = cos / TenThousand;

            if ((Math.Abs(Math.Abs(c) - 1.0) < 1e-60)) //Why does this make a difference if there is only 15/16 decimal places in regards to precision....?
            {
                return (1 - c) * Pi / 2.0;
            }

            return (Math.Atan(-c / Math.Sqrt(1 - c * c)) + 2 * Math.Atan(1)) * TenThousand;

            //return (Math.Abs(Math.Abs(c) - 1.0) < 1e-60
            //    ? (1 - c) * Pi / 2.0
            //    : Math.Atan(-c / Math.Sqrt(1 - c * c)) + 2 * Math.Atan(1)) * TenThousand;
        }

        private static double GetATan2(double atanX, double atanY, out double xyhyp2)
        {
            double atan4;

            xyhyp2 = Math.Sqrt((atanX * atanX * TenThousand) + (atanY * atanY * TenThousand));

            var angleRad4 = GetArcCos((atanX * OneMillion) / xyhyp2);

            if (atanY < 0)
                atan4 = -angleRad4;
            else
                atan4 = angleRad4;

            return atan4;
        }
    }
}
