using System.IO;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using static Android.Views.TextureView;
using static Android.Hardware.Camera;
using Android.Hardware;
using System.Threading;
using Tesseract;
using System;
using System.Threading.Tasks;
using Tesseract.Droid;
using Android.Util;
using Android.Widget;

namespace cameraandroid
{
    //https://developer.android.com/reference/android/view/TextureView.html
    [Activity (Label = "Camera", MainLauncher = true, Icon = "@mipmap/icon", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class Camera : Activity, ISurfaceTextureListener, IPreviewCallback
    {
        private Android.Hardware.Camera mCamera;
        private TextureView mTextureView;
        private CameraInfo mCameraInfo;
        bool _tesseractInitialised = false;
        ITesseractApi tesseract;

        /// <summary>
        /// The frequency which a snapshot from the camera is taken and run through Tesseract OCR
        /// </summary>
        private const int SnapshotMilliseconds = 200;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            tesseract = new TesseractApi(Application.Context, AssetsDeployment.OncePerVersion);

            mTextureView = new TextureView (this) 
            {
                SurfaceTextureListener = this

            };

            SetContentView (mTextureView);
        }

        public CameraInfo GetCameraInfo()
        {
            int noOfCameras = NumberOfCameras;
            for (int i = 0; i < noOfCameras; i++) {
                var cameraInfo = new CameraInfo ();
                Android.Hardware.Camera.GetCameraInfo (i, cameraInfo);

                if(cameraInfo.Facing == CameraFacing.Back)
                {
                    return cameraInfo;
                }
            }

            return null;
        }

        public void OnSurfaceTextureAvailable (SurfaceTexture surface, int width, int height)
        {
            mCamera = Open();
            mCameraInfo = GetCameraInfo ();


            //https://www.captechconsulting.com/blogs/android-camera-orientation-made-simple
            mCamera.SetDisplayOrientation (getCorrectCameraOrientation (mCameraInfo, mCamera));

            try {
                mCamera.SetPreviewTexture (surface);
				mCamera.SetPreviewCallback(this);
                mCamera.StartPreview ();
            } catch (IOException ioe) {
                // Something bad happened
            }
        }

        private void ConfigurePreviewSize()
        {
            var cameraParams = mCamera.GetParameters();
            var supportedPreviewSizes = cameraParams.SupportedPreviewSizes;
            int minDiff = int.MaxValue;
            Android.Hardware.Camera.Size bestSize = null;

            if(Application.Context.Resources.Configuration.Orientation == Android.Content.Res.Orientation.Landscape)
            {
                foreach (Android.Hardware.Camera.Size size in supportedPreviewSizes)
                {
                    var diff = Math.Abs(size.Width - mTextureView.Width);

                    if (diff < minDiff)
                    {
                        minDiff = diff;
                        bestSize = size;
                    }
                }
            }
            else
            {
                foreach (Android.Hardware.Camera.Size size in supportedPreviewSizes)
                {
                    var diff = Math.Abs(size.Height - mTextureView.Width);

                    if (diff < minDiff)
                    {
                        minDiff = diff;
                        bestSize = size;
                    }
                }
            }

            cameraParams.SetPreviewSize(bestSize.Width, bestSize.Height);
            mCamera.SetParameters(cameraParams);
        }

        public bool OnSurfaceTextureDestroyed (SurfaceTexture surface)
        {
            mCamera.StopPreview ();
            mCamera.Release ();
            return true;
        }

        public int getCorrectCameraOrientation (CameraInfo info, Android.Hardware.Camera camera)
        {
            IWindowManager windowManager = Android.App.Application.Context.GetSystemService (Context.WindowService).JavaCast<IWindowManager> ();

            var rotation = windowManager.DefaultDisplay.Rotation;
            int degrees = 0;

            switch (rotation) {
            case SurfaceOrientation.Rotation0:
                    degrees = 0;
                    break;

                case SurfaceOrientation.Rotation90:
                    degrees = 90;
                    break;

                case SurfaceOrientation.Rotation180:
                    degrees = 180;
                    break;

                case SurfaceOrientation.Rotation270:
                    degrees = 270;
                    break;
            }

            int result;

            if (info.Facing == CameraFacing.Front) 
            {
                result = (info.Orientation + degrees) % 360;
                result = (360 - 2) % 360;
            } 
            else 
            {
                result = (info.Orientation - degrees + 360) % 360;
            }

            return result;
        }

        public void OnSurfaceTextureSizeChanged (SurfaceTexture surface, int width, int height)
        {
            // Ignored, Camera does all the work for us
        }



        public void OnSurfaceTextureUpdated (SurfaceTexture surface)
        {
            // Ignored, Camera does all the work for us
        }

        /// <summary>
        /// https://stackoverflow.com/questions/20298699/onpreviewframe-data-image-to-imageview
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="camera">Camera.</param>
        private int surfaceTextureUpdateCount = 0;

        private bool ocrRunning = false;

        public void OnPreviewFrame(byte[] data, Android.Hardware.Camera camera)
        {
            if (surfaceTextureUpdateCount == 30)
            {
                Console.WriteLine("---------------------------------");
                Console.WriteLine("Time to frame it up");



                if(!ocrRunning)
                {
                    Console.WriteLine("Can do the task");

                    ocrRunning = true;

                    var task3 = new Task(async() =>
                        {
                            var bytes = ConvertYugByetArrayJpegByteArray(data, camera);
                            var bitmap = BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length);
                            var text = await DoOCR(bytes);
                            //Console.WriteLine(text);
                            Console.WriteLine("TBase64.EncodeToString(bytes, Base64.Default)ask is done");
                        //var base64 = Base64.EncodeToString(bytes, Base64Flags.Default);
                            //Console.WriteLine(base64);

                            ocrRunning = false;
                        });
                    task3.Start();
                } else {
                    Console.WriteLine("OCR already running");
                }

                surfaceTextureUpdateCount = 0;
            }

            surfaceTextureUpdateCount++;
        }

        private byte[] ConvertYugByetArrayJpegByteArray(byte[] data, Android.Hardware.Camera camera)
        {
            var parameters = camera.GetParameters();
            int width = parameters.PreviewSize.Width;
            int height = parameters.PreviewSize.Height;

            YuvImage yuv = new YuvImage(data, parameters.PreviewFormat, width, height, null);

            var byteArrayOutputStream = new MemoryStream();
            yuv.CompressToJpeg(new Rect(0, 0, width, height), 50, byteArrayOutputStream);
            return byteArrayOutputStream.ToArray();
        }

        public async Task<string> DoOCR(byte[] bytes)
        {
            // Take the photo
            if(!_tesseractInitialised) {
                _tesseractInitialised = await tesseract.Init("eng");
            }

            bool success = await tesseract.SetImage(bytes);

            if (success)
            {
                string textResult = tesseract.Text;
                textResult.Trim();
                textResult.Replace(" ", "");
				Console.WriteLine(textResult);
                return textResult;
                // Dispatch some kind of event
                //return textResult;
            }

            return null;
        }
    }

    public static class AsyncHelper
    {
        private static readonly TaskFactory MyTaskFactory = new
            TaskFactory(CancellationToken.None,
                TaskCreationOptions.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);

        public static TResult RunSync<TResult>(Func<Task<TResult>> func)
        {
            return MyTaskFactory
                .StartNew(func)
                .Unwrap()
                .GetAwaiter()
                .GetResult();
        }

        public static void RunSync(Func<Task> func)
        {
            MyTaskFactory
                .StartNew(func)
                .Unwrap()
                .GetAwaiter()
                .GetResult();
        }
    }
}
