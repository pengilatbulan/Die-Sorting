using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RGiesecke.DllExport;
using System.Text.RegularExpressions;
using Basler.Pylon;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using AForge;
using AForge.Imaging;
using AForge.Math;
using AForge.Vision;
using AForge.Imaging.Filters;
using AForge.Math.Geometry;
using System.Windows.Media.Imaging;

namespace Hardware.Vision
{
    public class Processing
    {
        #region Variables.
        public Bitmap image = null;
        public int imageWidth = 0;
        public int imageHeight = 0;
        public Dictionary<int, List<IntPoint>> leftEdges = new Dictionary<int, List<IntPoint>>();
        public Dictionary<int, List<IntPoint>> rightEdges = new Dictionary<int, List<IntPoint>>();
        public Dictionary<int, List<IntPoint>> topEdges = new Dictionary<int, List<IntPoint>>();
        public Dictionary<int, List<IntPoint>> bottomEdges = new Dictionary<int, List<IntPoint>>();

        public Dictionary<int, List<IntPoint>> hulls = new Dictionary<int, List<IntPoint>>();
        public Dictionary<int, List<IntPoint>> quadrilaterals = new Dictionary<int, List<IntPoint>>();

        public BlobCounter blobCounter = new BlobCounter();
        public Blob[] blobs;
        public int selectedBlobID;

        public List<AForge.Point> center;
        public List<AForge.Point> refPointList;
        public AForge.Point refPoint;
        public AForge.Point pickPoint;

        string path = "D:\\LOGIMG\\";
        string file = "";
        #endregion

        #region Enumerations.
        public enum Location
        {
            X = 0,
            Y = 1
        }
        #endregion

        #region Functions Pylon.
        public bool Grab(string CameraSelect)
        {
            bool status = false;
            try
            {
                // Create a camera object that selects the first camera device found.
                // More constructors are available for selecting a specific camera device.
                string datePatt = @"yyyyMMdd";
                

                using (Camera camera = new Camera(CameraSelect))
                {
                    // Print the model name of the camera.
                    //Console.WriteLine("Using camera {0}.", camera.CameraInfo[CameraInfoKey.ModelName]);

                    // Set the acquisition mode to free running continuous acquisition when the camera is opened.
                    camera.CameraOpened += Configuration.AcquireContinuous;

                    // Open the connection to the camera device.
                    camera.Open();

                    // The parameter MaxNumBuffer can be used to control the amount of buffers
                    // allocated for grabbing. The default value of this parameter is 10.
                    camera.Parameters[PLCameraInstance.MaxNumBuffer].SetValue(5);
                    camera.Parameters[PLCamera.PixelFormat].TrySetValue(PLCamera.PixelFormat.Mono8);
                    camera.Parameters[PLCamera.GainAuto].TrySetValue(PLCamera.GainAuto.Off);
                    camera.Parameters[PLCamera.CenterX].TrySetValue(true);
                    camera.Parameters[PLCamera.CenterY].TrySetValue(true);
                    camera.Parameters[PLCamera.ExposureTimeAbs].TrySetValue(40000.0);
                    camera.Parameters[PLCamera.ExposureTime].TrySetValue(40000.0);
                    // Setting the AOI. OffsetX, OffsetY, Width, and Height are integer parameters.
                    // On some cameras, the offsets are read-only. If they are writable, set the offsets to min.
                    camera.Parameters[PLCamera.OffsetX].TrySetToMinimum();
                    camera.Parameters[PLCamera.OffsetY].TrySetToMinimum();
                    // Some parameters have restrictions. You can use GetIncrement/GetMinimum/GetMaximum to make sure you set a valid value.
                    // Here, we let pylon correct the value if needed.
                    camera.Parameters[PLCamera.Width].SetValue(1139, IntegerValueCorrection.Nearest);
                    camera.Parameters[PLCamera.Height].SetValue(866, IntegerValueCorrection.Nearest);
                    // Start grabbing.
                    camera.StreamGrabber.Start();

                    // Grab a number of images.
                    for (int i = 0; i < 10; ++i)
                    {
                        // Wait for an image and then retrieve it. A timeout of 5000 ms is used.
                        IGrabResult grabResult = camera.StreamGrabber.RetrieveResult(3000, TimeoutHandling.ThrowException);
                        using (grabResult)
                        {
                            // Image grabbed successfully?
                            if (grabResult.GrabSucceeded)
                            {
                                if (grabResult.HasCRC && grabResult.CheckCRC() == false)
                                {
                                    Common.Reports.LogFile.Log("Image is corrupted: " + grabResult.ErrorCode + " " + grabResult.ErrorDescription);
                                }

                                // Access the image data.
                                imageWidth = grabResult.Width;
                                imageHeight = grabResult.Height;
                                int width = Convert.ToInt32(imageWidth);
                                int height = Convert.ToInt32(imageHeight);
                                //Bitmap bmp = new Bitmap(grabResult.Width, grabResult.Height);
                                if (!Directory.Exists(path))
                                {
                                    System.IO.Directory.CreateDirectory(path);
                                }

                                //file = path + DateTime.Now.ToString(datePatt);
                                if (File.Exists(@"D:\\LOGIMG\\raw.bmp")) //@"C:\tidy.exe"
                                {
                                    // If file found, delete it    
                                    File.Delete(@"D:\\LOGIMG\\raw.bmp");
                                }
                                file = path + "raw.bmp";
                                ImagePersistence.Save(ImageFileFormat.Bmp, file, grabResult);
                                
                                Common.Reports.LogFile.Log("Grab Image Succeeded.");
                                camera.StreamGrabber.Stop();
                                camera.Close();
                                return true;
                                // Display the grabbed image.
                                //ImageWindow.DisplayImage(0, grabResult);
                            }
                            else
                            {
                                Common.Reports.LogFile.Log("Failed to grab image: " + grabResult.ErrorCode +" "+ grabResult.ErrorDescription);
                                camera.StreamGrabber.Stop();
                                camera.Close();
                            }
                        }
                    }

                    // Stop grabbing.
                    camera.StreamGrabber.Stop();

                    // Close the connection to the camera device.
                    camera.Close();
                }
            }
            catch (Exception ex)
            {               
                Common.Reports.LogFile.Log("Failed to grab image: " + ex.Message);
            }
            finally
            {
            }
            return status;
        }
        #endregion

