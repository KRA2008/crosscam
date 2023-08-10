using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AutoAlignment;
using CrossCam.Model;
using CrossCam.Wrappers;
#if !__NO_EMGU__
using System.Drawing;
using CrossCam.Page;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Microsoft.AppCenter.Crashes;
#endif
using SkiaSharp;
using Xamarin.Forms;
using Math = System.Math;
#if __ANDROID__
using SkiaSharp.Views.Android;
#elif __IOS__
using SkiaSharp.Views.iOS;
using Xamarin.Forms.Shapes;
#endif

[assembly: Dependency(typeof(OpenCv))]
namespace AutoAlignment
{
    public class OpenCv : IOpenCv
    {
        public bool IsOpenCvSupported()
        {
#if __NO_EMGU__
            return false;

#else
            try
            {
                using (new Mat())
                {
                }
            }
            catch
            {
                return false;
            }

            return true;
#endif
        }

        public AlignedResult CreateAlignedSecondImageEcc(SKBitmap firstImage, SKBitmap secondImage, AlignmentSettings settings)
        {
#if __NO_EMGU__
            return null;
#else
            var topDownsizeFactor = settings.DownsizePercentage / 100f;
            
            using var mat1 = new Mat();
            using var mat2 = new Mat();
            using var warpMatrix = Mat.Eye(
                settings.EccMotionType == (uint) MotionType.Homography ? 3 : 2, 3,
                DepthType.Cv32F, 1);
            var termCriteria =
                new MCvTermCriteria((int) settings.EccIterations, Math.Pow(10, -settings.EccEpsilonLevel));
            double ecc = 0;
            for (var ii = (int)settings.EccPyramidLayers - 1; ii >= 0; ii--)
            {
                var downsize = topDownsizeFactor / Math.Pow(2, ii);
                CvInvoke.Imdecode(GetBytes(firstImage, downsize), ImreadModes.Grayscale, mat1);
                CvInvoke.Imdecode(GetBytes(secondImage, downsize), ImreadModes.Grayscale, mat2);

                try
                {
                    ecc = CvInvoke.FindTransformECC(mat2, mat1, warpMatrix, (MotionType) settings.EccMotionType,
                        termCriteria);
                }
                catch (CvException e)
                {
                    Crashes.TrackError(e);
                    Debug.WriteLine(e);
                    if (e.Status == (int)ErrorCodes.StsNoConv)
                    {
                        return null;
                    }
                    throw;
                }

                if (warpMatrix.IsEmpty)
                {
                    return null;
                }

                unsafe
                {
                    var ptr = (float*)warpMatrix.DataPointer.ToPointer(); //ScaleX
                    ptr++; //SkewX
                    ptr++; //TransX
                    *ptr *= 2; //scale up the shifting
                    ptr++; //SkewY
                    ptr++; //ScaleY
                    ptr++; //TransY
                    *ptr *= 2; //scale up the shifting
                }
            }

            var lastUpscaleFactor = 1 / (2 * topDownsizeFactor);
            ScaleUpCvMatOfFloats(warpMatrix, lastUpscaleFactor);

            if (ecc * 100 < settings.EccThresholdPercentage)
            {
                return null;
            }

            var result =  new AlignedResult
            {
                TransformMatrix1 = SKMatrix.CreateIdentity(),
                TransformMatrix2 = ConvertCvMatToSkMatrix(warpMatrix)
            };

            if (settings.DrawResultWarpedByOpenCv)
            {
                Mat fullSizeColor1 = new Mat(), fullSizeColor2 = new Mat();
                CvInvoke.Imdecode(GetBytes(firstImage, 1), ImreadModes.Color, fullSizeColor1);
                CvInvoke.Imdecode(GetBytes(secondImage, 1), ImreadModes.Color, fullSizeColor2);
                AddWarpedToResult(fullSizeColor1, fullSizeColor2, Mat.Eye(2, 3, DepthType.Cv32F, 1), warpMatrix, result);
                result.MethodName = "ECC";
            }

            return result;
#endif
        }

