using System;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using NLog;

namespace Hexapod.Hardware{
    public class UsbCamera
    {
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        private readonly ISubject<string> _imageCaptureSubject = new Subject<string>();

        internal ISubject<string> ImageCaptureSubject { get; private set; }

        internal async Task<bool> StartAsync(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            ImageCaptureSubject = Subject.Synchronize(_imageCaptureSubject);

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            async () =>
            {
                var _mediaCapture = new MediaCapture();

                await _mediaCapture.InitializeAsync();

                var resolutions = _mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview).ToList();
                // set used resolution
                await _mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, resolutions[4]); //4

                CaptureElement _captureElement = new CaptureElement();
                // Set the preview source for the CaptureElement
                _captureElement.Source = _mediaCapture;

                //// Start viewing through the CaptureElement 
                await _mediaCapture.StartPreviewAsync();

                //// Get the property meta data about the video.
                var props = _mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);

                //// Now set the updated meta data into the video preview.
                await _mediaCapture.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, null);

                //LowLagPhotoCapture lowLag = null;

                //if (_mediaCapture == null)
                //{
                //    //_mediaCapture.VideoDeviceController.LowLagPhoto.ThumbnailEnabled = true;
                //    //_mediaCapture.VideoDeviceController.LowLagPhoto.ThumbnailFormat = MediaThumbnailFormat.Bmp;
                //    //_mediaCapture.VideoDeviceController.LowLagPhoto.DesiredThumbnailSize = 800;

                //    //lowLag = await _mediaCapture.PrepareLowLagPhotoCaptureAsync(ImageEncodingProperties.CreateJpeg());
                //}

                while (!cancellationToken.IsCancellationRequested)
                {
                    //var photo = await lowLag.CaptureAsync();

                    // Get photo as a BitmapImage
                    //BitmapImage bitmap = new BitmapImage();
                    //await bitmap.SetSourceAsync(photo.Frame);

                    // Get thumbnail as a BitmapImage
                    //var bitmapThumbnail = new BitmapImage();
                    //await bitmapThumbnail.SetSourceAsync(photo.Thumbnail);


                    //using (var ms = new MemoryStream())
                    //{
                    //    WriteableBitmap wb = new WriteableBitmap((int)photo.Thumbnail.Width, (int)photo.Thumbnail.Height );
                    //    await wb.SetSourceAsync(photo.Thumbnail);

                    //    using (var s1 = wb.PixelBuffer.AsStream())
                    //    {
                    //        s1.CopyTo(ms);

                    //        var s = Convert.ToBase64String(ms.ToArray());

                    //        _imageCaptureSubject.OnNext(s);
                    //    }

                    //}

                    //using (var bitmapBgra8 = SoftwareBitmap.Convert(photo.Thumbnail.SoftwareBitmap, BitmapPixelFormat.Bgra8))
                    //{
                    //    var wb = new WriteableBitmap(bitmap.PixelWidth, bitmap.PixelHeight);
                    //    bitmapBgra8.CopyToBuffer(wb.PixelBuffer);

                    //    using (var stream = new  InMemoryRandomAccessStream())
                    //    {
                    //        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
                    //        encoder.SetSoftwareBitmap(bitmapBgra8);
                    //        await encoder.FlushAsync();

                    //        var reader = new DataReader(stream.GetInputStreamAt(0));
                    //        var bytes = new byte[stream.Size];
                    //        await reader.LoadAsync((uint)stream.Size);
                    //        reader.ReadBytes(bytes);
                    //        var s = Convert.ToBase64String(bytes);

                    //        _imageCaptureSubject.OnNext(s);
                    //    }
                    //}

                    using (var stream = new InMemoryRandomAccessStream())
                    {
                        try
                        {
                            await _mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);
                        }
                        catch (Exception e)
                        {
                            await Task.Delay(1000);
                            _logger.Log(LogLevel.Warn, e.Message);
                            continue;
                        }

                        using (var stream2 = await ReencodeAndSavePhotoAsync1(stream, PhotoOrientation.Rotate180))
                        {
                            try
                            {
                                var s = string.Empty;

                                using (var reader = new DataReader(stream2.GetInputStreamAt(0)))
                                {
                                    var bytes = new byte[stream2.Size];
                                    await reader.LoadAsync((uint)stream2.Size);
                                    reader.ReadBytes(bytes);


                                    s = Convert.ToBase64String(bytes);

                                    ImageCaptureSubject.OnNext(s);
                                }
                            }
                            catch (Exception e)
                            {
                                _logger.Log(LogLevel.Warn, $"ImageCapture => {e.Message}");
                            }
                        }
                    }
                }

                tcs.SetResult(true);
            });

            return await tcs.Task;
        }

        private static async Task<IRandomAccessStream> ReencodeAndSavePhotoAsync1(IRandomAccessStream stream, PhotoOrientation photoOrientation)
        {
            IRandomAccessStream randomAccessStream;

            using (var inputStream = stream)
            {
                var decoder = await BitmapDecoder.CreateAsync(inputStream);

                using (var outputStream = new InMemoryRandomAccessStream())
                {
                    var pixelData = await decoder.GetPixelDataAsync(
                                                 BitmapPixelFormat.Rgba8,
                                                 BitmapAlphaMode.Straight,
                                                 new BitmapTransform { ScaledHeight = decoder.PixelHeight / 3, ScaledWidth = decoder.PixelWidth / 3 },
                                                 ExifOrientationMode.RespectExifOrientation,
                                                 ColorManagementMode.DoNotColorManage);

                    var encoder = await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);

                    encoder.SetPixelData(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Premultiplied, decoder.PixelWidth / 3, decoder.PixelHeight / 3, 96, 96, pixelData.DetachPixelData());

                    var properties = new BitmapPropertySet
                    {
                        { "System.Photo.Orientation", new BitmapTypedValue(photoOrientation, PropertyType.UInt16) },
                    };

                    await encoder.BitmapProperties.SetPropertiesAsync(properties);
                    await encoder.FlushAsync();

                    randomAccessStream = outputStream.CloneStream();
                }
            }

            return randomAccessStream;
        }

    }
}