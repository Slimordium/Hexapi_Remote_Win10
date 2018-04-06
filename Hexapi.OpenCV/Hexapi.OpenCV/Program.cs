using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Cuda;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Newtonsoft.Json;
using RxMqtt.Client;

namespace Hexapi.OpenCV
{
    class Program
    {
        private static MqttClient _mqttClient;
        private static long _safeWait;
        private static IDisposable _disposable;

        static async Task Main(string[] args)
        {
            Console.WriteLine($"CUDA Available: {CudaInvoke.HasCuda}");

            _mqttClient = new MqttClient("OpenCv", "127.0.0.1", 1883);

            var status = await _mqttClient.InitializeAsync();

            Console.WriteLine($"MQTT Connection => {status}");

            //_disposable = _mqttClient.GetPublishByteObservable("hex-eye")
            //    .Sample(TimeSpan.FromMilliseconds(500))
            //    //.SubscribeOn(NewThreadScheduler.Default)
            //    .Subscribe(async buffer => { await DetectCircles(buffer); });

            _disposable = _mqttClient
                .GetPublishByteObservable("hex-eye")
                //.SubscribeOn(NewThreadScheduler.Default)
                //.SubscribeOn(Scheduler.Default)
                .Subscribe(async b =>
                {
                    if (Interlocked.Exchange(ref _safeWait, 1) == 1) //skip this image if not finished processing previous image
                        return;

                    await CudaDetector(b);
                });

            Console.ReadLine();

            _disposable?.Dispose();
        }

        private static async Task CudaDetector(byte[] imageBuffer)
        {
            using (var memoryStream = new MemoryStream(imageBuffer))
            using (var bmp = new Bitmap(memoryStream))
            {
                using (var img = new Image<Bgr, byte>(bmp))
                {
                    //Convert the image to grayscale and filter out the noise
                    var uimage = new Mat();
                    CvInvoke.CvtColor(img, uimage, ColorConversion.Bgr2Gray);

                    //use image pyr to remove noise
                    var pyrDown = new Mat();
                    CvInvoke.PyrDown(uimage, pyrDown);
                    CvInvoke.PyrUp(pyrDown, uimage);

                    var gpuImage = new GpuMat();
                    gpuImage.Upload(uimage);

                    await DetectCircles(gpuImage);

                    //await DetectLines(gpuImage);
                }
            }

            Interlocked.Exchange(ref _safeWait, 0);
        }

        private static async Task DetectCircles(GpuMat gpuImage)
        {
            using (var detector = new CudaHoughCirclesDetector(1, 10, 150, 60, 2, 140, 8)) //1, 20, 127, 60, 5, 400, 10
            {
                try
                {
                    IOutputArray gpuResult = new GpuMat();

                    detector.Detect(gpuImage, gpuResult);

                    var gpuMat = (GpuMat)gpuResult;

                    var mat = new Mat();

                    gpuMat.Download(mat);

                    var circleBuffer = mat.GetData();

                    if (circleBuffer == null)
                    {
                        return;
                    }

                    var circles = new List<CircleF>();

                    for (var i = 0; i < circleBuffer.Length - 12;)
                    {
                        var x = BitConverter.ToSingle(circleBuffer, i);

                        i += 4;

                        var y = BitConverter.ToSingle(circleBuffer, i);

                        i += 4;

                        var r = BitConverter.ToSingle(circleBuffer, i);

                        i += 4;

                        Console.WriteLine($"X:{x} Y:{y} R:{r}");

                        var circle = new CircleF(new PointF(Map(x, 0, 800, 800), Map(y)), r);

                        circles.Add(circle);
                    }

                    await _mqttClient.PublishAsync(JsonConvert.SerializeObject(circles), "opencv-circle");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        private static async Task DetectLines(GpuMat gpuImage)
        {
            using (var detector = new CudaHoughLinesDetector(15, (float)(Math.PI / 45.0), 100, false, 2))
            {
                try
                {
                    IOutputArray gpuResult = new GpuMat();

                    detector.Detect(gpuImage, gpuResult);

                    var gpuMat = (GpuMat)gpuResult;

                    var mat = new Mat();

                    gpuMat.Download(mat);

                    var lineSegmentBuffer = mat.GetData();

                    if (lineSegmentBuffer == null)
                    {
                        return;
                    }

                    var lineSegments = new List<LineSegment2D>();

                    for (var i = 0; i < lineSegmentBuffer.Length - 16;)
                    {
                        var x1 = BitConverter.ToInt16(lineSegmentBuffer, i);

                        i += 2;

                        var y1 = BitConverter.ToInt16(lineSegmentBuffer, i);

                        i += 2;


                        var x2 = BitConverter.ToInt16(lineSegmentBuffer, i);

                        i += 2;

                        var y2 = BitConverter.ToInt16(lineSegmentBuffer, i);

                        i += 2;

                        Console.WriteLine($"X1:{x1} Y1:{y1} - X2:{x2} Y2:{y2}");

                        var lineSegment = new LineSegment2D(new Point((int)x1, (int)y1), new Point((int)x2, (int)y2));

                        lineSegments.Add(lineSegment);
                    }

                    await _mqttClient.PublishAsync(JsonConvert.SerializeObject(lineSegments), "opencv-line");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        public static float Map(float valueToMap, double valueToMapMin = 0, double valueToMapMax = 600, double outMin = 600, double outMax = 0)
        {
            return (float)((valueToMap - valueToMapMin) * (outMax - outMin) / (valueToMapMax - valueToMapMin) + outMin);
        }
    }
}
