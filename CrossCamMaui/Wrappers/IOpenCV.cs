using CrossCam.Model;
using SkiaSharp;
using System.Diagnostics;
#if !__NO_EMGU__
using CrossCam.Page;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Microsoft.AppCenter.Crashes;
#endif
using Color = System.Drawing.Color;
using Math = System.Math;
using PointF = System.Drawing.PointF;
#if __ANDROID__
using SkiaSharp.Views.Android;
#elif __IOS__
using SkiaSharp.Views.iOS;
#endif

namespace CrossCam.Wrappers
{
    public interface IOpenCv
    {
        bool IsOpenCvSupported();

        AlignedResult CreateAlignedSecondImageEcc(SKBitmap firstImage, SKBitmap secondImage,
            AlignmentSettings settings);
        AlignedResult CreateAlignedSecondImageKeypoints(SKBitmap firstImage, SKBitmap secondImage,
            AlignmentSettings settings, bool keystoneRightOnFirst);
        SKImage AddBarrelDistortion(SKImage originalImage, float downsize, float strength, float cxProportion);
        byte[] GetBytes(SKBitmap bitmap, double downsize, SKFilterQuality filterQuality = SKFilterQuality.High);
    }

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

        public AlignedResult CreateAlignedSecondImageEcc(SKBitmap firstImage, SKBitmap secondImage,
            AlignmentSettings settings)
        {
#if __NO_EMGU__
            return null;
#else
            var topDownsizeFactor = settings.DownsizePercentage / 100f;

            using var mat1 = new Mat();
            using var mat2 = new Mat();
            using var warpMatrix = Mat.Eye(
                settings.EccMotionType == (uint)MotionType.Homography ? 3 : 2, 3,
                DepthType.Cv32F, 1);
            var termCriteria =
                new MCvTermCriteria((int)settings.EccIterations, Math.Pow(10, -settings.EccEpsilonLevel));
            double ecc = 0;
            for (var ii = (int)settings.EccPyramidLayers - 1; ii >= 0; ii--)
            {
                var downsize = topDownsizeFactor / Math.Pow(2, ii);
                CvInvoke.Imdecode(GetBytes(firstImage, downsize), ImreadModes.Grayscale, mat1);
                CvInvoke.Imdecode(GetBytes(secondImage, downsize), ImreadModes.Grayscale, mat2);

                try
                {
                    ecc = CvInvoke.FindTransformECC(mat2, mat1, warpMatrix, (MotionType)settings.EccMotionType,
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

            var result = new AlignedResult
            {
                TransformMatrix1 = SKMatrix.CreateIdentity(),
                TransformMatrix2 = ConvertCvMatToSkMatrix(warpMatrix),
                Confidence = (int)(ecc * 100)
            };

            if (settings.DrawResultWarpedByOpenCv)
            {
                Mat fullSizeColor1 = new Mat(), fullSizeColor2 = new Mat();
                CvInvoke.Imdecode(GetBytes(firstImage, 1), ImreadModes.Color, fullSizeColor1);
                CvInvoke.Imdecode(GetBytes(secondImage, 1), ImreadModes.Color, fullSizeColor2);
                AddWarpedToResult(fullSizeColor1, fullSizeColor2, Mat.Eye(2, 3, DepthType.Cv32F, 1), warpMatrix,
                    result);
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
            var result = new AlignedResult();

            using var detector = new ORB();
            var readMode = settings.ReadModeColor ? ImreadModes.Color : ImreadModes.Grayscale;

            using var image1Mat = new Mat();
            using var descriptors1 = new Mat();
            using var allKeyPointsVector1 = new VectorOfKeyPoint();
            CvInvoke.Imdecode(GetBytes(firstImage, settings.DownsizePercentage / 100d), readMode, image1Mat);
            detector.DetectAndCompute(image1Mat, null, allKeyPointsVector1, descriptors1, false);

            using var image2Mat = new Mat();
            using var descriptors2 = new Mat();
            using var allKeyPointsVector2 = new VectorOfKeyPoint();
            CvInvoke.Imdecode(GetBytes(secondImage, settings.DownsizePercentage / 100d), readMode, image2Mat);
            detector.DetectAndCompute(image2Mat, null, allKeyPointsVector2, descriptors2, false);

            var thresholdDistance = Math.Sqrt(Math.Pow(firstImage.Width, 2) + Math.Pow(firstImage.Height, 2)) *
                                    settings.PhysicalDistanceThreshold;

            using var distanceThresholdMask =
                new Mat(allKeyPointsVector2.Size, allKeyPointsVector1.Size, DepthType.Cv8U, 1);
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
                            var physicalDistance =
                                CalculatePhysicalDistanceBetweenPoints(keyPoint2.Point, keyPoint1.Point);
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

            using var vectorOfMatches = new VectorOfVectorOfDMatch();
            using var matcher = new BFMatcher(DistanceType.Hamming, settings.UseCrossCheck);

            if (descriptors1.IsEmpty || descriptors2.IsEmpty) return null;

            matcher.Add(descriptors1);
            matcher.KnnMatch(descriptors2, vectorOfMatches, settings.UseCrossCheck ? 1 : 2,
                settings.UseCrossCheck ? new VectorOfMat() : new VectorOfMat(distanceThresholdMask));

            var goodMatches = new List<MDMatch>();
            for (var i = 0; i < vectorOfMatches.Size; i++)
            {
                if (vectorOfMatches[i].Size == 0)
                {
                    continue;
                }

                if (vectorOfMatches[i].Size == 1 ||
                    (vectorOfMatches[i][0].Distance <
                     settings.RatioTest * vectorOfMatches[i][1].Distance)) //make sure matches are unique
                {
                    goodMatches.Add(vectorOfMatches[i][0]);
                }
            }

            if (goodMatches.Count < settings.MinimumKeypoints1) return null;

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

            if (settings.DrawKeypointMatches)
            {
                result.DirtyMatchesCount = pairedPoints.Count;
                result.DrawnDirtyMatches =
                    DrawMatches(firstImage, secondImage, pairedPoints, settings.DownsizePercentage);
            }

            if (settings.DiscardOutliersByDistance || settings.DiscardOutliersBySlope1)
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
                    var medianDistance = pairedPoints.OrderBy(p => p.Data.Distance).ElementAt(pairedPoints.Count / 2)
                        .Data.Distance;
                    var distanceStdDev = CalcStandardDeviation(pairedPoints.Select(p => p.Data.Distance).ToArray());
                    pairedPoints = pairedPoints.Where(p =>
                        Math.Abs(p.Data.Distance - medianDistance) <
                        Math.Abs(distanceStdDev * (settings.KeypointOutlierThresholdTenths / 10d))).ToList();
                    //Debug.WriteLine("Median Distance: " + medianDistance);
                    //Debug.WriteLine("Distance Cleaned Points count: " + pairedPoints.Count);
                }

                if (settings.DiscardOutliersBySlope1)
                {
                    var validSlopes = pairedPoints
                        .Where(p => !float.IsNaN(p.Data.Slope) && float.IsFinite(p.Data.Slope)).ToArray();
                    var medianSlope = validSlopes.OrderBy(p => p.Data.Slope).ElementAt(validSlopes.Length / 2).Data
                        .Slope;
                    var slopeStdDev = CalcStandardDeviation(validSlopes.Select(p => p.Data.Slope).ToArray());
                    pairedPoints = validSlopes.Where(p =>
                        Math.Abs(p.Data.Slope - medianSlope) <
                        Math.Abs(slopeStdDev * (settings.KeypointOutlierThresholdTenths / 10d))).ToList();
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
                    result.DrawnCleanMatches =
                        DrawMatches(firstImage, secondImage, pairedPoints, settings.DownsizePercentage);
                }
            }



            if (settings.TransformationFindingMethod == (uint)TransformationFindingMethod.BinarySearch)
            {
                var points1 = pairedPoints.Select(p => new SKPoint(p.KeyPoint1.Point.X, p.KeyPoint1.Point.Y)).ToArray();
                var points2 = pairedPoints.Select(p => new SKPoint(p.KeyPoint2.Point.X, p.KeyPoint2.Point.Y)).ToArray();


                var vert1 = FindVerticalTranslation(points1, points2, secondImage);
                var verted1 = SKMatrix.CreateTranslation(0, vert1);
                points2 = verted1.MapPoints(points2);

                var hor1 = FindHorizontalTranslation(points1, points2, secondImage);
                var hored1 = SKMatrix.CreateTranslation(hor1, 0);
                points2 = hored1.MapPoints(points2);

                var rotation1 = FindRotation(points1, points2, secondImage);
                var rotated1 = SKMatrix.CreateRotation(rotation1, secondImage.Width / 2f, secondImage.Height / 2f);
                points2 = rotated1.MapPoints(points2);

                var zoom1 = FindZoom(points1, points2, secondImage);
                var zoomed1 = SKMatrix.CreateScale(zoom1, zoom1, secondImage.Width / 2f, secondImage.Height / 2f);
                points2 = zoomed1.MapPoints(points2);

                var keystoned11 = SKMatrix.CreateIdentity();
                var keystoned21 = SKMatrix.CreateIdentity();
                if (settings.DoKeystoneCorrection1)
                {
                    FindTaperMatricesAndMapPoints(ref points1, ref points2, firstImage.Width, firstImage.Height,
                        out keystoned11, out keystoned21);
                }



                var vert2 = FindVerticalTranslation(points1, points2, secondImage);
                var verted2 = SKMatrix.CreateTranslation(0, vert2);
                points2 = verted2.MapPoints(points2);

                var hor2 = FindHorizontalTranslation(points1, points2, secondImage);
                var hored2 = SKMatrix.CreateTranslation(hor2, 0);
                points2 = hored2.MapPoints(points2);

                var rotation2 = FindRotation(points1, points2, secondImage);
                var rotated2 = SKMatrix.CreateRotation(rotation2, secondImage.Width / 2f, secondImage.Height / 2f);
                points2 = rotated2.MapPoints(points2);

                var zoom2 = FindZoom(points1, points2, secondImage);
                var zoomed2 = SKMatrix.CreateScale(zoom2, zoom2, secondImage.Width / 2f, secondImage.Height / 2f);
                points2 = zoomed2.MapPoints(points2);

                var keystoned12 = SKMatrix.CreateIdentity();
                var keystoned22 = SKMatrix.CreateIdentity();
                if (settings.DoKeystoneCorrection1)
                {
                    FindTaperMatricesAndMapPoints(ref points1, ref points2, firstImage.Width, firstImage.Height,
                        out keystoned12, out keystoned22);
                }


                var vert3 = FindVerticalTranslation(points1, points2, secondImage);
                var verted3 = SKMatrix.CreateTranslation(0, vert3);
                points2 = verted3.MapPoints(points2);

                var hor3 = FindHorizontalTranslation(points1, points2, secondImage);
                var hored3 = SKMatrix.CreateTranslation(hor3, 0);
                points2 = hored3.MapPoints(points2);

                var rotation3 = FindRotation(points1, points2, secondImage);
                var rotated3 = SKMatrix.CreateRotation(rotation3, secondImage.Width / 2f, secondImage.Height / 2f);
                points2 = rotated3.MapPoints(points2);

                var zoom3 = FindZoom(points1, points2, secondImage);
                var zoomed3 = SKMatrix.CreateScale(zoom3, zoom3, secondImage.Width / 2f, secondImage.Height / 2f);
                points2 = zoomed3.MapPoints(points2);

                var keystoned13 = SKMatrix.CreateIdentity();
                var keystoned23 = SKMatrix.CreateIdentity();
                if (settings.DoKeystoneCorrection1)
                {
                    FindTaperMatricesAndMapPoints(ref points1, ref points2, firstImage.Width, firstImage.Height,
                        out keystoned13, out keystoned23);
                }

                result.TransformMatrix1 = UpscaleSkMatrix(keystoned11.PostConcat(keystoned12).PostConcat(keystoned13), 1 / (settings.DownsizePercentage / 100f));

                result.TransformMatrix2 = UpscaleSkMatrix(
                                     verted1.PostConcat(hored1).PostConcat(rotated1).PostConcat(zoomed1).PostConcat(keystoned21)
                        .PostConcat(verted2).PostConcat(hored2).PostConcat(rotated2).PostConcat(zoomed2).PostConcat(keystoned22)
                        .PostConcat(verted3).PostConcat(hored3).PostConcat(rotated3).PostConcat(zoomed3).PostConcat(keystoned23), 1 / (settings.DownsizePercentage / 100f));
            }
            else
            {
                using var points1 = new VectorOfPointF(pairedPoints.Select(p => p.KeyPoint1.Point).ToArray());
                using var points2 = new VectorOfPointF(pairedPoints.Select(p => p.KeyPoint2.Point).ToArray());

                Mat warp1, warp2;
                if (settings.TransformationFindingMethod ==
                    (uint)TransformationFindingMethod.StereoRectifyUncalibrated)
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
                        TransformationFindingMethod.FindHomography => CvInvoke.FindHomography(points2, points1,
                            RobustEstimationAlgorithm.Ransac),
                        TransformationFindingMethod.EstimateRigidPartial => CvInvoke.EstimateAffinePartial2D(points2,
                            points1, new Mat(), RobustEstimationAlgorithm.Ransac, 3, 2000,0.99, 10),
                        TransformationFindingMethod.EstimateRigidFull => CvInvoke.EstimateAffine2D(points2,
                            points1)
                    };

                    if (warp2 == null || warp2.IsEmpty) return null;
                }

                if (settings.DrawResultWarpedByOpenCv)
                {
                    AddWarpedToResult(image1Mat, image2Mat, warp1, warp2, result);
                    result.MethodName = ((TransformationFindingMethod)settings.TransformationFindingMethod).ToString();
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

        private void FindTaperMatricesAndMapPoints(ref SKPoint[] points1, ref SKPoint[] points2, int width, int height,
            out SKMatrix keystone1, out SKMatrix keystone2)
        {
            var keystone = FindTaper(points2, points1, width, height);
            keystone1 = CreateTaper(width / 2f, height / 2f, keystone);
            keystone2 = CreateTaper(width / 2f, height / 2f, -keystone);
            points1 = keystone1.MapPoints(points1);
            points2 = keystone2.MapPoints(points2);
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

            using var cameraMatrix =
                GetCameraMatrix(image.Width * downsize * cxProportion, image.Height * downsize / 2f);

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

        private SKBitmap DrawMatches(SKBitmap image1, SKBitmap image2, List<PointForCleaning> points,
            uint downsizePercentage)
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
                drawnResult, new Bgr(Color.Green).MCvScalar, new Bgr(Color.Red).MCvScalar, null,
                flags: Features2DToolbox.KeypointDrawType.DrawRichKeypoints); //TODO: these colors don't work, it's always green.
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
                Debug.WriteLine(pair.KeyPoint1.Point.X + "," + pair.KeyPoint1.Point.Y + "," + pair.KeyPoint2.Point.X +
                                "," + pair.KeyPoint2.Point.Y);
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
            var translationInc = (float)(secondImage.Height * EditsSettings.DEFAULT_MAX_VERT_ALIGNMENT * 4d);

            return BinarySearchFindComponent(points1, points2, t => SKMatrix.CreateTranslation(0, t), translationInc,
                TRANSLATION_TERMINATION_THRESHOLD, useMedianDisplacement: true);
        }

        private static float FindHorizontalTranslation(SKPoint[] points1, SKPoint[] points2, SKBitmap secondImage)
        {
            const int TRANSLATION_TERMINATION_THRESHOLD = 1;
            var translationInc = secondImage.Width / 2f;

            return BinarySearchFindComponent(points1, points2, t => SKMatrix.CreateTranslation(t, 0), translationInc,
                TRANSLATION_TERMINATION_THRESHOLD, 0, true, true);
        }

        private static float FindRotation(SKPoint[] points1, SKPoint[] points2, SKBitmap secondImage)
        {
            const float FINAL_ROTATION_DELTA = 0.0001f;
            const float ROTATION_INC = EditsSettings.DEFAULT_MAX_ROTATION * 2f;

            return BinarySearchFindComponent(points1, points2,
                t => SKMatrix.CreateRotation(t, secondImage.Width / 2f, secondImage.Height / 2f), ROTATION_INC,
                FINAL_ROTATION_DELTA);
        }

        private static float FindZoom(SKPoint[] points1, SKPoint[] points2, SKBitmap secondImage)
        {
            const float FINAL_ZOOM_DELTA = 0.0001f;
            const float ZOOM_INC = (float)(EditsSettings.DEFAULT_MAX_ZOOM * 4f);

            return BinarySearchFindComponent(points1, points2,
                t => SKMatrix.CreateScale(t, t, secondImage.Width / 2f,
                    secondImage.Height / 2f), ZOOM_INC, FINAL_ZOOM_DELTA, 1);
        }

        private static float FindTaper(SKPoint[] points1, SKPoint[] points2, int width, int height)
        {
            const float FINAL_TAPER_DELTA = 0.01f;
            const float TAPER_INC = EditsSettings.DEFAULT_MAX_KEYSTONE * 3f;

            return BinarySearchFindComponentMirrored(points1, points2,
                f => CreateTaper(width, height / 2f, f), TAPER_INC,
                FINAL_TAPER_DELTA);
        }

        private static SKMatrix CreateTaper(float width, float centerY, float rotation)
        {
            return DrawTool.MakeKeystoneTransform(rotation, 0, width, centerY).Matrix;
        }

        private static float BinarySearchFindComponent(SKPoint[] basePoints, SKPoint[] pointsToTransform, Func<float, SKMatrix> testerFunction, float searchingIncrement, float terminationThreshold, float componentStart = 0, bool xDisplacement = false, bool useMedianDisplacement = false)
        {
            var baseOffset = useMedianDisplacement
                ? GetMedianOffset(basePoints, pointsToTransform, xDisplacement)
                : GetNetWhiskerOffset(basePoints, pointsToTransform, xDisplacement);

            while (searchingIncrement > terminationThreshold)
            {
                //Debug.WriteLine("baseOffset: " + baseOffset + " inc: " + searchingIncrement + " func: " +
                //                testerFunction.Method.Name);
                var versionA = testerFunction(componentStart + searchingIncrement);
                var versionB = testerFunction(componentStart - searchingIncrement);
                var attemptA = versionA.MapPoints(pointsToTransform);
                var attemptB = versionB.MapPoints(pointsToTransform);
                double offsetA, offsetB;
                if (useMedianDisplacement)
                {
                    offsetA = GetMedianOffset(basePoints, attemptA, xDisplacement);
                    offsetB = GetMedianOffset(basePoints, attemptB, xDisplacement);
                }
                else
                {
                    offsetA = GetNetWhiskerOffset(basePoints, attemptA, xDisplacement);
                    offsetB = GetNetWhiskerOffset(basePoints, attemptB, xDisplacement);
                }

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

        private static float BinarySearchFindComponentMirrored(SKPoint[] basePoints1, SKPoint[] basePoints2, Func<float, SKMatrix> testerFunction, float searchingIncrement, float terminationThreshold, float componentStart = 0, bool xDisplacement = false, bool useMedianDisplacement = false)
        {
            var baseOffset = useMedianDisplacement
                ? GetMedianOffset(basePoints1, basePoints2, xDisplacement)
                : GetNetWhiskerOffset(basePoints1, basePoints2, xDisplacement);

            while (searchingIncrement > terminationThreshold)
            {
                //Debug.WriteLine("baseOffset: " + baseOffset + " inc: " + searchingIncrement + " func: " +
                //                testerFunction.Method.Name);
                var up = testerFunction(componentStart + searchingIncrement);
                var down = testerFunction(componentStart - searchingIncrement);

                var up1 = up.MapPoints(basePoints1);
                var down2 = down.MapPoints(basePoints2);

                var down1 = down.MapPoints(basePoints1);
                var up2 = up.MapPoints(basePoints2);

                double offsetA, offsetB;
                if (useMedianDisplacement)
                {
                    offsetA = GetMedianOffset(down1, up2, xDisplacement);
                    offsetB = GetMedianOffset(up1, down2, xDisplacement);
                }
                else
                {
                    offsetA = GetNetWhiskerOffset(down1, up2, xDisplacement);
                    offsetB = GetNetWhiskerOffset(up1, down2, xDisplacement);
                }

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

        private static double GetMedianOffset(SKPoint[] points1, SKPoint[] points2, bool xDisplacement)
        {
            var offsets = points1.Select((t, ii) => Math.Abs(xDisplacement ? t.X - points2[ii].X : t.Y - points2[ii].Y))
                .OrderBy(o => o).ToList();
            //Debug.WriteLine("### median x: " + offsets.ElementAt(offsets.Count / 2));
            return offsets.ElementAt(offsets.Count / 2);
        }

        private static double GetNetWhiskerOffset(SKPoint[] points1, SKPoint[] points2, bool xDisplacement)
        {
            var rawOffsets = points1.Select((t, ii) => xDisplacement ? t.X - points2[ii].X : t.Y - points2[ii].Y)
                .OrderBy(o => o).ToList();
            var firstQuartile = rawOffsets.ElementAt(rawOffsets.Count / 4);
            var median = rawOffsets.ElementAt(rawOffsets.Count / 2);
            var thirdQuartile = rawOffsets.ElementAt(3 * rawOffsets.Count / 4);
            var iqr = Math.Abs(thirdQuartile - firstQuartile);
            var outlierRange = iqr * 1.5;
            var inliers = rawOffsets.Where(offset => offset >= median - outlierRange && offset <= median + outlierRange)
                .ToList();
            //var outliers = rawOffsets.Where(offset => offset < median - outlierRange || offset > median + outlierRange)
            //    .ToList();
            var sum = inliers.Select(Math.Abs).Sum();
            //Debug.WriteLine("### q1: " + firstQuartile +
            //                " med: " + median +
            //                " q3: " + thirdQuartile +
            //                " iqr: " + iqr +
            //                " sum: " + sum);
            //Debug.WriteLine(inliers.Count + " inliers: " + string.Join(",", inliers.Select(d => Math.Round(d, 2))));
            //Debug.WriteLine(outliers.Count + " outliers: " + string.Join(",", outliers.Select(d => Math.Round(d, 2))));
            //Debug.WriteLine("all: " + string.Join(",", rawOffsets.Select(d => Math.Round(d, 2))));
            return sum;
        }

        private static double GetNetOffset(SKPoint[] points1, SKPoint[] points2, bool x)
        {
            var netOffset = 0d;
            for (var ii = 0; ii < points1.Length; ii++)
            {
                netOffset += Math.Abs(x ? points1[ii].X - points2[ii].X : points1[ii].Y - points2[ii].Y);
            }

            //Debug.WriteLine("### net: " + netOffset);
            return netOffset;
        }

        private static SKMatrix UpscaleSkMatrix(SKMatrix skMatrix, float upscaleFactor)
        {
            var scaleMatrix = new SKMatrix(
                upscaleFactor, 0, 0,
                0, upscaleFactor, 0,
                0, 0, 1);
            skMatrix = scaleMatrix.PreConcat(skMatrix).PreConcat(scaleMatrix.Invert());

            return skMatrix;
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
                    var ptr = (float*)mat.DataPointer.ToPointer();
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
                    var ptr = (double*)mat.DataPointer.ToPointer();
                    skMatrix.ScaleX = (float)*ptr;
                    ptr++;
                    skMatrix.SkewX = (float)*ptr;
                    ptr++;
                    skMatrix.TransX = (float)*ptr;
                    ptr++;
                    skMatrix.SkewY = (float)*ptr;
                    ptr++;
                    skMatrix.ScaleY = (float)*ptr;
                    ptr++;
                    skMatrix.TransY = (float)*ptr;
                    if (mat.Rows == 3)
                    {
                        ptr++;
                        skMatrix.Persp0 = (float)*ptr;
                        ptr++;
                        skMatrix.Persp1 = (float)*ptr;
                        ptr++;
                        skMatrix.Persp2 = (float)*ptr;
                    }
                }
            }

            if (upscaleFactor != 1)
            {
                skMatrix = UpscaleSkMatrix(skMatrix, upscaleFactor);
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