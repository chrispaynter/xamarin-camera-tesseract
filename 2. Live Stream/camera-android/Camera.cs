
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using static Android.Views.TextureView;
using static Android.Hardware.Camera;
using System.Threading.Tasks;
using Android.Hardware;
using System.Threading;
using Neteril.Android;

namespace cameraandroid
{
   
    //https://developer.android.com/reference/android/view/TextureView.html
    [Activity (Label = "Camera", MainLauncher = true, Icon = "@mipmap/icon", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class Camera : Activity, ISurfaceTextureListener, IPictureCallback, IShutterCallback
    {
        private Android.Hardware.Camera mCamera;
        private TextureView mTextureView;
        private CameraInfo mCameraInfo;
        bool _keepPolling = true;

        /// <summary>
        /// The frequency which a snapshot from the camera is taken and run through Tesseract OCR
        /// </summary>
        private const int SnapshotMilliseconds = 200;

        protected async override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            mTextureView = new TextureView (this) 
            {
                SurfaceTextureListener = this
            };

            SetContentView (mTextureView);

            //using (var scope = ActivityScope.Of(this))
                    //await BufferSnapshotTimer();
        }

        public CameraInfo GetCameraInfo()
        {
            int noOfCameras = Android.Hardware.Camera.NumberOfCameras;
            for (int i = 0; i < noOfCameras; i++) {
                var cameraInfo = new CameraInfo ();
                Android.Hardware.Camera.GetCameraInfo (i, cameraInfo);

                if(cameraInfo.Facing == Android.Hardware.CameraFacing.Back)
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


            } catch (IOException ioe) {
                // Something bad happened
            }
        }

        public async Task BufferSnapshotTimer()
        {
            
            while (_keepPolling)
            {
                
                Handler h = new Handler();
                Action myAction = () =>
                {
                    mCamera.TakePicture(this, this, this);
                };

                h.PostDelayed(myAction, 1000);
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

        public void OnSurfaceTextureUpdated (SurfaceTexture surface)
        {
            // Invoked every time there's a new Camera preview frame
        }

        public void OnPictureTaken(byte[] data, Android.Hardware.Camera camera)
        {
        }

        public void OnShutter()
        {
        }


    }
}
