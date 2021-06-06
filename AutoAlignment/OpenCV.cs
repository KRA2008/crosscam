using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AutoAlignment;
using CrossCam.Model;
using CrossCam.Page;
using CrossCam.Wrappers;
#if !__NO_EMGU__
using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV.Util;
#endif
using SkiaSharp;
using Xamarin.Forms;
using Point = System.Drawing.Point;
#if __ANDROID__
using SkiaSharp.Views.Android;
#elif __IOS__
using SkiaSharp.Views.iOS;
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
#endif
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
        }

        public AlignedResult CreateAlignedSecondImageEcc(SKBitmap firstImage, SKBitmap secondImage, bool discardTransX, AlignmentSettings settings)
        {
#if __NO_EMGU__
            return null;
#endif
            var topDownsizeFactor = settings.EccDownsizePercentage / 100f;

            var eccs = new List<double>();
            using var mat1 = new Mat();
            using var mat2 = new Mat();
            using var warpMatrix = Mat.Eye(2, 3, DepthType.Cv32F, 1);
            var termCriteria = new MCvTermCriteria(settings.EccIterations, Math.Pow(10, -settings.EccEpsilonLevel));
            for (var ii = settings.EccPyramidLayers - 1; ii >= 0; ii--)
            {
                var downsize = topDownsizeFactor / Math.Pow(2, ii);
                CvInvoke.Imdecode(GetBytes(firstImage, downsize), ImreadModes.Grayscale, mat1);
                CvInvoke.Imdecode(GetBytes(secondImage, downsize), ImreadModes.Grayscale, mat2);

                try
                {
                    var ecc = CvInvoke.FindTransformECC(mat2, mat1, warpMatrix, MotionType.Euclidean, termCriteria);
                    eccs.Add(ecc);
                }
                catch (CvException e)
                {
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

            if (eccs.Last() * 100 < settings.EccThresholdPercentage)
            {
                return null;
            }

            var skMatrix = ConvertCvMatOfFloatsToSkMatrix(warpMatrix, discardTransX);

            var result = new AlignedResult
            {
                TransformMatrix2 = skMatrix
            };

            using var alignedMat = new Mat();
            using var fullSizeColorSecondMat = new Mat();
            CvInvoke.Imdecode(GetBytes(secondImage, 1), ImreadModes.Color, fullSizeColorSecondMat);
            CvInvoke.WarpAffine(fullSizeColorSecondMat, alignedMat, warpMatrix,
                fullSizeColorSecondMat.Size);

#if __IOS__
            result.AlignedBitmap2 = alignedMat.ToCGImage().ToSKBitmap();
#elif __ANDROID__
            result.AlignedBitmap2 = alignedMat.ToBitmap().ToSKBitmap();
#endif
            return result;
        }

        public AlignedResult CreateAlignedSecondImageKeypoints(SKBitmap firstImage, SKBitmap secondImage,
            bool discardTransX, AlignmentSettings settings, bool keystoneRightOnFirst)
        {
#if __NO_EMGU__
            return null;
#endif
            var result = new AlignedResult();

            var detector = new ORBDetector();
            const ImreadModes READ_MODE = ImreadModes.Color;

            var mat1 = new Mat();
            var descriptors1 = new Mat();
            var allKeyPointsVector1 = new VectorOfKeyPoint();
            CvInvoke.Imdecode(GetBytes(firstImage, 1), READ_MODE, mat1);
            detector.DetectAndCompute(mat1, null, allKeyPointsVector1, descriptors1, false);

            var mat2 = new Mat();
            var descriptors2 = new Mat();
            var allKeyPointsVector2 = new VectorOfKeyPoint();
            CvInvoke.Imdecode(GetBytes(secondImage, 1), READ_MODE, mat2);
            detector.DetectAndCompute(mat2, null, allKeyPointsVector2, descriptors2, false);

            const double THRESHOLD_PROPORTION = 1 / 4d;
            var thresholdDistance = Math.Sqrt(Math.Pow(firstImage.Width, 2) + Math.Pow(firstImage.Height, 2)) * THRESHOLD_PROPORTION;

            var distanceThresholdMask = new Mat(allKeyPointsVector2.Size, allKeyPointsVector1.Size, DepthType.Cv8U, 1);
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

            var vectorOfMatches = new VectorOfVectorOfDMatch();
            var matcher = new BFMatcher(DistanceType.Hamming, settings.UseCrossCheck);
            matcher.Add(descriptors1);
            matcher.KnnMatch(descriptors2, vectorOfMatches, settings.UseCrossCheck ? 1 : 2, settings.UseCrossCheck ? new VectorOfMat() : new VectorOfMat(distanceThresholdMask));

            var goodMatches = new List<MDMatch>();
            for (var i = 0; i < vectorOfMatches.Size; i++)
            {
                if (vectorOfMatches[i].Size == 0)
                {
                    continue;
                }

                if(vectorOfMatches[i].Size == 1 || 
                   (vectorOfMatches[i][0].Distance < 0.75 * vectorOfMatches[i][1].Distance)) //make sure matches are unique
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

            if (settings.DrawKeypointMatches)
            {
                result.DirtyMatchesCount = pairedPoints.Count;
                result.DrawnDirtyMatches = DrawMatches(firstImage, secondImage, pairedPoints);
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
                    result.DrawnCleanMatches = DrawMatches(firstImage, secondImage, pairedPoints);
                }
            }

            var points1 = pairedPoints.Select(p => new SKPoint(p.KeyPoint1.Point.X, p.KeyPoint1.Point.Y)).ToArray();
            var points2 = pairedPoints.Select(p => new SKPoint(p.KeyPoint2.Point.X, p.KeyPoint2.Point.Y)).ToArray();


            var translation1 = FindVerticalTranslation(points1, points2, secondImage);
            var translated1 = SKMatrix.MakeTranslation(0, translation1);
            points2 = translated1.MapPoints(points2);

            var rotation1 = FindRotation(points1, points2, secondImage);
            var rotated1 = SKMatrix.MakeRotation(rotation1, secondImage.Width / 2f, secondImage.Height / 2f);
            points2 = rotated1.MapPoints(points2);

            var zoom1 = FindZoom(points1, points2, secondImage);
            var zoomed1 = SKMatrix.MakeScale(zoom1, zoom1, secondImage.Width / 2f, secondImage.Height / 2f);
            points2 = zoomed1.MapPoints(points2);



            var translation2 = FindVerticalTranslation(points1, points2, secondImage);
            var translated2 = SKMatrix.MakeTranslation(0, translation2);
            points2 = translated2.MapPoints(points2);

            var rotation2 = FindRotation(points1, points2, secondImage);
            var rotated2 = SKMatrix.MakeRotation(rotation2, secondImage.Width / 2f, secondImage.Height / 2f);
            points2 = rotated2.MapPoints(points2);

            var zoom2 = FindZoom(points1, points2, secondImage);
            var zoomed2 = SKMatrix.MakeScale(zoom2, zoom2, secondImage.Width / 2f, secondImage.Height / 2f);
            points2 = zoomed2.MapPoints(points2);

            

            var translation3 = FindVerticalTranslation(points1, points2, secondImage);
            var translated3 = SKMatrix.MakeTranslation(0, translation3);
            points2 = translated3.MapPoints(points2);

            var rotation3 = FindRotation(points1, points2, secondImage);
            var rotated3 = SKMatrix.MakeRotation(rotation3, secondImage.Width / 2f, secondImage.Height / 2f);
            points2 = rotated3.MapPoints(points2);

            var zoom3 = FindZoom(points1, points2, secondImage);
            var zoomed3 = SKMatrix.MakeScale(zoom3, zoom3, secondImage.Width / 2f, secondImage.Height / 2f);
            points2 = zoomed3.MapPoints(points2);


            var keystoned1 = SKMatrix.MakeIdentity();
            var keystoned2 = SKMatrix.MakeIdentity();
            if (settings.DoKeystoneCorrection)
            {
                keystoned1 = FindTaper(points2, points1, secondImage, keystoneRightOnFirst);
                points1 = keystoned1.MapPoints(points1);
                keystoned2 = FindTaper(points1, points2, secondImage, !keystoneRightOnFirst);
                points2 = keystoned2.MapPoints(points2);
            }


            var horizontaled = SKMatrix.MakeIdentity();
            if (!discardTransX)
            {
                var horizontalAdj = FindHorizontalTranslation(points1, points2, secondImage);
                horizontaled = SKMatrix.MakeTranslation(horizontalAdj, 0);
                points2 = horizontaled.MapPoints(points2);
            }



            var tempMatrix1 = new SKMatrix();
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
            SKMatrix.Concat(ref tempMatrix9, tempMatrix8, keystoned2);

            var tempMatrix10 = new SKMatrix();
            SKMatrix.Concat(ref tempMatrix10, tempMatrix9, horizontaled);

            var finalMatrix = tempMatrix10;
            result.TransformMatrix2 = finalMatrix;
            var alignedImage2 = new SKBitmap(secondImage.Width, secondImage.Height);
            using (var canvas = new SKCanvas(alignedImage2))
            {
                canvas.SetMatrix(finalMatrix);
                canvas.DrawBitmap(secondImage, 0, 0);
            }
            result.AlignedBitmap2 = alignedImage2;


            result.TransformMatrix1 = keystoned1;
            var alignedImage1 = new SKBitmap(firstImage.Width, firstImage.Height);
            using (var canvas = new SKCanvas(alignedImage1))
            {
                canvas.SetMatrix(keystoned1);
                canvas.DrawBitmap(firstImage, 0, 0);
            }
            result.AlignedBitmap1 = alignedImage1;


            return result;
        }

        private static SKBitmap DrawMatches(SKBitmap image1, SKBitmap image2, List<PointForCleaning> points)
        {
            using var fullSizeColor1 = new Mat();
            using var fullSizeColor2 = new Mat();
            CvInvoke.Imdecode(GetBytes(image1, 1), ImreadModes.Color, fullSizeColor1);
            CvInvoke.Imdecode(GetBytes(image2, 1), ImreadModes.Color, fullSizeColor2);

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

            return BinarySearchFindComponent(points1, points2, t => SKMatrix.MakeTranslation(0, t), translationInc, TRANSLATION_TERMINATION_THRESHOLD);
        }

        private static float FindHorizontalTranslation(SKPoint[] points1, SKPoint[] points2, SKBitmap secondImage)
        {
            const int TRANSLATION_TERMINATION_THRESHOLD = 1;
            var translationInc = secondImage.Width / 2;

            return BinarySearchFindComponent(points1, points2, t => SKMatrix.MakeTranslation(t, 0), translationInc, TRANSLATION_TERMINATION_THRESHOLD, 0, true);
        }

        private static float FindRotation(SKPoint[] points1, SKPoint[] points2, SKBitmap secondImage)
        {
            const float FINAL_ROTATION_DELTA = 0.0001f;
            const float ROTATION_INC = (float)Math.PI / 2f;

            return BinarySearchFindComponent(points1, points2,
                t => SKMatrix.MakeRotation(t, secondImage.Width / 2f, secondImage.Height / 2f), ROTATION_INC,
                FINAL_ROTATION_DELTA);
        }

        private static float FindZoom(SKPoint[] points1, SKPoint[] points2, SKBitmap secondImage)
        {
            const float FINAL_ZOOM_DELTA = 0.0001f;
            const float ZOOM_INC = 1f;

            return BinarySearchFindComponent(points1, points2,
                t => SKMatrix.MakeScale(t, t, secondImage.Width / 2f,
                    secondImage.Height / 2f), ZOOM_INC, FINAL_ZOOM_DELTA, 1);
        }

        private static SKMatrix FindTaper(SKPoint[] pointsToMatch, SKPoint[] pointsToCorrect, SKBitmap image, bool keystoneRight)
        {
            const float FINAL_TAPER_DELTA = 0.0001f;
            const float TAPER_INC = 0.5f;
            var taperSide = keystoneRight ? TaperSide.Right : TaperSide.Left;

            var taper = BinarySearchFindComponent(pointsToMatch, pointsToCorrect,
                t => TaperTransform.Make(new SKSize(image.Width, image.Height), taperSide, TaperCorner.Both, t),
                TAPER_INC, FINAL_TAPER_DELTA, 1);
            return TaperTransform.Make(new SKSize(image.Width, image.Height), taperSide, TaperCorner.Both, taper);
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

        private static SKMatrix ConvertCvMatOfFloatsToSkMatrix(Mat mat, bool discardTransX)
        {
            var skMatrix = SKMatrix.MakeIdentity();
            unsafe
            {
                var ptr = (float*)mat.DataPointer.ToPointer(); //ScaleX
                skMatrix.ScaleX = *ptr;
                ptr++; //SkewX
                skMatrix.SkewX = *ptr;
                ptr++; //TransX
                if (discardTransX)
                {
                    *ptr = 0;
                }
                skMatrix.TransX = *ptr;
                ptr++; //SkewY
                skMatrix.SkewY = *ptr;
                ptr++; //ScaleY
                skMatrix.ScaleY = *ptr;
                ptr++; //TransY
                skMatrix.TransY = *ptr;
            }

            return skMatrix;
        }

        private static SKMatrix ConvertCvMatOfDoublesToSkMatrix(Mat mat, bool discardTransX)
        {
            var skMatrix = SKMatrix.MakeIdentity();
            unsafe
            {
                var ptr = (double*)mat.DataPointer.ToPointer(); //ScaleX
                skMatrix.ScaleX = (float)*ptr;
                ptr++; //SkewX
                skMatrix.SkewX = (float)*ptr;
                ptr++; //TransX
                if (discardTransX)
                {
                    *ptr = 0;
                }
                skMatrix.TransX = (float)*ptr;
                ptr++; //SkewY
                skMatrix.SkewY = (float)*ptr;
                ptr++; //ScaleY
                skMatrix.ScaleY = (float)*ptr;
                ptr++; //TransY
                skMatrix.TransY = (float)*ptr;
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

        private static byte[] GetBytes(SKBitmap bitmap, double downsize)
        {
            var width = (int)(bitmap.Width * downsize);
            var height = (int)(bitmap.Height * downsize);
            using var tempSurface =
                SKSurface.Create(new SKImageInfo(width, height));
            var canvas = tempSurface.Canvas;
            canvas.Clear();

            canvas.DrawBitmap(bitmap,
                SKRect.Create(0, 0, bitmap.Width, bitmap.Height),
                SKRect.Create(0, 0, width, height));

            using var data = tempSurface.Snapshot().Encode(SKEncodedImageFormat.Jpeg, 100);
            return data.ToArray();
        }
    }
}