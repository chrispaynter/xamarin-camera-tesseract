using System;
using Android.Graphics;
using Android.Views;
using static Android.Views.TextureView;

namespace cameraandroid
{
    public class OCRCameraView:View,ISurfaceTextureListener
    {
        TextureView mTextureView;

        public OCRCameraView()
        {
            mTextureView = new TextureView(this)
            {
                SurfaceTextureListener = this
            };
        }

        public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
        {
            throw new NotImplementedException();
        }

        public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
        {
            throw new NotImplementedException();
        }

        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
        {
            throw new NotImplementedException();
        }

        public void OnSurfaceTextureUpdated(SurfaceTexture surface)
        {
            throw new NotImplementedException();
        }
    }
}
