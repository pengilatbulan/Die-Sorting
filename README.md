# Die-Sorting
Die sorting is the process of removing a die, package, or device from one carrier medium (commonly a diced wafer on tape frame) and placing them into another carrier medium.
<p align="center">
    <img src="https://github.com/saadson/Die-Sorting/blob/master/die-sorting.png" width="Auto" height="Auto">
</p>
## Computer vision in Die-Sorting
This repository covers a systematic approach using C# with AFORGE.NET framework in computer vision to identify the center of die in order to accomplish repositioning of the X and Y Axises in Die Sorting.

## The Overall Process of Computer Vision
1. Grab Image
2. Image Filtering
    2.1 Median
    2.2 Threshold
    2.3 Blob Filtering
3. Segregate blobs
4. Image Center
5. Finding reference XY 
6. Find Center XY of the Die

### 1. Grab Image
Image acquisition used in the computer vision is accomplished via Basler.Pylon API. The following is the usage of Grab.
```csharp
Processing _vis = new Processing();
string cameraSelect = "21271580";   /* Serial number of the Pylon Camera */
_vis.Grab(cameraSelect);
```

<figure>
	<p align="center">
  		<img src="https://github.com/saadson/Die-Sorting/blob/master/raw.bmp" alt="Raw Image" width="450" height="Auto">
  	</p>
	<figcaption align="center">Fig.1 - Raw Image</figcaption>
	
</figure>

### 2.1 Image Filtering: Median
The median filter is normally used to reduce noise in an image, somewhat like the mean filter. However, it often does a better job than the mean filter of preserving useful detail in the image. Each pixel of the original source image is replaced with the median of neighboring pixel values. The median is calculated by first sorting all the pixel values from the surrounding neighborhood into numerical order and then replacing the pixel being considered with the middle pixel value. The filter accepts 8 bpp grayscale images and 24/32 bpp color images for processing.
```csharp
Bitmap imageclone  = AForge.Imaging.Image.Clone( image, image.PixelFormat);
Median filterMedian = new Median();
filterMedian.ApplyInPlace(imageclone);
```

### 2.2 Image Filtering: Threshold
The filter does image binarization using specified threshold value. All pixels with intensities equal or higher than threshold value are converted to white pixels. All other pixels with intensities below threshold value are converted to black pixels. The filter accepts 8 and 16 bpp grayscale images for processing. Note: Since the filter can be applied as to 8 bpp and to 16 bpp images, the ThresholdValue value should be set appropriately to the pixel format. In the case of 8 bpp images the threshold value is in the [0, 255] range, but in the case of 16 bpp images the threshold value is in the [0, 65535] range.
```csharp
Bitmap imageclone  = AForge.Imaging.Image.Clone( image, image.PixelFormat);
Threshold filterThreshold = new Threshold(250);
filterThreshold.ApplyInPlace(imageclone);
```

### 2.3 Image Filtering: Blob Filtering
The filter performs filtering of blobs by their size in the specified source image - all blobs, which are smaller or bigger then specified limits, are removed from the image.
```csharp
Bitmap imageclone  = AForge.Imaging.Image.Clone( image, image.PixelFormat);
BlobsFiltering filterBlobsFiltering = new BlobsFiltering();
filterBlobsFiltering.CoupledSizeFiltering = false;
filterBlobsFiltering.MinWidth = 6;
filterBlobsFiltering.MinHeight = 6;
filterBlobsFiltering.MaxWidth = 8;
filterBlobsFiltering.MaxHeight = 8;
filterBlobsFiltering.ApplyInPlace(imageclone);
```
### 3 Segregate blobs
In this process, we  need to further filter out blobs from min XY to max XY so that we only process the specific die.
The following is function used to filter X.
```csharp
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
```
The following is function used to filter X.
```csharp
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
```
These functions are used as the following:
```csharp
filterBlobX(516.0, 1117.0);
filterBlobY(357.0, 460.0);
```
### 4 Image Center
Image center is determined by checking the width and height of the image and devide them by 2.

### 5 Finding a single reference point of XY
In order to find the central point of the die, a reference point must be searched first. The reference point is refering to the point where it is an identical reference to the specific die. In this case, we will be using the 2nd blob on the horizontal blob at the bottom. 