        #region Functions Vision.
        // Process image
        public void ProcessImage(Bitmap image)
        {
            int foundBlobsCount = SetImage(image);

            String blobcount = string.Format("Found blobs' count: {0}", foundBlobsCount);
            Common.Reports.LogFile.Log(blobcount);
        }
        public int SetImage(Bitmap image)
        {
            leftEdges.Clear();
            rightEdges.Clear();
            topEdges.Clear();
            bottomEdges.Clear();
            hulls.Clear();
            quadrilaterals.Clear();

            selectedBlobID = 0;

            Bitmap imageclone  = AForge.Imaging.Image.Clone( image, image.PixelFormat);
            // create filter
            Median filterMedian = new Median();
            // apply the filter
            // create filter
            Threshold filterThreshold = new Threshold(250);
            // configure filter
            // create filter
            BlobsFiltering filterBlobsFiltering = new BlobsFiltering();
            filterBlobsFiltering.CoupledSizeFiltering = false;
            filterBlobsFiltering.MinWidth = 6;  //7
            filterBlobsFiltering.MinHeight = 6; //7
            filterBlobsFiltering.MaxWidth = 8;  //8
            filterBlobsFiltering.MaxHeight = 8; //8
            // apply the filter
            filterThreshold.ApplyInPlace(imageclone);
            filterBlobsFiltering.ApplyInPlace(imageclone);
            filterMedian.ApplyInPlace(imageclone);

            //this.image  = AForge.Imaging.Image.Clone( image, PixelFormat.Format16bppGrayScale );
            //imageclone = AForge.Imaging.Image.Clone(image);
            imageWidth = imageclone.Width;
            imageHeight = imageclone.Height;

            blobCounter.ProcessImage(imageclone);
            blobs = blobCounter.GetObjectsInformation();
            center = new List<AForge.Point>();
            GrahamConvexHull grahamScan = new GrahamConvexHull();

            foreach (Blob blob in blobs)
            {
                List<IntPoint> leftEdge = new List<IntPoint>();
                List<IntPoint> rightEdge = new List<IntPoint>();
                List<IntPoint> topEdge = new List<IntPoint>();
                List<IntPoint> bottomEdge = new List<IntPoint>();

                // collect edge points
                blobCounter.GetBlobsLeftAndRightEdges(blob, out leftEdge, out rightEdge);
                blobCounter.GetBlobsTopAndBottomEdges(blob, out topEdge, out bottomEdge);
                AForge.Point centering = blob.CenterOfGravity;
                leftEdges.Add(blob.ID, leftEdge);
                rightEdges.Add(blob.ID, rightEdge);
                topEdges.Add(blob.ID, topEdge);
                bottomEdges.Add(blob.ID, bottomEdge);

                // find convex hull
                List<IntPoint> edgePoints = new List<IntPoint>();
                edgePoints.AddRange(leftEdge);
                edgePoints.AddRange(rightEdge);

                List<IntPoint> hull = grahamScan.FindHull(edgePoints);
                hulls.Add(blob.ID, hull);

                List<IntPoint> quadrilateral = null;

                // List array center of gravity
                center.Add(centering);

                // find quadrilateral  //
                if (hull.Count < 4)
                {
                    quadrilateral = new List<IntPoint>(hull);
                }
                else
                {
                    quadrilateral = PointsCloud.FindQuadrilateralCorners(hull);
                }
                quadrilaterals.Add(blob.ID, quadrilateral);

                // shift all points for vizualization
                IntPoint shift = new IntPoint(1, 1);

                PointsCloud.Shift(leftEdge, shift);
                PointsCloud.Shift(rightEdge, shift);
                PointsCloud.Shift(topEdge, shift);
                PointsCloud.Shift(bottomEdge, shift);
                PointsCloud.Shift(hull, shift);
                PointsCloud.Shift(quadrilateral, shift);
            }

            double xhair = imageWidth / 2;
            double yhair = imageHeight / 2;
            if (image.PixelFormat != PixelFormat.Format24bppRgb)
            {

                //filterBlobX(516.0, 670.0);
                //filterBlobY(360.0, 520.0);
                
                filterBlobX(516.0, 1117.0);
                filterBlobY(357.0, 460.0);
                refPointList = new List<AForge.Point>();
                //findRef(388.0, 0.5);
                findRef(20.0, 1.5);//
                findPick(refPoint.X, refPoint.Y);
            }
            //UpdatePosition();
            //Invalidate();
            //if (!Directory.Exists(path))
            //{
            //    System.IO.Directory.CreateDirectory(path);
            //}

            ////file = path + DateTime.Now.ToString(datePatt);

            file = path + "visimg.bmp";
            imageclone.Save(file);
            if (blobs.Length > 0)
            {
                return blobs.Length;
            }
            else
            {
                return 0;
            }
            
        }
        private void filterBlobX(double low, double high)
        {
            for (int b = 0; b < center.Count;)
            {
                if (center[b].X < low)
                {
                    center.RemoveAt(b);
                }
                else if (center[b].X > high)
                {
                    center.RemoveAt(b);
                }
                else
                {
                    b++;
                }
                
            }
        }

