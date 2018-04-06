using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml.Controls.Primitives;
using Hexapi.Shared.Imu;
using Hexapi.Shared.OpenCv;
using Hexapi.Shared.Utilities;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace Hexapi.Remote.Views{

    public sealed partial class ShellView
    {
        private IDisposable _imageDisposable;

        private IDisposable _cvDisposable;

        internal static Subject<IBuffer> ImageSubject { get; } = new Subject<IBuffer>();

        internal static Subject<List<CircleF>> OpenCvShapeDetectSubject { get; } = new Subject<List<CircleF>>();

        internal static ImuData ImuData { private get; set; } = new ImuData();

        private bool _notComplete;

        internal static int Range { private get; set; }

        private double _canvasCenterWidth;

        private double _canvasCenterHeight;

        private CanvasRenderTarget _canvasRenderTarget;

        private static List<CircleF> _circleF;

        public ShellView()
        {
            InitializeComponent();

            _imageDisposable = ImageSubject
                .Subscribe(async buffer =>
                {
                    if (_notComplete)
                        return;

                    _notComplete = true;

                    using (var stream = buffer.AsStream())
                    using (var imageBuffer = stream.AsRandomAccessStream())
                    {
                        using (var ds = _canvasRenderTarget.CreateDrawingSession())
                        {
                            using (var canvasBitmap = await CanvasBitmap.LoadAsync(ds, imageBuffer))
                            {
                                var rect = new Rect(0, 0, canvasBitmap.SizeInPixels.Width, canvasBitmap.SizeInPixels.Height);

                                ICanvasImage image = new Transform2DEffect
                                {
                                    Source = canvasBitmap,
                                    TransformMatrix = Matrix3x2.CreateRotation((float)(180 * Math.PI / 180)),
                                };

                                var sourceRect = image.GetBounds(ds);
                                ds.DrawImage(image, rect, sourceRect, 1);
                            }

                            if (_circleF != null)
                            {
                                var cl = new List<CircleF>(_circleF);

                                foreach (var c in cl)
                                {
                                    ds.DrawCircle(new Vector2(c.Center.X, c.Center.Y), c.Radius, Colors.GreenYellow);
                                }
                            }
                        }
                    }

                    CanvasControl.Invalidate();

                    _notComplete = false;
                });

            _cvDisposable = OpenCvShapeDetectSubject.Subscribe(circleF => { _circleF = circleF; });
        }

        private void CanvasControl_CreateResources(CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            _canvasRenderTarget = new CanvasRenderTarget(sender, (float)sender.ActualWidth, (float)sender.ActualHeight);

            _canvasCenterWidth = sender.ActualWidth / 2;
            _canvasCenterHeight = sender.ActualHeight / 2;
        }

        private void CanvasControl_OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            args.DrawingSession.DrawImage(_canvasRenderTarget);

            var color = Colors.GreenYellow;

            if (Range <= 12)
                color = Colors.Red;

            //args.DrawingSession.DrawCircle((float)_canvasCenterWidth, (float)_canvasCenterHeight, (float)Range.Map(0, 70, 290, 2), circleColor);

            args.DrawingSession.DrawText($"Range: {Range} in.", new Vector2(14, (float)_canvasCenterHeight - 1), Colors.Black);
            args.DrawingSession.DrawText($"Range: {Range} in.", new Vector2(15, (float)_canvasCenterHeight), color);

            args.DrawingSession.DrawText($"Yaw: {ImuData.Yaw}", new Vector2(14, 499), Colors.Black);
            args.DrawingSession.DrawText($"Yaw: {ImuData.Yaw}", new Vector2(15, 500), Colors.GreenYellow);

            args.DrawingSession.DrawText($"Pitch: {ImuData.Pitch}", new Vector2(14, 524), Colors.Black);
            args.DrawingSession.DrawText($"Pitch: {ImuData.Pitch}", new Vector2(15, 525), Colors.GreenYellow);

            args.DrawingSession.DrawText($"Roll: {ImuData.Roll}", new Vector2(14, 549), Colors.Black);
            args.DrawingSession.DrawText($"Roll: {ImuData.Roll}", new Vector2(15, 550), Colors.GreenYellow);
        }
    }
}
