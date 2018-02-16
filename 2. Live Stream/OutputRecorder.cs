using System;
using System.Threading.Tasks;
using AVFoundation;
using CoreGraphics;
using CoreMedia;
using CoreVideo;
using UIKit;

namespace Camera
{

    /// <summary>
    /// https://developer.xamarin.com/guides/ios/user_interface/controls/intro_to_manual_camera_controls/
    /// </summary>
    public class OutputRecorder : AVCaptureVideoDataOutputSampleBufferDelegate
    {
        public delegate Task OnFrameRecievedEvent(UIImage image);
        public event OnFrameRecievedEvent OnFrameRecieved;
        private int frameCount = 0;
        private readonly int frameInterval;
        public UIView View { get; set; }

        /// <param name="view">View.</param>
        /// <param name="frameInterval">The number of frames to wait between capturing firing the OnFrameRecieved event.</param>
        public OutputRecorder(UIView view, int frameInterval = 20)
        {
            View = view;
            this.frameInterval = frameInterval;
        }

        private UIImage GetImageFromSampleBuffer(CMSampleBuffer sampleBuffer)
        {

            // Get a pixel buffer from the sample buffer
            using (var pixelBuffer = sampleBuffer.GetImageBuffer() as CVPixelBuffer)
            {
                // Lock the base address
                pixelBuffer.Lock(CVPixelBufferLock.None);

                // Prepare to decode buffer
                var flags = CGBitmapFlags.PremultipliedFirst | CGBitmapFlags.ByteOrder32Little;

                // Decode buffer - Create a new colorspace
                using (var cs = CGColorSpace.CreateDeviceRGB())
                {

                    // Create new context from buffer
                    using (var context = new CGBitmapContext(pixelBuffer.BaseAddress,
                        pixelBuffer.Width,
                        pixelBuffer.Height,
                        8,
                        pixelBuffer.BytesPerRow,
                        cs,
                        (CGImageAlphaInfo)flags))
                    {

                        // Get the image from the context
                        using (var cgImage = context.ToImage())
                        {

                            // Unlock and return image
                            pixelBuffer.Unlock(CVPixelBufferLock.None);
                            return UIImage.FromImage(cgImage);
                        }
                    }
                }
            }
        }

        public override void DidOutputSampleBuffer(AVCaptureOutput captureOutput, CoreMedia.CMSampleBuffer sampleBuffer, AVCaptureConnection connection)
        {
            // Trap all errors
            try
            {
                if (frameCount >= frameInterval)
                {
                    // Grab an image from the buffer
                    var image = GetImageFromSampleBuffer(sampleBuffer);


                    // Display the image
                    if (View != null)
                    {
                        View.BeginInvokeOnMainThread(async () => {

                            await OnFrameRecieved(image);


                            //// Set the image
                            //if (DisplayView.Image != null) DisplayView.Image.Dispose();
                            //DisplayView.Image = image;

                            //// Rotate image to the correct display orientation
                            //DisplayView.Transform = CGAffineTransform.MakeRotation((float)Math.PI / 2);
                        });
                    }

                    frameCount = 0;
                }
                frameCount++;

                // IMPORTANT: You must release the buffer because AVFoundation has a fixed number
                // of buffers and will stop delivering frames if it runs out.
                sampleBuffer.Dispose();

            }
            catch (Exception e)
            {
                // Report error
                Console.WriteLine("Error sampling buffer: {0}", e.Message);
            }
        }
    }
}