        private void filterBlobY(double low, double high)
        {
            for (int b = 0; b < center.Count;)
            {
                if (center[b].Y < low)
                {
                    center.RemoveAt(b);
                }
                else if (center[b].Y > high)
                {
                    center.RemoveAt(b);
                }
                else
                {
                    b++;
                }
            }
        }

        private void findRef(double diff, double tollerance)
        {
            for (int b = 0; b < center.Count - 1; b++)
            {
                if (((center[b + 1].X - center[b].X) > (diff - tollerance)) && ((center[b + 1].X - center[b].X) < (diff + tollerance)))
                {
                    refPointList.Add(center[b]);
                }
            }
            if (refPointList.Count > 1)
            {
                for (int b = 0; b < refPointList.Count - 1;)
                {
                    if (refPointList[b].X < refPointList[b + 1].X)
                    {
                        refPointList.RemoveAt(b + 1);
                    }
                    else if (refPointList[b].X > refPointList[b + 1].X)
                    {
                        refPointList.RemoveAt(b);
                    }
                    else
                    {
                        b++;
                    }
                }
                if (refPointList.Count == 1)
                {
                    refPoint = refPointList[0];
                }
            }
            else
            {
                //refPoint = refPointList[0]; temporary due to worng image processing
            }

        }

        private void findRefback(double diff, double tollerance)
        {
            for (int b = 0; b < center.Count - 1; b++)
            {
                if (((center[b + 1].X - center[b].X) > (diff - tollerance)) && ((center[b + 1].X - center[b].X) < (diff + tollerance)))
                {
                    refPointList.Add(center[b]);
                }
            }
            if(refPointList.Count>1)
            {                
                for (int b = 0; b < refPointList.Count - 1; b++)
                {
                    if (refPointList[b].Y < refPointList[b+1].Y)
                    {
                        refPointList.RemoveAt(b+1);
                    }
                    else
                    {
                        refPointList.RemoveAt(b);
                    }
                }
                if(refPointList.Count == 1)
                {
                    refPoint = refPointList[0];
                }
            }
            else
            {
                //refPoint = refPointList[0]; temporary due to worng image processing
            }
            
        }