        public AlignedResult CreateAlignedSecondImageKeypoints(SKBitmap firstImage, SKBitmap secondImage,
            AlignmentSettings settings, bool keystoneRightOnFirst)
        {
#if __NO_EMGU__
            return null;
#else
            var stopwatch = Stopwatch.StartNew();

            var result = new AlignedResult();

            using var detector = new ORBDetector();
            var readMode = settings.ReadModeColor ? ImreadModes.Color : ImreadModes.Grayscale;

            using var image1Mat = new Mat();
            using var descriptors1 = new Mat();
            using var allKeyPointsVector1 = new VectorOfKeyPoint();
            CvInvoke.Imdecode(GetBytes(firstImage, settings.DownsizePercentage / 100d), readMode, image1Mat);
            Debug.WriteLine("### decode: " + stopwatch.ElapsedTicks);
            stopwatch.Restart();
            detector.DetectAndCompute(image1Mat, null, allKeyPointsVector1, descriptors1, false);
            Debug.WriteLine("### detect: " + stopwatch.ElapsedTicks);

            using var image2Mat = new Mat();
            using var descriptors2 = new Mat();
            using var allKeyPointsVector2 = new VectorOfKeyPoint();
            CvInvoke.Imdecode(GetBytes(secondImage, settings.DownsizePercentage / 100d), readMode, image2Mat);
            detector.DetectAndCompute(image2Mat, null, allKeyPointsVector2, descriptors2, false);

            stopwatch.Restart();
            var thresholdDistance = Math.Sqrt(Math.Pow(firstImage.Width, 2) + Math.Pow(firstImage.Height, 2)) * settings.PhysicalDistanceThreshold;

            using var distanceThresholdMask = new Mat(allKeyPointsVector2.Size, allKeyPointsVector1.Size, DepthType.Cv8U, 1);
            if (!settings.UseCrossCheck)
            {
                unsafe
                {
                    var maskPtr = (byte*)distanceThresholdMask.DataPointer.ToPointer();
                    for (var i = 0; i < allKeyPointsVector2.Size; i++)
                    {
                        var keyPoint2 = allKeyPointsVector2[i];
                        for (var j = 0; j < allKeyPointsVector1.Size; j++)
                        {
                            var keyPoint1 = allKeyPointsVector1[j];
                            var physicalDistance = CalculatePhysicalDistanceBetweenPoints(keyPoint2.Point, keyPoint1.Point);
                            if (physicalDistance < thresholdDistance)
                            {
                                *maskPtr = 255;
                            }
                            else
                            {
                                *maskPtr = 0;
                            }

                            maskPtr++;
                        }
                    }
                }
            }
            Debug.WriteLine("### physical distance checks: " + stopwatch.ElapsedTicks);
            stopwatch.Restart();

            using var vectorOfMatches = new VectorOfVectorOfDMatch();
            using var matcher = new BFMatcher(DistanceType.Hamming, settings.UseCrossCheck);
            matcher.Add(descriptors1);
            matcher.KnnMatch(descriptors2, vectorOfMatches, settings.UseCrossCheck ? 1 : 2, settings.UseCrossCheck ? new VectorOfMat() : new VectorOfMat(distanceThresholdMask));
            Debug.WriteLine("### match: " + stopwatch.ElapsedTicks);
            stopwatch.Restart();

            var goodMatches = new List<MDMatch>();
            for (var i = 0; i < vectorOfMatches.Size; i++)
            {
                if (vectorOfMatches[i].Size == 0)
                {
                    continue;
                }

                if(vectorOfMatches[i].Size == 1 || 
                   (vectorOfMatches[i][0].Distance < settings.RatioTest * vectorOfMatches[i][1].Distance)) //make sure matches are unique
                {
                    goodMatches.Add(vectorOfMatches[i][0]);
                }
            }

            if (goodMatches.Count < settings.MinimumKeypoints) return null;

            var pairedPoints = new List<PointForCleaning>();
            for (var ii = 0; ii < goodMatches.Count; ii++)
            {
                var keyPoint1 = allKeyPointsVector1[goodMatches[ii].TrainIdx];
                var keyPoint2 = allKeyPointsVector2[goodMatches[ii].QueryIdx];
                pairedPoints.Add(new PointForCleaning
                {
                    KeyPoint1 = keyPoint1,
                    KeyPoint2 = keyPoint2,
                    Data = new KeyPointOutlierDetectorData
                    {
                        Distance = (float)CalculatePhysicalDistanceBetweenPoints(keyPoint1.Point, keyPoint2.Point),
                        Slope = (keyPoint2.Point.Y - keyPoint1.Point.Y) / (keyPoint2.Point.X - keyPoint1.Point.X)
                    },
                    Match = new MDMatch
                    {
                        Distance = goodMatches[ii].Distance,
                        ImgIdx = goodMatches[ii].ImgIdx,
                        QueryIdx = ii,
                        TrainIdx = ii
                    }
                });
            }
            Debug.WriteLine("### match distance: " + stopwatch.ElapsedTicks);
            stopwatch.Restart();

            if (settings.DrawKeypointMatches)
            {
                result.DirtyMatchesCount = pairedPoints.Count;
                result.DrawnDirtyMatches = DrawMatches(firstImage, secondImage, pairedPoints, settings.DownsizePercentage);
                Debug.WriteLine("### draw matches: " + stopwatch.ElapsedTicks);
                stopwatch.Restart();
            }

            if (settings.DiscardOutliersByDistance || settings.DiscardOutliersBySlope)
            {
                //Debug.WriteLine("DIRTY POINTS START (ham,dist,slope,ydiff), count: " + pairedPoints.Count);
                //foreach (var pointForCleaning in pairedPoints)
                //{
                //    Debug.WriteLine(pointForCleaning.Match.Distance  + "," + pointForCleaning.Data.Distance + "," + pointForCleaning.Data.Slope + "," + Math.Abs(pointForCleaning.KeyPoint1.Point.Y - pointForCleaning.KeyPoint2.Point.Y));
                //}

                //Debug.WriteLine("DIRTY PAIRS:");
                //PrintPairs(pairedPoints);

                if (settings.DiscardOutliersByDistance)
                {
                    // reject distances and slopes more than some number of standard deviations from the median
                    var medianDistance = pairedPoints.OrderBy(p => p.Data.Distance).ElementAt(pairedPoints.Count / 2).Data.Distance;
                    var distanceStdDev = CalcStandardDeviation(pairedPoints.Select(p => p.Data.Distance).ToArray());
                    pairedPoints = pairedPoints.Where(p => Math.Abs(p.Data.Distance - medianDistance) < Math.Abs(distanceStdDev * (settings.KeypointOutlierThresholdTenths / 10d))).ToList();
                    //Debug.WriteLine("Median Distance: " + medianDistance);
                    //Debug.WriteLine("Distance Cleaned Points count: " + pairedPoints.Count);
                }

                if (settings.DiscardOutliersBySlope)
                {
                    var validSlopes = pairedPoints.Where(p => !float.IsNaN(p.Data.Slope) && float.IsFinite(p.Data.Slope)).ToArray();
                    var medianSlope = validSlopes.OrderBy(p => p.Data.Slope).ElementAt(validSlopes.Length / 2).Data.Slope;
                    var slopeStdDev = CalcStandardDeviation(validSlopes.Select(p => p.Data.Slope).ToArray());
                    pairedPoints = validSlopes.Where(p => Math.Abs(p.Data.Slope - medianSlope) < Math.Abs(slopeStdDev * (settings.KeypointOutlierThresholdTenths / 10d))).ToList();
                    //Debug.WriteLine("Median Slope: " + medianSlope);
                    //Debug.WriteLine("Slope Cleaned Points count: " + pairedPoints.Count);
                }

                //Debug.WriteLine("CLEAN POINTS START (ham,dist,slope,ydiff), count: " + pairedPoints.Count);
                //foreach (var pointForCleaning in pairedPoints)
                //{
                //    Debug.WriteLine(pointForCleaning.Match.Distance + "," + pointForCleaning.Data.Distance + "," + pointForCleaning.Data.Slope + "," + Math.Abs(pointForCleaning.KeyPoint1.Point.Y - pointForCleaning.KeyPoint2.Point.Y));
                //}

                //Debug.WriteLine("CLEANED PAIRS:");
                //PrintPairs(pairedPoints);

                for (var ii = 0; ii < pairedPoints.Count; ii++)
                {
                    var oldMatch = pairedPoints[ii].Match;
                    pairedPoints[ii].Match = new MDMatch
                    {
                        Distance = oldMatch.Distance,
                        ImgIdx = oldMatch.ImgIdx,
                        QueryIdx = ii,
                        TrainIdx = ii
                    };
                }

                if (settings.DrawKeypointMatches)
                {
                    result.CleanMatchesCount = pairedPoints.Count;
                    result.DrawnCleanMatches = DrawMatches(firstImage, secondImage, pairedPoints, settings.DownsizePercentage);
                }
            }



            if (settings.TransformationFindingMethod == (uint)TransformationFindingMethod.BinarySearch)
            {
                var points1 = pairedPoints.Select(p => new SKPoint(p.KeyPoint1.Point.X, p.KeyPoint1.Point.Y)).ToArray();
                var points2 = pairedPoints.Select(p => new SKPoint(p.KeyPoint2.Point.X, p.KeyPoint2.Point.Y)).ToArray();


                var translation1 = FindVerticalTranslation(points1, points2, secondImage);
                var translated1 = SKMatrix.CreateTranslation(0, translation1);
                points2 = translated1.MapPoints(points2);

                var rotation1 = FindRotation(points1, points2, secondImage);
                var rotated1 = SKMatrix.CreateRotation(rotation1, secondImage.Width / 2f, secondImage.Height / 2f);
                points2 = rotated1.MapPoints(points2);

                var zoom1 = FindZoom(points1, points2, secondImage);
                var zoomed1 = SKMatrix.CreateScale(zoom1, zoom1, secondImage.Width / 2f, secondImage.Height / 2f);
                points2 = zoomed1.MapPoints(points2);



                var translation2 = FindVerticalTranslation(points1, points2, secondImage);
                var translated2 = SKMatrix.CreateTranslation(0, translation2);
                points2 = translated2.MapPoints(points2);

                var rotation2 = FindRotation(points1, points2, secondImage);
                var rotated2 = SKMatrix.CreateRotation(rotation2, secondImage.Width / 2f, secondImage.Height / 2f);
                points2 = rotated2.MapPoints(points2);

                var zoom2 = FindZoom(points1, points2, secondImage);
                var zoomed2 = SKMatrix.CreateScale(zoom2, zoom2, secondImage.Width / 2f, secondImage.Height / 2f);
                points2 = zoomed2.MapPoints(points2);



                var translation3 = FindVerticalTranslation(points1, points2, secondImage);
                var translated3 = SKMatrix.CreateTranslation(0, translation3);
                points2 = translated3.MapPoints(points2);

                var rotation3 = FindRotation(points1, points2, secondImage);
                var rotated3 = SKMatrix.CreateRotation(rotation3, secondImage.Width / 2f, secondImage.Height / 2f);
                points2 = rotated3.MapPoints(points2);

                var zoom3 = FindZoom(points1, points2, secondImage);
                var zoomed3 = SKMatrix.CreateScale(zoom3, zoom3, secondImage.Width / 2f, secondImage.Height / 2f);
                points2 = zoomed3.MapPoints(points2);


                var keystonedFirst1 = SKMatrix.CreateIdentity();
                var keystonedFirst2 = SKMatrix.CreateIdentity();
                var keystonedFirst3 = SKMatrix.CreateIdentity();
                var keystonedSecond1 = SKMatrix.CreateIdentity();
                var keystonedSecond2 = SKMatrix.CreateIdentity();
                var keystonedSecond3 = SKMatrix.CreateIdentity();
                if (settings.DoKeystoneCorrection)
                {
                    var keystoneFirst1 = FindTaper(points2, points1, firstImage.Width, firstImage.Height);
                    keystonedFirst1 = CreateTaper(firstImage.Width / 2f, firstImage.Height / 2f, keystoneFirst1);
                    points1 = keystonedFirst1.MapPoints(points1);

                    var keystoneSecond1 = FindTaper(points1, points2, secondImage.Width, secondImage.Height);
                    keystonedSecond1 = CreateTaper(secondImage.Width / 2f, secondImage.Height / 2f, keystoneSecond1);
                    points2 = keystonedSecond1.MapPoints(points2);

                    var keystoneFirst2 = FindTaper(points2, points1, firstImage.Width, firstImage.Height);
                    keystonedFirst2 = CreateTaper(firstImage.Width / 2f, firstImage.Height / 2f, keystoneFirst2);
                    points1 = keystonedFirst2.MapPoints(points1);

                    var keystoneSecond2 = FindTaper(points1, points2, secondImage.Width, secondImage.Height);
                    keystonedSecond2 = CreateTaper(secondImage.Width / 2f, secondImage.Height / 2f, keystoneSecond2);
                    points2 = keystonedSecond2.MapPoints(points2);

                    var keystoneFirst3 = FindTaper(points2, points1, firstImage.Width, firstImage.Height);
                    keystonedFirst3 = CreateTaper(firstImage.Width / 2f, firstImage.Height / 2f, keystoneFirst3);
                    points1 = keystonedFirst3.MapPoints(points1);

                    var keystoneSecond3 = FindTaper(points1, points2, secondImage.Width, secondImage.Height);
                    keystonedSecond3 = CreateTaper(secondImage.Width / 2f, secondImage.Height / 2f, keystoneSecond3);
                    points2 = keystonedSecond3.MapPoints(points2);
                }


                var horizontalAdj = FindHorizontalTranslation(points1, points2, secondImage);
                var horizontaled = SKMatrix.CreateTranslation(horizontalAdj, 0);
                points2 = horizontaled.MapPoints(points2);



                var tempMatrix1 = new SKMatrix(); //TODO: chain these together with PostConcat?
                SKMatrix.Concat(ref tempMatrix1, translated1, rotated1);
                var tempMatrix2 = new SKMatrix();
                SKMatrix.Concat(ref tempMatrix2, tempMatrix1, zoomed1);

                var tempMatrix3 = new SKMatrix();
                SKMatrix.Concat(ref tempMatrix3, tempMatrix2, translated2);
                var tempMatrix4 = new SKMatrix();
                SKMatrix.Concat(ref tempMatrix4, tempMatrix3, rotated2);
                var tempMatrix5 = new SKMatrix();
                SKMatrix.Concat(ref tempMatrix5, tempMatrix4, zoomed2);

                var tempMatrix6 = new SKMatrix();
                SKMatrix.Concat(ref tempMatrix6, tempMatrix5, translated3);
                var tempMatrix7 = new SKMatrix();
                SKMatrix.Concat(ref tempMatrix7, tempMatrix6, rotated3);
                var tempMatrix8 = new SKMatrix();
                SKMatrix.Concat(ref tempMatrix8, tempMatrix7, zoomed3);


                var tempMatrix9 = new SKMatrix();
                SKMatrix.Concat(ref tempMatrix9, tempMatrix8, keystonedSecond1);
                var tempMatrix10 = new SKMatrix();
                SKMatrix.Concat(ref tempMatrix10, tempMatrix9, keystonedSecond2);
                var tempMatrix11 = new SKMatrix();
                SKMatrix.Concat(ref tempMatrix11, tempMatrix10, keystonedSecond3);

                var tempMatrix12 = new SKMatrix();
                SKMatrix.Concat(ref tempMatrix12, tempMatrix11, horizontaled);

                var keyTempMatrix1 = new SKMatrix(); //TODO: make the keystones for image1
                SKMatrix.Concat(ref keyTempMatrix1, tempMatrix8, keystonedSecond1);

                var finalMatrix = tempMatrix10;
                result.TransformMatrix2 = finalMatrix;
                result.TransformMatrix1 = keystonedFirst;
                Debug.WriteLine("### homebrew matrix finding: " + stopwatch.ElapsedTicks);
                stopwatch.Restart();
            }
            else
            {
                using var points1 = new VectorOfPointF(pairedPoints.Select(p => p.KeyPoint1.Point).ToArray());
                using var points2 = new VectorOfPointF(pairedPoints.Select(p => p.KeyPoint2.Point).ToArray());

                Mat warp1, warp2;
                if (settings.TransformationFindingMethod == (uint)TransformationFindingMethod.StereoRectifyUncalibrated)
                {
                    warp1 = Mat.Eye(3, 3, DepthType.Cv64F, 1);
                    warp2 = Mat.Eye(3, 3, DepthType.Cv64F, 1);

                    using var fundamental = CvInvoke.FindFundamentalMat(points1, points2);
                    if (fundamental == null) return null;
                    var didRectify = CvInvoke.StereoRectifyUncalibrated(points1, points2, fundamental, image1Mat.Size,
                        warp1, warp2); //TODO: this works correctly when warped by OpenCv, but not Skia
                    Debug.WriteLine("### didRectify: " + didRectify);
                    
                    if (warp1.IsEmpty || warp2.IsEmpty || !didRectify) return null;
                }
                else
                {
                    warp1 = Mat.Eye(2, 3, DepthType.Cv64F, 1);
                    warp2 = (TransformationFindingMethod)settings.TransformationFindingMethod switch
                    {
                        TransformationFindingMethod.FindHomography => CvInvoke.FindHomography(points2, points1, HomographyMethod.Ransac),
                        TransformationFindingMethod.EstimateRigidPartial => CvInvoke.EstimateRigidTransform(points2, points1, false),
                        TransformationFindingMethod.EstimateRigidFull => CvInvoke.EstimateRigidTransform(points2, points1, true)
                    };

                    if (warp2 == null || warp2.IsEmpty) return null;
                }

                Debug.WriteLine("### method " + (TransformationFindingMethod)settings.TransformationFindingMethod + ": " + stopwatch.ElapsedTicks);
                stopwatch.Restart();

                if (settings.DrawResultWarpedByOpenCv)
                {
                    AddWarpedToResult(image1Mat, image2Mat, warp1, warp2, result);
                    result.MethodName = ((TransformationFindingMethod) settings.TransformationFindingMethod).ToString();
                }

                var matrix1 = ConvertCvMatToSkMatrix(warp1, 1 / (settings.DownsizePercentage / 100f));
                var matrix2 = ConvertCvMatToSkMatrix(warp2, 1 / (settings.DownsizePercentage / 100f));

                result.TransformMatrix1 = matrix1;
                result.TransformMatrix2 = matrix2;

                return result;
            }

            return result;
#endif
        }

