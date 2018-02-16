using System; using UIKit; using Foundation; using AVFoundation; using System.Threading.Tasks; using Tesseract; using Tesseract.iOS; using CoreGraphics; using System.Drawing; using CoreVideo;
using CoreFoundation;
using CoreMedia;

namespace Camera {     public partial class ViewController : UIViewController     {         public ViewController(IntPtr handle) : base(handle)         {         }          public override void ViewDidLoad()
        {             // Initialise the OCRCameraView to fit in the parent View frame             OCRCameraView cameraView = new OCRCameraView(liveCameraStream)
            {
                Frame = new CGRect(                     this.View.Frame.X,                     this.View.Frame.Y,                     this.View.Frame.Width,                     this.View.Frame.Height                 )
            };              // Basic configuration examples             //cameraView.TargetOverlayWidth = 200;             //cameraView.TargetOverlayHeight = 100;             //cameraView.VideoFrameInterval = 20;             cameraView.DebugMode = true; // Set this to false for production              cameraView.OnOCRTextReceivedAsync += async (string text) =>             {                 await Task.Delay(1000); // This is just to make the method async                 Console.WriteLine("Async version of OCR Text Recieved: " + text);             };              cameraView.OnOCRTextReceived += (string text) =>
            {
                Console.WriteLine("Sync version of OCR Text Recieved: " + text);             };              // Add the camera view to your view. From this point,             // when the view gets added to the screen it will configure itself             this.View.AddSubview(cameraView);             base.ViewDidLoad();
        }          public override void DidReceiveMemoryWarning()         {             base.DidReceiveMemoryWarning();         }     } } 