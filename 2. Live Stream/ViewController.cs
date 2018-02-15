using System;
using UIKit;
using Foundation;
using AVFoundation;
using System.Threading.Tasks;
using Tesseract;
using Tesseract.iOS;
using CoreGraphics;
using System.Drawing;

namespace Camera
{
    public partial class ViewController : UIViewController
    {
        AVCaptureSession captureSession;
        AVCaptureDeviceInput captureDeviceInput;
        AVCaptureStillImageOutput stillImageOutput;
        AVCaptureVideoPreviewLayer videoPreviewLayer;
        ITesseractApi tesseract;
        UIImageView targetOverlayView;
        bool _tesseractInitialised = false;
        bool _keepPolling = true;
        UILabel textOutputLabel;


        private const int TargetOverlayWidth = 100;
        private const int TargetOverlayHeight = 50;

        /// <summary>
        /// The frequency which a snapshot from the camera is taken and run through Tesseract OCR
        /// </summary>
        private const int SnapshotMilliseconds = 200;


        public ViewController(IntPtr handle) : base(handle)
        {
            tesseract = new TesseractApi();
        }

        public override async void ViewDidLoad()
        {
            base.ViewDidLoad();

            await AuthorizeCameraUse();
            await SetupLiveCameraStream();
        }

        public override void DidReceiveMemoryWarning()
        {
            base.DidReceiveMemoryWarning();
        }

        /// <summary>
        /// Adds the target frame sized snapshot to the view
        /// </summary>
        /// <param name="image">Image.</param>
        private void AddImageToScreenHelper(UIImage image)
        {
            var imageView = new UIImageView(image)
            {
                Frame = new CGRect(0, 0, targetOverlayView.Frame.Width, targetOverlayView.Frame.Height)
            };

            View.AddSubview(imageView);
        }

        /// <summary>
        /// Creates a camera stream and adds it to the view
        /// </summary>
        public async Task SetupLiveCameraStream()
        {
            captureSession = new AVCaptureSession();

            var viewLayer = liveCameraStream.Layer;
            videoPreviewLayer = new AVCaptureVideoPreviewLayer(captureSession)
            {
                Frame = this.View.Frame
            };

            liveCameraStream.Layer.AddSublayer(videoPreviewLayer);

            var captureDevice = AVCaptureDevice.DefaultDeviceWithMediaType(AVMediaType.Video);
            ConfigureCameraForDevice(captureDevice);
            captureDeviceInput = AVCaptureDeviceInput.FromDevice(captureDevice);
            captureSession.AddInput(captureDeviceInput);

            var dictionary = new NSMutableDictionary();
            dictionary[AVVideo.CodecKey] = new NSNumber((int)AVVideoCodec.JPEG);
            stillImageOutput = new AVCaptureStillImageOutput()
            {
                OutputSettings = new NSDictionary()
            };

            captureSession.AddOutput(stillImageOutput);
            AddTargetOverlay();


            textOutputLabel = new UILabel(new CGRect(targetOverlayView.Frame.Width + 10, 10, 100, 100));
            textOutputLabel.TextColor = UIColor.White;
            textOutputLabel.Font = UIFont.BoldSystemFontOfSize(22);
            View.AddSubview(textOutputLabel);



            captureSession.StartRunning();
            await BufferSnapshotTimer();
        }

        /// <summary>
        /// Adds the over view image that the user aims with
        /// </summary>
        public void AddTargetOverlay()
        {
            var targetX = (View.Frame.Width - TargetOverlayWidth) / 2;
            var targetY = (View.Frame.Height - TargetOverlayHeight) / 2;

            targetOverlayView = new UIImageView(UIImage.FromFile("camera-target.png"))
            {
                Frame = new CGRect(targetX, targetY, TargetOverlayWidth, TargetOverlayHeight)
            };

            View.AddSubview(targetOverlayView);
        }