        private void AddWarpedToResult(Mat image1Mat, Mat image2Mat, Mat warp1, Mat warp2, AlignedResult result)
        {
            using Mat warped1 = new Mat(), warped2 = new Mat();

            if (warp1.Rows == 3)
            {
                CvInvoke.WarpPerspective(image1Mat, warped1, warp1, image1Mat.Size);
            }
            else
            {
                CvInvoke.WarpAffine(image1Mat, warped1, warp1, image1Mat.Size);
            }

            if (warp2.Rows == 3)
            {
                CvInvoke.WarpPerspective(image2Mat, warped2, warp2, image2Mat.Size);
            }
            else
            {
                CvInvoke.WarpAffine(image2Mat, warped2, warp2, image2Mat.Size);
            }
#if __IOS__
            result.Warped1 = warped1.ToCGImage().ToSKBitmap();
            result.Warped2 = warped2.ToCGImage().ToSKBitmap();
#elif __ANDROID__
            result.Warped1 = warped1.ToBitmap().ToSKBitmap();
            result.Warped2 = warped2.ToBitmap().ToSKBitmap();
#endif
        }

        public SKImage AddBarrelDistortion(SKImage image, float downsize, float strength, float cxProportion)
        {
#if __NO_EMGU__
            return SKImage.Create(new SKImageInfo());
#else
            using var cvImage = new Mat();
            CvInvoke.Imdecode(image.Encode().ToArray(), ImreadModes.Color, cvImage);

            using var cameraMatrix = GetCameraMatrix(image.Width * downsize * cxProportion, image.Height * downsize / 2f);

            var size = Math.Sqrt(Math.Pow(image.Width * downsize, 2) + Math.Pow(image.Height * downsize, 2));
            var scaledCoeff = strength / Math.Pow(size, 2);
            using var distortionMatrix = GetDistortionMatrix((float)scaledCoeff);

            using var transformedImage = new Mat();
            CvInvoke.Undistort(cvImage, transformedImage, cameraMatrix, distortionMatrix);
#if __IOS__
            return transformedImage.ToCGImage().ToSKImage();
#elif __ANDROID__
            return transformedImage.ToBitmap().ToSKImage();
#endif
#endif
        }

