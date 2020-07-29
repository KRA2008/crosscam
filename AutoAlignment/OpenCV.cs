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
                    var ecc = CvInvoke.FindTransformECC(mat2, mat1, eccWarpMatrix, MotionType.Euclidean, termCriteria);
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

                ScaleUpMat(eccWarpMatrix, 2);
            }

            var lastUpscaleFactor = 1 / ( 2 * topDownsizeFactor );
            ScaleUpMat(eccWarpMatrix, lastUpscaleFactor);

            if (eccs.Last() * 100 < eccCutoff)
            {
                return null;
            }

            var skMatrix = ConvertCvMatToSkMatrix(eccWarpMatrix, false);




            var rigidWarpMatrix = new Mat();
            const double SCALING_FACTOR = 1;
            VectorOfKeyPoint goodKeyPointsVector1;
            VectorOfKeyPoint goodKeyPointsVector2;
            VectorOfVectorOfDMatch goodMatchesVector;
            Mat funMat;
            using (var detector = new ORBDetector())
            {
                var grayscale1 = new Mat();
                var descriptors1 = new Mat();
                var allKeyPointsVector1 = new VectorOfKeyPoint();
                CvInvoke.Imdecode(GetBytes(firstImage, SCALING_FACTOR), ImreadModes.Grayscale, grayscale1);
                detector.DetectAndCompute(grayscale1, null, allKeyPointsVector1, descriptors1, false);

                var grayscale2 = new Mat();
                var descriptors2 = new Mat();
                var allKeyPointsVector2 = new VectorOfKeyPoint();
                CvInvoke.Imdecode(GetBytes(secondImage, SCALING_FACTOR), ImreadModes.Grayscale, grayscale2);
                detector.DetectAndCompute(grayscale2, null, allKeyPointsVector2, descriptors2, false);

                const double THRESHOLD = 2;
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

                var vectorOfMatches = new VectorOfVectorOfDMatch();
                var matcher = new BFMatcher(DistanceType.Hamming);
                matcher.Add(descriptors1);
                matcher.KnnMatch(descriptors2, vectorOfMatches, 2, null);

                var goodMatches = new List<MDMatch>();
                for (var i = 0; i < vectorOfMatches.Size; i++)
                {
                    if (vectorOfMatches[i].Size == 0)
                    {
                        continue;
                    }

                    if(vectorOfMatches[i].Size == 1 || (vectorOfMatches[i][0].Distance < 0.75 * vectorOfMatches[i][1].Distance))
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

                goodMatchesVector = new VectorOfVectorOfDMatch(goodMatchesVectorList.ToArray());
                goodKeyPointsVector1 = new VectorOfKeyPoint(tempGoodKeyPoints1.ToArray());
                goodKeyPointsVector2 = new VectorOfKeyPoint(tempGoodKeyPoints2.ToArray());
                var goodPointsVector1 = new VectorOfPointF(tempGoodPoints1List.ToArray());
                var goodPointsVector2 = new VectorOfPointF(tempGoodPoints2List.ToArray());

                try
                {
                    Debug.WriteLine("MATCHES: " + goodMatches.Count);
                    funMat = CvInvoke.FindFundamentalMat(goodPointsVector1, goodPointsVector2);
                    rigidWarpMatrix = CvInvoke.EstimateRigidTransform(goodPointsVector2, goodPointsVector1, false);
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
#if __IOS__
                //CvInvoke.StereoRectifyUncalibrated(goodKeyPointsVector1, goodKeyPointsVector2, funMat, fullSizeColor1.Size,
                //    matrix1, matrix2);
                //CvInvoke.WarpPerspective(fullSizeColor1, alignedMat1, matrix1, fullSizeColor1.Size);
                //CvInvoke.WarpPerspective(fullSizeColor2, alignedMat2, matrix2, fullSizeColor2.Size);
                //result.AlignedFirstBitmap = alignedMat1.ToCGImage().ToSKBitmap();
                //result.AlignedSecondBitmap = alignedMat2.ToCGImage().ToSKBitmap();



                CvInvoke.WarpAffine(fullSizeColor2, alignedMat2, rigidWarpMatrix, fullSizeColor2.Size);
                result.AlignedFirstBitmap = fullSizeColor1.ToCGImage().ToSKBitmap();
                result.AlignedSecondBitmap = alignedMat2.ToCGImage().ToSKBitmap();



                //var drawnResult = new Mat();
                //Features2DToolbox.DrawMatches(fullSizeColor1, goodKeyPointsVector1, fullSizeColor2, goodKeyPointsVector2, goodMatchesVector, drawnResult, new MCvScalar(0, 255, 0), new MCvScalar(255, 255, 0));
                //result.AlignedFirstBitmap = drawnResult.ToCGImage().ToSKBitmap();
                //result.AlignedSecondBitmap = drawnResult.ToCGImage().ToSKBitmap();
#elif __ANDROID__
                //result.AlignedBitmap = alignedMat.ToBitmap().ToSKBitmap();
#endif
                return result;
            }
#endif
        }

        private static SKMatrix ConvertCvMatToSkMatrix(Mat mat, bool discardTransX)
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

        private static void ScaleUpMat(Mat mat, float factor)
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