        private async Task BufferSnapshotTimer()
        {
            // For dev, 2 second delay to align the camera for a good shot
            await Task.Delay(TimeSpan.FromMilliseconds(SnapshotMilliseconds));


            while (_keepPolling)
            {
                // Update the UI (because of async/await magic, this is still in the UI thread!)
                // At this point, the imageNsData is in landscape mode (verified from base 64 string)
                var imageNsData = await GetNSDataOfCurrentImageAsync();
                var croppedImage = CropCapturedImageToTargetOverlay(imageNsData);

                AddImageToScreenHelper(croppedImage);
                var text = await ProcessOCR(croppedImage);

                if (!string.IsNullOrEmpty(text))
                {
                    textOutputLabel.Text = text;
                }
                else
                {
                    textOutputLabel.Text = "-";
                }


                if (_keepPolling)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(SnapshotMilliseconds));
                }
            }
        }

        /// <summary>
        /// Takes the snapshot from the camera and grabs it's NSData
        /// </summary>
        /// <returns>The NSD ata of current image async.</returns>
        async Task<NSData> GetNSDataOfCurrentImageAsync()
        {
            var videoConnection = stillImageOutput.ConnectionFromMediaType(AVMediaType.Video);
            videoConnection.VideoOrientation = AVCaptureVideoOrientation.Portrait;

            // Captures an image from an input device.
            var sampleBuffer = await stillImageOutput.CaptureStillImageTaskAsync(videoConnection);

            // Returns an NSData representing the jpeg image and its metadata.
            var nsData = AVCaptureStillImageOutput.JpegStillToNSData(sampleBuffer);
            //Console.WriteLine(nsData.GetBase64EncodedString(NSDataBase64EncodingOptions.None));
            return nsData;
        }




        /// <summary>
        /// Takes imagedata and crops it based on the size of the target overlay frame.
        /// </summary>
        /// <returns>The captured image to target overlay.</returns>
        /// <param name="imageData">Image data.</param>
        //https://stackoverflow.com/questions/15951746/how-to-crop-an-image-from-avcapture-to-a-rect-seen-on-the-display
        // https://forums.xamarin.com/discussion/14269/objective-c-image-crop-function
        UIImage CropCapturedImageToTargetOverlay(NSData imageData)
        {
            // This image will rotate the NSData image 90 degrees
            // so that it is in portrait mode.
            var image = UIImage.LoadFromData(imageData);


            // At this point, the base 64 string was still landscape, but can confirm very high res
            //Console.WriteLine(image.AsPNG().GetBase64EncodedString(NSDataBase64EncodingOptions.None));


            // This is the size of the final image we need
            var targetFrame = targetOverlayView.Frame;


            // NOTE: iOS stores images in landscape, even when the view is in portrait mode.
            // "...an iOS device camera always encodes pixel data in the camera sensor's native landscape orientation, along with metadata indicating the camera orientation."
            // https://developer.apple.com/documentation/uikit/uiimageorientation

            // The assumption, based on requirements of the app, is that the phone will always be in portrait mode.
            // If this wasn't the case, we'd need to calculate the ratios below differently, based on the image.Orientation prop.

            // Images are a lot bigger than the screen when stored. The aspect ratio remains the same.
            // Thus we can get the ration of that difference and apply it to our crop calculations.

            var imageWidth = image.Size.Width;
            var imageHeight = image.Size.Height;

            // We need to know the ratio of the scaling, so that we can
            // scale up the dimensions of our target frame and crop the
            // image correctly where the user pointed. Width or height will do.
            var scaleRatio = imageWidth / View.Frame.Width;
            var scaledTargetX = targetFrame.X * scaleRatio;
            var scaledTargetY = targetFrame.Y * scaleRatio;
            var scaledTargetWidth = targetFrame.Width * scaleRatio;
            var scaledTargetHeight = targetFrame.Height * scaleRatio;

            // These cordinates and size are inverted, because the source
            // image is a portrait image stored in landscape. We invert the
            // target frame so that the cropping occurs as landscape.
            var newCropRect = new CGRect(
                scaledTargetY,
                scaledTargetX,
                scaledTargetHeight,
                scaledTargetWidth
                );

            using (CGImage croppedImage = image.CGImage.WithImageInRect(newCropRect))
            {
                // Once we crop the image, we rotate it.
                return ScaleAndRotateImage(croppedImage, UIImageOrientation.Right);
            }
        }

        /// <summary>
        /// Takes an image, and runs it through Tesseract, returning
        /// the cleaned string it detected.
        /// </summary>
        /// <returns>The ocr.</returns>
        /// <param name="image">Image.</param>
        private async Task<string> ProcessOCR(UIImage image)
        {
            if (!_tesseractInitialised)
            {
                _tesseractInitialised = await tesseract.Init("eng");
                tesseract.SetWhitelist("0123456789");
            }

            bool success = await tesseract.SetImage(image.AsPNG().AsStream());

            if (success)
            {
                string textResult = tesseract.Text;
                textResult.Trim();
                textResult.Replace(" ", "");
                return textResult;
            }

            return null;
        }


        /// <summary>
        /// Authorise the camera's use.
        /// </summary>
        /// <returns>The camera use.</returns>
        async Task AuthorizeCameraUse()
        {
            var authorizationStatus = AVCaptureDevice.GetAuthorizationStatus(AVMediaType.Video);

            if (authorizationStatus != AVAuthorizationStatus.Authorized)
            {
                await AVCaptureDevice.RequestAccessForMediaTypeAsync(AVMediaType.Video);
            }
        }

        /// <summary>
        /// Setsup the camera quality
        /// </summary>
        /// <param name="device">Device.</param>
        void ConfigureCameraForDevice(AVCaptureDevice device)
        {
            var error = new NSError();
            if (device.IsFocusModeSupported(AVCaptureFocusMode.ContinuousAutoFocus))
            {
                device.LockForConfiguration(out error);
                device.FocusMode = AVCaptureFocusMode.ContinuousAutoFocus;
                device.UnlockForConfiguration();
            }
            else if (device.IsExposureModeSupported(AVCaptureExposureMode.ContinuousAutoExposure))
            {
                device.LockForConfiguration(out error);
                device.ExposureMode = AVCaptureExposureMode.ContinuousAutoExposure;
                device.UnlockForConfiguration();
            }
            else if (device.IsWhiteBalanceModeSupported(AVCaptureWhiteBalanceMode.ContinuousAutoWhiteBalance))
            {
                device.LockForConfiguration(out error);
                device.WhiteBalanceMode = AVCaptureWhiteBalanceMode.ContinuousAutoWhiteBalance;
                device.UnlockForConfiguration();
            }
        }


        /// <summary>
        /// https://forums.xamarin.com/discussion/19778/uiimage-rotation-and-transformation
        /// </summary>
        /// <returns>The and rotate image.</returns>
        /// <param name="imgRef">Image reference.</param>
        /// <param name="orIn">Or in.</param>
        UIImage ScaleAndRotateImage(CGImage imgRef, UIImageOrientation orIn)
        {
            int kMaxResolution = 2048;

            //CGImage imgRef = imageIn.CGImage;
            float width = imgRef.Width;
            float height = imgRef.Height;
            CGAffineTransform transform = CGAffineTransform.MakeIdentity();
            RectangleF bounds = new RectangleF(0, 0, width, height);

            if (width > kMaxResolution || height > kMaxResolution)
            {
                float ratio = width / height;

                if (ratio > 1)
                {
                    bounds.Width = kMaxResolution;
                    bounds.Height = bounds.Width / ratio;
                }
                else
                {
                    bounds.Height = kMaxResolution;
                    bounds.Width = bounds.Height * ratio;
                }
            }

            float scaleRatio = bounds.Width / width;
            SizeF imageSize = new SizeF(width, height);
            UIImageOrientation orient = orIn;
            float boundHeight;

            switch (orient)
            {
                case UIImageOrientation.Up:                                        //EXIF = 1
                    transform = CGAffineTransform.MakeIdentity();
                    break;

                case UIImageOrientation.UpMirrored:                                //EXIF = 2
                    transform = CGAffineTransform.MakeTranslation(imageSize.Width, 0f);
                    transform = CGAffineTransform.MakeScale(-1.0f, 1.0f);
                    break;

                case UIImageOrientation.Down:                                      //EXIF = 3
                    transform = CGAffineTransform.MakeTranslation(imageSize.Width, imageSize.Height);
                    transform = CGAffineTransform.Rotate(transform, (float)Math.PI);
                    break;

                case UIImageOrientation.DownMirrored:                              //EXIF = 4
                    transform = CGAffineTransform.MakeTranslation(0f, imageSize.Height);
                    transform = CGAffineTransform.MakeScale(1.0f, -1.0f);
                    break;

                case UIImageOrientation.LeftMirrored:                              //EXIF = 5
                    boundHeight = bounds.Height;
                    bounds.Height = bounds.Width;
                    bounds.Width = boundHeight;
                    transform = CGAffineTransform.MakeTranslation(imageSize.Height, imageSize.Width);
                    transform = CGAffineTransform.MakeScale(-1.0f, 1.0f);
                    transform = CGAffineTransform.Rotate(transform, 3.0f * (float)Math.PI / 2.0f);
                    break;

                case UIImageOrientation.Left:                                      //EXIF = 6
                    boundHeight = bounds.Height;
                    bounds.Height = bounds.Width;
                    bounds.Width = boundHeight;
                    transform = CGAffineTransform.MakeTranslation(0.0f, imageSize.Width);
                    transform = CGAffineTransform.Rotate(transform, 3.0f * (float)Math.PI / 2.0f);
                    break;

                case UIImageOrientation.RightMirrored:                             //EXIF = 7
                    boundHeight = bounds.Height;
                    bounds.Height = bounds.Width;
                    bounds.Width = boundHeight;
                    transform = CGAffineTransform.MakeScale(-1.0f, 1.0f);
                    transform = CGAffineTransform.Rotate(transform, (float)Math.PI / 2.0f);
                    break;

                case UIImageOrientation.Right:                                     //EXIF = 8
                    boundHeight = bounds.Height;
                    bounds.Height = bounds.Width;
                    bounds.Width = boundHeight;
                    transform = CGAffineTransform.MakeTranslation(imageSize.Height, 0.0f);
                    transform = CGAffineTransform.Rotate(transform, (float)Math.PI / 2.0f);
                    break;

                default:
                    throw new Exception("Invalid image orientation");
                    break;
            }

            UIGraphics.BeginImageContext(bounds.Size);

            CGContext context = UIGraphics.GetCurrentContext();

            if (orient == UIImageOrientation.Right || orient == UIImageOrientation.Left)
            {
                context.ScaleCTM(-scaleRatio, scaleRatio);
                context.TranslateCTM(-height, 0);
            }
            else
            {
                context.ScaleCTM(scaleRatio, -scaleRatio);
                context.TranslateCTM(0, -height);
            }

            context.ConcatCTM(transform);
            context.DrawImage(new RectangleF(0, 0, width, height), imgRef);

            UIImage imageCopy = UIGraphics.GetImageFromCurrentImageContext();
            UIGraphics.EndImageContext();

            return imageCopy;
        }

    }
}