        public byte[] GetBytes(SKBitmap bitmap, double downsize, SKFilterQuality filterQuality = SKFilterQuality.High)
        {
            //TODO: compare jpeg 100 vs png 100 vs png 0
            if (downsize == 1)
            {
                return SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 0).ToArray();
            }

            var targetWidth = (int)(bitmap.Width * downsize);
            var targetHeight = (int)(bitmap.Height * downsize);
            using var tempSurface =
                SKSurface.Create(new SKImageInfo(targetWidth, targetHeight));
            using var canvas = tempSurface.Canvas;
            canvas.Clear();

            using var paint = new SKPaint { FilterQuality = filterQuality };
            canvas.DrawBitmap(bitmap,
                SKRect.Create(0, 0, targetWidth, targetHeight),
                paint);

            using var data = tempSurface.Snapshot().Encode(SKEncodedImageFormat.Png, 0);
            return data.ToArray();
        }

#if !__NO_EMGU__
        private static Mat GetCameraMatrix(float cx, float cy)
        {
            var cameraMatrix = Mat.Eye(3, 3, DepthType.Cv32F, 1);
            unsafe
            {
                var ptr = (float*)cameraMatrix.DataPointer.ToPointer(); //fx
                ptr++; //0
                ptr++; //cx
                *ptr = cx;
                ptr++; //0
                ptr++; //fy
                ptr++; //cy
                *ptr = cy;
                ptr++; //0
                ptr++; //0
                ptr++; //1
            }

            return cameraMatrix;
        }

