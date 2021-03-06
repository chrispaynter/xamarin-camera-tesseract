﻿using System; using UIKit; using Foundation; using AVFoundation; using System.Threading.Tasks; using Tesseract; using Tesseract.iOS; using CoreGraphics; using System.Drawing; using CoreVideo;
using CoreFoundation;
using CoreMedia;
using System.Linq;

namespace Camera {     public partial class ViewController : UIViewController     {         public ViewController(IntPtr handle) : base(handle)         {         }          public override void ViewDidLoad()
        {             // Initialise the OCRCameraView to fit in the parent View frame             OCRCameraView cameraView = new OCRCameraView()
            {
                Frame = new CGRect(                     this.View.Frame.X,                     this.View.Frame.Y,                     this.View.Frame.Width,                     this.View.Frame.Height                 )
            };              // Basic configuration examples             //cameraView.TargetOverlayWidth = 200;             //cameraView.TargetOverlayHeight = 100;             //cameraView.VideoFrameInterval = 20;             cameraView.OCRWhiteList = "0123456789";             cameraView.DebugMode = true; // Set this to false for production              cameraView.OnOCRTextReceivedAsync += async (string[] results) =>             {                 await Task.Delay(1000); // This is just to make the method async                 Console.WriteLine("Async version of OCR Text Recieved: ");                 Console.WriteLine(string.Join(",", results));             };              cameraView.OnOCRTextReceived += (string[] results) =>
            {
                Console.WriteLine("Sync version of OCR Text Recieved: ");                 Console.WriteLine(string.Join(",", results));             };              // Add the camera view to your view. From this point,             // when the view gets added to the screen it will configure itself             this.View.AddSubview(cameraView);             base.ViewDidLoad();
        }          public override void DidReceiveMemoryWarning()         {             base.DidReceiveMemoryWarning();         }     } } 