        private void findPick(float PickX, float PickY)
        {
            if (refPoint.X != 0.0 && refPoint.Y != 0.0)
            {
                pickPoint.X = PickX + StaticRes.Global.PickPixel.PickX;
                pickPoint.Y = PickY - StaticRes.Global.PickPixel.PickY;
            }
        }

        public void DrawCrossHair(Bitmap bmp, double PointX, double PointY)
        {
            System.Drawing.Pen limeGreenPen = new System.Drawing.Pen(System.Drawing.Color.LimeGreen, 4);
            System.Drawing.Pen redPen = new System.Drawing.Pen(System.Drawing.Color.Red, 4);
            System.Drawing.Pen YellowPen = new System.Drawing.Pen(System.Drawing.Color.Yellow, 4);
            //Bitmap tempBitmap = AForge.Imaging.Image.Clone(bmp, bmp.PixelFormat);
            int width = bmp.Width;
            int height = bmp.Height;
            double PointXref = (double)refPoint.X;
            double PointYref = (double)refPoint.Y;
            Bitmap tempBitmap = new Bitmap(width, height);

            int x1a = (width / 2);
            int y1a = (height / 2) - 30;
            int x2a = (width / 2);
            int y2a = (height / 2) + 30;
            int x3a = (width / 2) - 30;
            int y3a = (height / 2);
            int x4a = (width / 2) + 30;
            int y4a = (height / 2);

            float x1b = (float)PointXref;
            float y1b = (float)PointYref - 30;
            float x2b = (float)PointXref;
            float y2b = (float)PointYref + 30;
            float x3b = (float)PointXref - 30;
            float y3b = (float)PointYref;
            float x4b = (float)PointXref + 30;
            float y4b = (float)PointYref;

            float x1 = (float)PointX;
            float y1 = (float)PointY - 40;
            float x2 = (float)PointX;
            float y2 = (float)PointY + 40;
            float x3 = (float)PointX - 40;
            float y3 = (float)PointY;
            float x4 = (float)PointX + 40;
            float y4 = (float)PointY;

            // Draw line to screen.
            using (var graphics = Graphics.FromImage(tempBitmap))
            {
                graphics.DrawImage(bmp, 0, 0);
                // Draw Center Image Crosshair
                graphics.DrawLine(redPen, x1a, y1a, x2a, y2a);
                graphics.DrawLine(redPen, x3a, y3a, x4a, y4a);
                // Draw Center Image Ref Crosshair
                graphics.DrawLine(YellowPen, x1b, y1b, x2b, y2b);
                graphics.DrawLine(YellowPen, x3b, y3b, x4b, y4b);
                // Draw Center Image Pick Crosshair
                graphics.DrawLine(limeGreenPen, x1, y1, x2, y2);
                graphics.DrawLine(limeGreenPen, x3, y3, x4, y4);
            }
            string path = "D:\\LOGIMG\\";
            string file = "";
            file = path + "visFinal.bmp";
            tempBitmap.Save(file);
        }
        #endregion
    }
}