        private static Mat GetDistortionMatrix(float strength)
        {
            var distortionMatrix = Mat.Zeros(1, 5, DepthType.Cv32F, 1);
            unsafe
            {
                var ptr = (float*)distortionMatrix.DataPointer.ToPointer(); //k1
                *ptr = strength; //0.0000001f;
                ptr++; //k2 ?
                //*ptr = 0.0000000000001f;
                ptr++; //p1 - top and bottom keystone kind of
                //*ptr = 0.0001f;
                ptr++; //p2 - left and right keystone kind of
                //*ptr = 0.0001f;
                ptr++; //k3 ?
                //*ptr = 0.0000001f;
            }

            return distortionMatrix;
        }

        private SKBitmap DrawMatches(SKBitmap image1, SKBitmap image2, List<PointForCleaning> points, uint downsizePercentage)
        {
            using var fullSizeColor1 = new Mat();
            using var fullSizeColor2 = new Mat();
            CvInvoke.Imdecode(GetBytes(image1, downsizePercentage / 100d), ImreadModes.Color, fullSizeColor1);
            CvInvoke.Imdecode(GetBytes(image2, downsizePercentage / 100d), ImreadModes.Color, fullSizeColor2);

            using var drawnResult = new Mat();
            Features2DToolbox.DrawMatches(
                fullSizeColor1, new VectorOfKeyPoint(points.Select(m => m.KeyPoint1).ToArray()),
                fullSizeColor2, new VectorOfKeyPoint(points.Select(m => m.KeyPoint2).ToArray()),
                new VectorOfVectorOfDMatch(points.Select(p => new[] { p.Match }).ToArray()),
                drawnResult, new MCvScalar(0, 255, 0), new MCvScalar(255, 255, 0));
#if __IOS__
            return drawnResult.ToCGImage().ToSKBitmap();
#elif __ANDROID__
            return drawnResult.ToBitmap().ToSKBitmap();
#endif
        }

