using System;
using System.Collections.Generic;
using System.Linq;
using AutoAlignment;
using CrossCam.Model;
using CrossCam.Wrappers;
#if !__NO_EMGU__
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Flann;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Emgu.CV.XFeatures2D;
#endif
using SkiaSharp;
using Xamarin.Forms;
using Color = System.Drawing.Color;
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

        public AlignedResult CreateAlignedSecondImage(SKBitmap firstImage, SKBitmap secondImage, int downsizePercentage, int iterations,
            int epsilonLevel, int eccCutoff, int pyramidLayers, bool discardTransX)
        {
#if __NO_EMGU__
            return null;
#else
            var mat1 = new Mat();
            var mat2 = new Mat();

            var topDownsizeFactor = downsizePercentage / 100f;

            var eccs = new List<double>();
            var eccWarpMatrix = Mat.Eye(2, 3, DepthType.Cv32F, 1);
            var termCriteria = new MCvTermCriteria(iterations, Math.Pow(10, -epsilonLevel));
            for (var ii = pyramidLayers - 1; ii >= 0; ii--)
            {
                var downsize = topDownsizeFactor / Math.Pow(2, ii);
                CvInvoke.Imdecode(GetBytes(firstImage, downsize), ImreadModes.Grayscale, mat1);
                CvInvoke.Imdecode(GetBytes(secondImage, downsize), ImreadModes.Grayscale, mat2);

                try
                {
                    var ecc = CvInvoke.FindTransformECC(mat2, mat1, eccWarpMatrix, MotionType.Affine, termCriteria);
                    eccs.Add(ecc);
                }
                catch (CvException e)
                {
                    if (e.Status == (int) ErrorCodes.StsNoConv)
                    {
                        return null;
                    }

                    throw;
                }

                if (eccWarpMatrix.IsEmpty)
                {
                    return null;
                }

                unsafe
                {
                    var ptr = (float*)eccWarpMatrix.DataPointer.ToPointer(); //ScaleX
                    ptr++; //SkewX
                    ptr++; //TransX
                    *ptr *= 2; //scale up the shifting
                    ptr++; //SkewY
                    ptr++; //ScaleY
                    ptr++; //TransY
                    *ptr *= 2; //scale up the shifting
                }
            }

            var lastUpscaleFactor = 1 / ( 2 * topDownsizeFactor );
            unsafe
            {
                var ptr = (float*)eccWarpMatrix.DataPointer.ToPointer(); //ScaleX
                ptr++; //SkewX
                ptr++; //TransX
                *ptr *= lastUpscaleFactor; //scale up the shifting
                ptr++; //SkewY
                ptr++; //ScaleY
                ptr++; //TransY
                *ptr *= lastUpscaleFactor; //scale up the shifting
            }

            if (eccs.Last() * 100 < eccCutoff)
            {
                return null;
            }

            var skMatrix = SKMatrix.MakeIdentity();
            unsafe
            {
                var ptr = (float*)eccWarpMatrix.DataPointer.ToPointer(); //ScaleX
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





            var warpMatrix = new Mat();
            const double SCALING_FACTOR = 0.5;
            VectorOfKeyPoint goodKeyPointsVector1;
            VectorOfKeyPoint allKeyPointsVector1;
            VectorOfKeyPoint goodKeyPointsVector2;
            VectorOfKeyPoint allKeyPointsVector2;
            VectorOfVectorOfDMatch goodMatchesVector;
            Mat funMat;
            using (var detector = new ORBDetector()) //TODO: tweak props?
            {
                // 1 = "object"
                // 2 = "scene"
                var grayscale1 = new Mat();
                var descriptors1 = new Mat();
                allKeyPointsVector1 = new VectorOfKeyPoint();
                CvInvoke.Imdecode(GetBytes(firstImage, SCALING_FACTOR), ImreadModes.Grayscale, grayscale1);
                detector.DetectAndCompute(grayscale1, null, allKeyPointsVector1, descriptors1, false);

                var grayscale2 = new Mat();
                var descriptors2 = new Mat();
                allKeyPointsVector2 = new VectorOfKeyPoint();
                CvInvoke.Imdecode(GetBytes(secondImage, SCALING_FACTOR), ImreadModes.Grayscale, grayscale2);
                detector.DetectAndCompute(grayscale2, null, allKeyPointsVector2, descriptors2, false);

                const double THRESHOLD = 4;
                var thresholdDistance = Math.Sqrt(Math.Pow(skMatrix.TransX * SCALING_FACTOR, 2) + Math.Pow(skMatrix.TransY * SCALING_FACTOR, 2)) * THRESHOLD;
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

                //var mask = Mat.Ones(allKeyPointsVector2.Size, allKeyPointsVector1.Size, DepthType.Cv8U, 1);

                var vectorOfMatches = new VectorOfVectorOfDMatch();
                var matcher = new BFMatcher(DistanceType.Hamming);
                var maskSpecial = new VectorOfMat(mask);
                matcher.Add(descriptors1);
                try
                {
                    //var mask = Mat.Ones(descriptors1.Rows, descriptors2.Rows, DepthType.Cv8U, 1);
                    //maskSpecial.Push(mask);
                    //var mask = Mat.Ones(1, ANYTHING, DepthType.Cv8U, 1);   // this gets past the first assertion but fails: masks[i].rows == queryDescriptorsCount && masks[i].cols == rows && masks[i].type() == CV_8UC1
                    matcher.KnnMatch(descriptors2, vectorOfMatches, 2, maskSpecial);
                }
                catch (CvException e)
                {
                    Debug.WriteLine(e.ErrorMessage);
                    return null;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    return null;
                }

                var goodMatches = new List<MDMatch>();
                //var closeMatches = new List<MDMatch>();
                for (var i = 0; i < vectorOfMatches.Size; i++)
                {
                    //var matchesOrderedByPhysicalDistance = vectorOfMatches[i].ToArray()
                    //    .OrderBy(m => CalculatePhysicalDistanceBetweenPoints(
                    //                    vectorTargets[m.QueryIdx],
                    //                    allKeyPointsVector2[m.TrainIdx].Point)).ToList();
                    //foreach (var mdMatch in matchesOrderedByPhysicalDistance)
                    //{
                    //    var queryPoint = allKeyPointsVector1[mdMatch.QueryIdx].Point;
                    //    var targetPoint = vectorTargets[mdMatch.QueryIdx];
                    //    var trainPoint = allKeyPointsVector2[mdMatch.TrainIdx].Point;
                    //    Debug.WriteLine(queryPoint.X + "," + queryPoint.Y + " " + targetPoint.X + "," + targetPoint.Y + " " + trainPoint.X + "," + trainPoint.Y);
                    //}

                    //closeMatches.Add(matchesOrderedByPhysicalDistance[0]);

                    if (vectorOfMatches[i].Size == 0)
                    {
                        continue;
                    }

                    if(vectorOfMatches[i].Size == 1 || (vectorOfMatches[i][0].Distance < 0.75 * vectorOfMatches[i][1].Distance))
                    {
                        goodMatches.Add(vectorOfMatches[i][0]);
                    }
                }

                //goodMatches = goodMatches.OrderBy(m => m.Distance).ToList().GetRange(0, 30);

                //var goodMatches = closeMatches.OrderBy(m => CalculatePhysicalDistanceBetweenPoints(
                //    vectorTargets[m.QueryIdx],
                //    allKeyPointsVector2[m.TrainIdx].Point)).ToList().GetRange(0, Math.Min(30,closeMatches.Count));


                //foreach (var mdMatch in goodMatches)
                //{
                //    var queryPoint = allKeyPointsVector1[mdMatch.QueryIdx].Point;
                //    var targetPoint = vectorTargets[mdMatch.QueryIdx];
                //    var trainPoint = allKeyPointsVector2[mdMatch.TrainIdx].Point;
                //    Debug.WriteLine(queryPoint.X + "," + queryPoint.Y + " " + targetPoint.X + "," + targetPoint.Y + " " + trainPoint.X + "," + trainPoint.Y);
                //}

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
                    //if (queryIndex > -1 && trainIndex > -1)
                    //{
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
                    //}
                    //else
                    //{
                    //    var what = "";
                    //}
                }


                goodMatchesVector = new VectorOfVectorOfDMatch(goodMatchesVectorList.ToArray());
                goodKeyPointsVector1 = new VectorOfKeyPoint(tempGoodKeyPoints1.ToArray());
                goodKeyPointsVector2 = new VectorOfKeyPoint(tempGoodKeyPoints2.ToArray());
                var goodPointsVector1 = new VectorOfPointF(tempGoodPoints1List.ToArray());
                var goodPointsVector2 = new VectorOfPointF(tempGoodPoints2List.ToArray());

                try
                {
                    Debug.WriteLine("MATCHES: " + goodMatches.Count);
                    funMat = CvInvoke.FindFundamentalMat(goodPointsVector1, goodPointsVector2);
                }
                catch
                {
                    return null;
                }

                //warpMatrix = CvInvoke.FindHomography(goodPointsVector1, goodPointsVector2, HomographyMethod.Ransac);
                //if (warpMatrix == null || warpMatrix.IsEmpty)
                //{
                //    return null;
                //}
            }




            var result = new AlignedResult
            {
                //TransformMatrix = skMatrix
            };

            using (var fullSizeColor1 = new Mat())
            using (var fullSizeColor2 = new Mat())
            using (var matrix1 = new Mat())
            using (var matrix2 = new Mat())
            using (var alignedMat1 = new Mat())
            using (var alignedMat2 = new Mat())
            {
                CvInvoke.Imdecode(GetBytes(firstImage, SCALING_FACTOR), ImreadModes.Color, fullSizeColor1);
                CvInvoke.Imdecode(GetBytes(secondImage, SCALING_FACTOR), ImreadModes.Color, fullSizeColor2);

                CvInvoke.StereoRectifyUncalibrated(goodKeyPointsVector1, goodKeyPointsVector2, funMat, fullSizeColor1.Size,
                    matrix1, matrix2);

                CvInvoke.WarpPerspective(fullSizeColor1, alignedMat1, matrix1, fullSizeColor1.Size);
                CvInvoke.WarpPerspective(fullSizeColor2, alignedMat2, matrix2, fullSizeColor2.Size);

                //var drawnResult = new Mat();
                //Features2DToolbox.DrawMatches(fullSizeColor1, goodKeyPointsVector1, fullSizeColor2, goodKeyPointsVector2, goodMatchesVector, drawnResult, new MCvScalar(0, 255, 0), new MCvScalar(255, 255, 0));
                //Features2DToolbox.DrawKeypoints(fullSizeColor1, goodKeyPointsVector1, alignedMat1, new Bgr(Color.LawnGreen));
                //Features2DToolbox.DrawKeypoints(fullSizeColor2, goodKeyPointsVector2, alignedMat2, new Bgr(Color.LawnGreen));

#if __IOS__
                //result.AlignedFirstBitmap = drawnResult.ToCGImage().ToSKBitmap();
                //result.AlignedSecondBitmap = drawnResult.ToCGImage().ToSKBitmap();
                //result.AlignedFirstBitmap = alignedMat1.ToCGImage().ToSKBitmap();
                //result.AlignedSecondBitmap = alignedMat2.ToCGImage().ToSKBitmap();

                //CvInvoke.Imdecode(GetBytes(firstImage, 1), ImreadModes.Color, fullSizeColor1);
                //CvInvoke.Imdecode(GetBytes(secondImage, 1), ImreadModes.Color, fullSizeColor2);

                //CvInvoke.WarpAffine(fullSizeColor2, alignedMat2, eccWarpMatrix, fullSizeColor2.Size);

                result.AlignedFirstBitmap = alignedMat1.ToCGImage().ToSKBitmap();
                result.AlignedSecondBitmap = alignedMat2.ToCGImage().ToSKBitmap();
#elif __ANDROID__
                //result.AlignedBitmap = alignedMat.ToBitmap().ToSKBitmap();
#endif
                return result;
            }
#endif
        }

        private static double CalculatePhysicalDistanceBetweenPoints(PointF from, PointF to)
        {
            return Math.Sqrt(Math.Pow(from.X - to.X, 2) + Math.Pow(from.Y - to.Y, 2));
        }

        private static byte[] GetBytes(SKBitmap bitmap, double downsize)
        {
            var width = (int)(bitmap.Width * downsize);
            var height = (int)(bitmap.Height * downsize);
            using (var tempSurface =
                SKSurface.Create(new SKImageInfo(width, height)))
            {
                var canvas = tempSurface.Canvas;
                canvas.Clear();

                canvas.DrawBitmap(bitmap,
                    SKRect.Create(0, 0, bitmap.Width, bitmap.Height),
                    SKRect.Create(0, 0, width, height));

                using (var data = tempSurface.Snapshot().Encode(SKEncodedImageFormat.Jpeg, 100))
                {
                    return data.ToArray();
                }
            }
        }
    }
}