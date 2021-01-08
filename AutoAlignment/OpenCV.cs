using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Linq;
using AutoAlignment;
using CrossCam.Model;
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

        //TODO: the problem with this one is that it is unable to scale, this is important for using two devices with different fields of view
        public AlignedResult CreateAlignedSecondImageEcc(SKBitmap firstImage, SKBitmap secondImage, int downsizePercentage, int iterations,
            int epsilonLevel, int eccCutoff, int pyramidLayers, bool discardTransX)
        {
#if __NO_EMGU__
            return null;
#endif
            var topDownsizeFactor = downsizePercentage / 100f;

            var eccs = new List<double>();
            using var mat1 = new Mat();
            using var mat2 = new Mat();
            using var warpMatrix = Mat.Eye(2, 3, DepthType.Cv32F, 1);
            var termCriteria = new MCvTermCriteria(iterations, Math.Pow(10, -epsilonLevel));
            for (var ii = pyramidLayers - 1; ii >= 0; ii--)
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

            if (eccs.Last() * 100 < eccCutoff)
            {
                return null;
            }

            var skMatrix = ConvertCvMatOfFloatsToSkMatrix(warpMatrix, discardTransX);

            var result = new AlignedResult
            {
                TransformMatrix = skMatrix
            };

            using var alignedMat = new Mat();
            using var fullSizeColorSecondMat = new Mat();
            CvInvoke.Imdecode(GetBytes(secondImage, 1), ImreadModes.Color, fullSizeColorSecondMat);
            CvInvoke.WarpAffine(fullSizeColorSecondMat, alignedMat, warpMatrix,
                fullSizeColorSecondMat.Size);

#if __IOS__
            result.AlignedBitmap = alignedMat.ToCGImage().ToSKBitmap();
#elif __ANDROID__
            result.AlignedBitmap = alignedMat.ToBitmap().ToSKBitmap();
#endif
            return result;
        }

        //TODO: when this one has good keypoints (its own challenge):
        //TODO: - rigid transform seems to make perfectly built stereograms *worse* (it's trying to minimize keypoint changes, which isn't really what we want
        //TODO: - allowing full affine makes the horizontal alignment better, but it causes skewing which can make the picture totally trippy and unrealistic
        //TODO: - using stereoRectifyUncalibrated gives me weird results that mess up perfect pictures and absolutely destroys imperfect ones
        //TODO:       ^is stereo rectification even what I want? or is that more like allowing two camera/computers to see "perpendicularly" without being perpendicular? (coincidence that rectified stereograms are sometimes good viewing?)
        //TODO: - look into alignment as one would use for a panorama?
        //TODO: ???? roll my own optimization based on horizontal lines and keypoints?
        public AlignedResult CreateAlignedSecondImageKeypoints(SKBitmap firstImage, SKBitmap secondImage,
            bool discardTransX, bool fullAffine)
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

            // OPTION 1A

            ////filtering keypoint matching by physical distance between keypoints as proportion of image size
            const double THRESHOLD_PROPORTION = 1 / 4d;
            var thresholdDistance = Math.Sqrt(Math.Pow(firstImage.Width, 2) + Math.Pow(firstImage.Height, 2)) * THRESHOLD_PROPORTION; //TODO: idk, something else?

            //OPTION 1B

            ////filtering keypoint matching by physical distance as proportion of eccWarpMatrix translation distance
            const double THRESHOLD = 2;
            //var thresholdDistance = Math.Sqrt(Math.Pow(skMatrix.TransX, 2) + Math.Pow(skMatrix.TransY, 2)) * THRESHOLD; //TODO: account for rotation?

            var mask = new Mat(allKeyPointsVector2.Size, allKeyPointsVector1.Size, DepthType.Cv8U, 1);
            unsafe
            {
                var maskPtr = (byte*)mask.DataPointer.ToPointer();
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


            // OPTION 2

            ////filtering keypoint matching by physical distance from expected keypoint location when transformed by eccWarpMatrix
            //var transformedKeypoints2 = new VectorOfPointF();
            //CvInvoke.Transform(new VectorOfPointF(allKeyPointsVector2.ToArray().Select(v => v.Point).ToArray()), transformedKeypoints2, eccWarpMatrix);
            //const double THRESHOLD_DISTANCE_PROPORTION = 1/20d;
            //var thresholdDistance = Math.Sqrt(Math.Pow(firstImage.Width, 2) + Math.Pow(firstImage.Height, 2)) * THRESHOLD_DISTANCE_PROPORTION;
            //var mask = new Mat(allKeyPointsVector2.Size, allKeyPointsVector1.Size, DepthType.Cv8U, 1);
            //unsafe
            //{
            //    var maskPtr = (byte*)mask.DataPointer.ToPointer();
            //    for (var i = 0; i < transformedKeypoints2.Size; i++)
            //    {
            //        var transformedKeyPoint2 = transformedKeypoints2[i];
            //        for (var j = 0; j < allKeyPointsVector1.Size; j++)
            //        {
            //            var keyPoint1 = allKeyPointsVector1[j];
            //            var physicalDistance = CalculatePhysicalDistanceBetweenPoints(transformedKeyPoint2, keyPoint1.Point);
            //            if (physicalDistance < thresholdDistance)
            //            {
            //                *maskPtr = 255;
            //            }
            //            else
            //            {
            //                *maskPtr = 0;
            //            }

            //            maskPtr++;
            //        }
            //    }
            //}




            var vectorOfMatches = new VectorOfVectorOfDMatch();
            var matcher = new BFMatcher(DistanceType.Hamming, true); //TODO: messing with crosscheck, it's turned on... good? if using, don't need unique match check below.
            matcher.Add(descriptors1);
            matcher.KnnMatch(descriptors2, vectorOfMatches, 1, new VectorOfMat()); //TODO: am not using the mask... is that good?

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

            var tempGoodPoints1List = new List<PointF>();
            var tempGoodKeyPoints1 = new List<MKeyPoint>();
            var tempGoodPoints2List = new List<PointF>();
            var tempGoodKeyPoints2 = new List<MKeyPoint>();

            var tempAllKeyPoints1List = allKeyPointsVector1.ToArray().ToList();
            var tempAllKeyPoints2List = allKeyPointsVector2.ToArray().ToList();
            var goodMatchesVectorList = new List<MDMatch[]>();
            for (var ii = 0; ii < goodMatches.Count; ii++)
            {
                var queryIndex = goodMatches[ii].QueryIdx;
                var trainIndex = goodMatches[ii].TrainIdx;
                tempGoodPoints1List.Add(tempAllKeyPoints1List.ElementAt(trainIndex).Point);
                tempGoodKeyPoints1.Add(tempAllKeyPoints1List.ElementAt(trainIndex));
                tempGoodPoints2List.Add(tempAllKeyPoints2List.ElementAt(queryIndex).Point);
                tempGoodKeyPoints2.Add(tempAllKeyPoints2List.ElementAt(queryIndex));
                goodMatchesVectorList.Add(new[]{new MDMatch
                {
                    Distance = goodMatches[ii].Distance,
                    ImgIdx = goodMatches[ii].ImgIdx,
                    QueryIdx = ii,
                    TrainIdx = ii
                }});
            }

            var goodMatchesVector = new VectorOfVectorOfDMatch(goodMatchesVectorList.ToArray());
            var goodKeyPointsVector1 = new VectorOfKeyPoint(tempGoodKeyPoints1.ToArray());
            var goodKeyPointsVector2 = new VectorOfKeyPoint(tempGoodKeyPoints2.ToArray());

            var pointsFrom1 = tempGoodKeyPoints1.Select(p => p.Point).ToList();
            var pointsFrom2 = tempGoodKeyPoints2.Select(p => p.Point).ToList();

            if(pointsFrom1.Count != pointsFrom2.Count) Debug.WriteLine("crap.");

            var pairedPoints = new List<PointForCleaning>();
            for (var ii = 0; ii < pointsFrom1.Count; ii++)
            {
                var point1 = pointsFrom1[ii];
                var point2 = pointsFrom2[ii];
                pairedPoints.Add(new PointForCleaning
                {
                    Point1 = point1,
                    Point2 = point2,
                    Data = new KeyPointOutlierDetectorData
                    {
                        Distance = (float)CalculatePhysicalDistanceBetweenPoints(point1, point2),
                        Slope = (point2.Y - point1.Y) / (point2.X - point1.X)
                    }
                });
            }

            Debug.WriteLine("Dirty Distances: " + string.Join(",", pairedPoints.Select(d => d.Data.Distance)));
            Debug.WriteLine("Dirty Slopes: " + string.Join(",", pairedPoints.Select(d => d.Data.Slope)));
            Debug.WriteLine("Dirty X: " + string.Join(",", pairedPoints.Select(d => Math.Abs(d.Point1.X - d.Point2.X))));
            Debug.WriteLine("Dirty Y: " + string.Join(",", pairedPoints.Select(d => Math.Abs(d.Point1.Y - d.Point2.Y))));

            var rejectCountOnEnds = pairedPoints.Count * 0.05;
            pairedPoints = pairedPoints.OrderBy(p => p.Data.Slope).ToList();
            pairedPoints.RemoveRange(0, (int)rejectCountOnEnds);
            pairedPoints.RemoveRange((int)(pairedPoints.Count - rejectCountOnEnds), (int)rejectCountOnEnds);

            Debug.WriteLine("Clean Distances: " + string.Join(",", pairedPoints.Select(d => d.Data.Distance)));
            Debug.WriteLine("Clean Slopes: " + string.Join(",", pairedPoints.Select(d => d.Data.Slope)));
            Debug.WriteLine("Clean X: " + string.Join(",", pairedPoints.Select(d => Math.Abs(d.Point1.X - d.Point2.X))));
            Debug.WriteLine("Clean Y: " + string.Join(",", pairedPoints.Select(d => Math.Abs(d.Point1.Y - d.Point2.Y))));

            var points1 = pairedPoints.Select(p => new SKPoint(p.Point1.X, p.Point1.Y)).ToArray();
            var points2 = pairedPoints.Select(p => new SKPoint(p.Point2.X, p.Point2.Y)).ToArray();



            var translation1 = FindTranslation(points1, points2, secondImage);
            var translated1 = SKMatrix.MakeTranslation(0, translation1);
            points2 = translated1.MapPoints(points2);

            var rotation1 = FindRotation(points1, points2, secondImage);
            var rotated1 = SKMatrix.MakeRotation(rotation1, secondImage.Width / 2f, secondImage.Height / 2f);
            points2 = rotated1.MapPoints(points2);

            var zoom1 = FindZoom(points1, points2);
            var zoomed1 = SKMatrix.MakeScale(zoom1, zoom1);
            points2 = zoomed1.MapPoints(points2);



            var translation2 = FindTranslation(points1, points2, secondImage);
            var translated2 = SKMatrix.MakeTranslation(0, translation2);
            points2 = translated2.MapPoints(points2);

            var rotation2 = FindRotation(points1, points2, secondImage);
            var rotated2 = SKMatrix.MakeRotation(rotation2, secondImage.Width / 2f, secondImage.Height / 2f);
            points2 = rotated2.MapPoints(points2);

            var zoom2 = FindZoom(points1, points2);
            var zoomed2 = SKMatrix.MakeScale(zoom2, zoom2);
            points2 = zoomed2.MapPoints(points2);

            

            var translation3 = FindTranslation(points1, points2, secondImage);
            var translated3 = SKMatrix.MakeTranslation(0, translation3);
            points2 = translated3.MapPoints(points2);

            var rotation3 = FindRotation(points1, points2, secondImage);
            var rotated3 = SKMatrix.MakeRotation(rotation3, secondImage.Width / 2f, secondImage.Height / 2f);
            points2 = rotated3.MapPoints(points2);

            var zoom3 = FindZoom(points1, points2); //TODO: how am i handling different resolutions on the way in?
            var zoomed3 = SKMatrix.MakeScale(zoom3, zoom3);
            points2 = zoomed3.MapPoints(points2);



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



            result.TransformMatrix = tempMatrix8; //TODO: this doesn't translate horizontally right now - add that as an option
            var alignedImage = new SKBitmap(secondImage.Width, secondImage.Height);
            using (var canvas = new SKCanvas(alignedImage))
            {
                canvas.SetMatrix(tempMatrix8);
                canvas.DrawBitmap(secondImage, 0, 0);
            }
            result.AlignedBitmap = alignedImage;




            using var fullSizeColor1 = new Mat();
            using var fullSizeColor2 = new Mat();
            CvInvoke.Imdecode(GetBytes(firstImage, 1), ImreadModes.Color, fullSizeColor1);
            CvInvoke.Imdecode(GetBytes(secondImage, 1), ImreadModes.Color, fullSizeColor2);

            using var drawnResult = new Mat();
            Features2DToolbox.DrawMatches(fullSizeColor1, goodKeyPointsVector1, fullSizeColor2, goodKeyPointsVector2, goodMatchesVector, drawnResult, new MCvScalar(0, 255, 0), new MCvScalar(255, 255, 0), new Mat());
#if __IOS__
            result.DrawnMatches = drawnResult.ToCGImage().ToSKBitmap();
#elif __ANDROID__
            result.DrawnMatches = drawnResult.ToBitmap().ToSKBitmap();
#endif
            return result;
        }

        private class KeyPointOutlierDetectorData
        {
            public float Distance { get; set; }
            public float Slope { get; set; }
        }

        private class PointForCleaning
        {
            public PointF Point1 { get; set; }
            public PointF Point2 { get; set; }
            public KeyPointOutlierDetectorData Data { get; set; }
        }

        private static float FindTranslation(SKPoint[] good1, SKPoint[] good2, SKBitmap secondImage)
        {
            var baseOffset = GetNetYOffset(good1, good2);
            const int FINAL_TRANSLATION_DELTA = 1;
            var translationInc = secondImage.Height / 2;
            var finalTranslation = 0f;

            while (translationInc > FINAL_TRANSLATION_DELTA)
            {
                var translateA = SKMatrix.MakeTranslation(0, finalTranslation + translationInc);
                var translateB = SKMatrix.MakeTranslation(0, finalTranslation - translationInc);
                var attemptA = translateA.MapPoints(good2);
                var offsetA = GetNetYOffset(good1, attemptA);
                var attemptB = translateB.MapPoints(good2);
                var offsetB = GetNetYOffset(good1, attemptB);

                if (offsetA < baseOffset)
                {
                    baseOffset = offsetA;
                    finalTranslation += translationInc;
                }
                else if (offsetB < baseOffset)
                {
                    baseOffset = offsetB;
                    finalTranslation -= translationInc;
                }
                translationInc /= 2;
            }

            return finalTranslation;
        }

        private static float FindRotation(SKPoint[] good1, SKPoint[] good2, SKBitmap secondImage)
        {
            var baseOffset = GetNetYOffset(good1, good2);
            const float FINAL_ROTATION_DELTA = 0.0001f; //TODO: make configurable?
            var rotationInc = (float)Math.PI / 2f;
            var finalRotation = 0f;

            while (rotationInc > FINAL_ROTATION_DELTA)
            {
                var rotateA = SKMatrix.MakeRotation(finalRotation + rotationInc, secondImage.Width / 2f, secondImage.Height / 2f);
                var rotateB = SKMatrix.MakeRotation(finalRotation - rotationInc, secondImage.Width / 2f, secondImage.Height / 2f);
                var attemptA = rotateA.MapPoints(good2);
                var offsetA = GetNetYOffset(good1, attemptA);
                var attemptB = rotateB.MapPoints(good2);
                var offsetB = GetNetYOffset(good1, attemptB);

                if (offsetA < baseOffset)
                {
                    baseOffset = offsetA;
                    finalRotation += rotationInc;
                }
                else if (offsetB < baseOffset)
                {
                    baseOffset = offsetB;
                    finalRotation -= rotationInc;
                }
                rotationInc /= 2f;
            }

            return finalRotation;
        }

        private static float FindZoom(SKPoint[] good1, SKPoint[] good2)
        {
            var baseOffset = GetNetYOffset(good1, good2);
            const float FINAL_ZOOM_DELTA = 0.0001f; //TODO: make configurable?
            var zoomInc = 1f;
            var finalZoom = 1f;

            while (zoomInc > FINAL_ZOOM_DELTA)
            {
                var zoomA = SKMatrix.MakeScale(finalZoom + zoomInc, finalZoom + zoomInc);
                var zoomB = SKMatrix.MakeScale(finalZoom - zoomInc, finalZoom - zoomInc);
                var attemptA = zoomA.MapPoints(good2);
                var offsetA = GetNetYOffset(good1, attemptA);
                var attemptB = zoomB.MapPoints(good2);
                var offsetB = GetNetYOffset(good1, attemptB);

                if (offsetA < baseOffset)
                {
                    baseOffset = offsetA;
                    finalZoom += zoomInc;
                }
                else if (offsetB < baseOffset)
                {
                    baseOffset = offsetB;
                    finalZoom -= zoomInc;
                }
                zoomInc /= 2f;
            }

            return finalZoom;
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