        private static void PrintPairs(IEnumerable<PointForCleaning> pairedPoints)
        {
            foreach (var pair in pairedPoints)
            {
                Debug.WriteLine(pair.KeyPoint1.Point.X + "," + pair.KeyPoint1.Point.Y + "," + pair.KeyPoint2.Point.X + "," + pair.KeyPoint2.Point.Y);
            }
        }

        private static double CalcStandardDeviation(float[] data)
        {
            var mean = data.Sum() / (1f * data.Length);
            var sumOfSquaresOfDeviations = data.Select(d => Math.Pow(d - mean, 2)).Sum();
            return Math.Sqrt(sumOfSquaresOfDeviations / (1f * data.Length));
        }

        private class KeyPointOutlierDetectorData
        {
            public float Distance { get; set; }
            public float Slope { get; set; }
        }

        private class PointForCleaning
        {
            public MKeyPoint KeyPoint1 { get; set; }
            public MKeyPoint KeyPoint2 { get; set; }
            public KeyPointOutlierDetectorData Data { get; set; }
            public MDMatch Match { get; set; }
        }

        private static float FindVerticalTranslation(SKPoint[] points1, SKPoint[] points2, SKBitmap secondImage)
        {
            const int TRANSLATION_TERMINATION_THRESHOLD = 1;
            var translationInc = secondImage.Height / 2;

            return BinarySearchFindComponent(points1, points2, t => SKMatrix.CreateTranslation(0, t), translationInc, TRANSLATION_TERMINATION_THRESHOLD);
        }

