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

namespace cameraandroid
{
    //https://developer.android.com/reference/android/view/TextureView.html
    [Activity (Label = "Camera", MainLauncher = true, Icon = "@mipmap/icon", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class Camera : Activity, ISurfaceTextureListener, IPreviewCallback
    {
        private Android.Hardware.Camera mCamera;
        private TextureView mTextureView;
        private CameraInfo mCameraInfo;

        /// <summary>
        /// The frequency which a snapshot from the camera is taken and run through Tesseract OCR
        /// </summary>
        private const int SnapshotMilliseconds = 200;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            mTextureView = new TextureView (this) 
            {
                SurfaceTextureListener = this
            };

            SetContentView (mTextureView);
        }

        public CameraInfo GetCameraInfo()
        {
            int noOfCameras = Android.Hardware.Camera.NumberOfCameras;
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
            mCamera = Android.Hardware.Camera.Open ();
            mCameraInfo = GetCameraInfo ();


            //https://www.captechconsulting.com/blogs/android-camera-orientation-made-simple
            mCamera.SetDisplayOrientation (getCorrectCameraOrientation (mCameraInfo, mCamera));

            try {
                mCamera.SetPreviewTexture (surface);
                mCamera.StartPreview ();

                mCamera.SetPreviewCallback(this);

            } catch (IOException ioe) {
                // Something bad happened
            }
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

            if (info.Facing == Android.Hardware.CameraFacing.Front) 
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


        private int surfaceTextureUpdateCount = 0;

        public void OnSurfaceTextureUpdated (SurfaceTexture surface)
        {
            if(surfaceTextureUpdateCount == 30)
            {
                surfaceTextureUpdateCount = 0;
            }
            surfaceTextureUpdateCount++;
        }

        /// <summary>
        /// https://stackoverflow.com/questions/20298699/onpreviewframe-data-image-to-imageview
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="camera">Camera.</param>
        public void OnPreviewFrame(byte[] data, Android.Hardware.Camera camera)
        {
            var parameters = camera.GetParameters();
            int width = parameters.PreviewSize.Width;
            int height = parameters.PreviewSize.Height;

            YuvImage yuv = new YuvImage(data, parameters.PreviewFormat, width, height, null);

            var byteArrayOutputStream = new MemoryStream();
            yuv.CompressToJpeg(new Rect(0, 0, width, height), 50, byteArrayOutputStream);
            byte[] bytes = byteArrayOutputStream.ToArray();
            var bitmap = BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length);
        }
    }

    //public class PictureCallbackClass : Java.Lang.Object, IPictureCallback, IShutterCallback 
    //{
    //    Android.Hardware.Camera camera;
    //    ITesseractApi tesseract;
    //    bool _tesseractInitialised = false;


    //    public PictureCallbackClass(Android.Hardware.Camera camera)
    //    {
    //        this.camera = camera;
    //        tesseract = new TesseractApi(Application.Context, AssetsDeployment.OncePerVersion);
    //    }

    //    public void OnPictureTaken(byte[] data, Android.Hardware.Camera camera)
    //    {
    //        if(data != null) 
    //        {
    //            Thread t = new Thread(async () =>
    //            {
    //                // Take the photo
    //                if(!_tesseractInitialised) {
    //                    _tesseractInitialised = await tesseract.Init("eng");
    //                }

    //                bool success = await tesseract.SetImage(data);

    //                if (success)
    //                {
    //                    string textResult = tesseract.Text;
    //                    textResult.Trim();
    //                    textResult.Replace(" ", "");

    //                    // Dispatch some kind of event
    //                    //return textResult;
    //                }

    //            });

    //            t.IsBackground = true;
    //            t.Start();

    //            camera.StartPreview();   
    //        }
    //    }

    //    public void OnShutter()
    //    {
    //    }
    //}
}
