# Die-Sorting
Die sorting is the process of removing a die, package, or device from one carrier medium (commonly a diced wafer on tape frame) and placing them into another carrier medium.

![logo](https://github.com/saadson/Die-Sorting/blob/master/die-sorting.png)

## Computer vision in Die-Sorting
This repository covers a systematic approach using C# with AFORGE.NET framework in computer vision to identify the center of die in order to accomplish repositioning of the X and Y Axises in Die Sorting.

## The Overall Process of Computer Vision
1. Grab Image
2. Image Processing (Median, Threshold)
3. Blob Filtering
4. Segregate blobs
5. Image Center
6. Finding reference XY 
7. Find Center XY of the Die
8. Pixel Pitch

## Grab Image
Image acquisition used in the computer vision is accomplished via Basler.Pylon API. The following is the usage of Grab.
```csharp
Processing _vis = new Processing();
string cameraSelect = "21271580";   /* Serial number of the Pylon Camera */
_vis.Grab(cameraSelect);
```