        private static float FindHorizontalTranslation(SKPoint[] points1, SKPoint[] points2, SKBitmap secondImage)
        {
            const int TRANSLATION_TERMINATION_THRESHOLD = 1;
            var translationInc = secondImage.Width / 2;

            return BinarySearchFindComponent(points1, points2, t => SKMatrix.CreateTranslation(t, 0), translationInc, TRANSLATION_TERMINATION_THRESHOLD, 0, true);
        }

        private static float FindRotation(SKPoint[] points1, SKPoint[] points2, SKBitmap secondImage)
        {
            const float FINAL_ROTATION_DELTA = 0.0001f;
            const float ROTATION_INC = (float)Math.PI / 2f;

            return BinarySearchFindComponent(points1, points2,
                t => SKMatrix.CreateRotation(t, secondImage.Width / 2f, secondImage.Height / 2f), ROTATION_INC,
                FINAL_ROTATION_DELTA);
        }

        private static float FindZoom(SKPoint[] points1, SKPoint[] points2, SKBitmap secondImage)
        {
            const float FINAL_ZOOM_DELTA = 0.0001f;
            const float ZOOM_INC = 1f;

            return BinarySearchFindComponent(points1, points2,
                t => SKMatrix.CreateScale(t, t, secondImage.Width / 2f,
                    secondImage.Height / 2f), ZOOM_INC, FINAL_ZOOM_DELTA, 1);
        }

        private static float FindTaper(SKPoint[] points1, SKPoint[] points2, int width, int height)
        {
            const float FINAL_TAPER_DELTA = 0.1f;
            const float TAPER_INC = 45f;
            
            return BinarySearchFindComponent(points1, points2, 
                f => CreateTaper(width / 2f, height / 2f, f), TAPER_INC,
                FINAL_TAPER_DELTA);
        }

        private static SKMatrix CreateTaper(float centerX, float centerY, float rotation)
        {
            var transform4D = SKMatrix44.CreateIdentity();
            transform4D.PostConcat(SKMatrix44.CreateTranslate(-centerX, -centerY, 0));
            transform4D.PostConcat(SKMatrix44.CreateRotationDegrees(0, 1, 0, rotation));
            transform4D.PostConcat(DrawTool.MakePerspective(centerX));
            transform4D.PostConcat(SKMatrix44.CreateTranslate(centerX, centerY, 0));
            return transform4D.Matrix;
        }

        private static float BinarySearchFindComponent(SKPoint[] basePoints, SKPoint[] pointsToTransform, Func<float, SKMatrix> testerFunction, float searchingIncrement, float terminationThreshold, float componentStart = 0, bool useXDisplacement = false)
        {
            var baseOffset = useXDisplacement ? GetNetXOffset(basePoints, pointsToTransform) : GetNetYOffset(basePoints, pointsToTransform);

            while (searchingIncrement > terminationThreshold)
            {
                var versionA = testerFunction(componentStart + searchingIncrement);
                var versionB = testerFunction(componentStart - searchingIncrement);
                var attemptA = versionA.MapPoints(pointsToTransform);
                var offsetA = useXDisplacement ? GetNetXOffset(basePoints, attemptA) : GetNetYOffset(basePoints, attemptA);
                var attemptB = versionB.MapPoints(pointsToTransform);
                var offsetB = useXDisplacement ? GetNetXOffset(basePoints, attemptB) : GetNetYOffset(basePoints, attemptB);

                if (offsetA < baseOffset)
                {
                    baseOffset = offsetA;
                    componentStart += searchingIncrement;
                }
                else if (offsetB < baseOffset)
                {
                    baseOffset = offsetB;
                    componentStart -= searchingIncrement;
                }
                searchingIncrement /= 2;
            }

            return componentStart;
        }

        private static double GetNetXOffset(SKPoint[] points1, SKPoint[] points2)
        {
            var netOffset = 0d;
            for (var ii = 0; ii < points1.Length; ii++)
            {
                netOffset += Math.Abs(points1[ii].X - points2[ii].X);
            }
            return netOffset;
        }

        private static double GetNetYOffset(SKPoint[] points1, SKPoint[] points2)
        {
            var netOffset = 0d;
            for (var ii = 0; ii < points1.Length; ii++)
            {
                netOffset += Math.Abs(points1[ii].Y - points2[ii].Y);
            }
            return netOffset;
        }

        private static SKMatrix ConvertCvMatToSkMatrix(Mat mat, float upscaleFactor = 1)
        {
            var skMatrix = SKMatrix.CreateIdentity();

            if (mat.IsEmpty) return skMatrix;

            if (mat.Cols != 3 || mat.Rows > 3)
            {
                throw new NotImplementedException();
            }

            if (mat.Depth == DepthType.Cv32F)
            {
                unsafe
                {
                    var ptr = (float*) mat.DataPointer.ToPointer();
                    skMatrix.ScaleX = *ptr;
                    ptr++;
                    skMatrix.SkewX = *ptr;
                    ptr++;
                    skMatrix.TransX = *ptr;
                    ptr++;
                    skMatrix.SkewY = *ptr;
                    ptr++;
                    skMatrix.ScaleY = *ptr;
                    ptr++;
                    skMatrix.TransY = *ptr;
                    if (mat.Rows == 3)
                    {
                        ptr++;
                        skMatrix.Persp0 = *ptr;
                        ptr++;
                        skMatrix.Persp1 = *ptr;
                        ptr++;
                        skMatrix.Persp2 = *ptr;
                    }
                }
            }
            else if (mat.Depth == DepthType.Cv64F)
            {
                unsafe
                {
                    var ptr = (double*) mat.DataPointer.ToPointer();
                    skMatrix.ScaleX = (float) *ptr;
                    ptr++;
                    skMatrix.SkewX = (float) *ptr;
                    ptr++;
                    skMatrix.TransX = (float) *ptr;
                    ptr++;
                    skMatrix.SkewY = (float) *ptr;
                    ptr++;
                    skMatrix.ScaleY = (float) *ptr;
                    ptr++;
                    skMatrix.TransY = (float) *ptr;
                    if (mat.Rows == 3)
                    {
                        ptr++;
                        skMatrix.Persp0 = (float) *ptr;
                        ptr++;
                        skMatrix.Persp1 = (float) *ptr;
                        ptr++;
                        skMatrix.Persp2 = (float) *ptr;
                    }
                }
            }

            if (upscaleFactor != 1)
            {
                var scaleMatrix = new SKMatrix(
                    upscaleFactor, 0, 0,
                    0, upscaleFactor, 0,
                    0, 0, 1);
                skMatrix = scaleMatrix.PreConcat(skMatrix).PreConcat(scaleMatrix.Invert());
            }

            return skMatrix;
        }

        private static void ScaleUpCvMatOfFloats(Mat mat, float factor)
        {
            unsafe
            {
                var ptr = (float*)mat.DataPointer.ToPointer(); //ScaleX
                ptr++; //SkewX
                ptr++; //TransX
                *ptr *= factor; //scale up the shifting
                ptr++; //SkewY
                ptr++; //ScaleY
                ptr++; //TransY
                *ptr *= factor; //scale up the shifting
            }
        }

        private static double CalculatePhysicalDistanceBetweenPoints(PointF from, PointF to)
        {
            return Math.Sqrt(Math.Pow(from.X - to.X, 2) + Math.Pow(from.Y - to.Y, 2));
        }
#endif
    }
}