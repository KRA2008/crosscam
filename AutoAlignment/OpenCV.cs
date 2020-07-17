﻿using System;
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
            const double SCALING_FACTOR = 0.2;
            VectorOfKeyPoint goodKeyPointsVector1;
            VectorOfKeyPoint allKeyPointsVector1;
            VectorOfKeyPoint goodKeyPointsVector2;
            VectorOfKeyPoint allKeyPointsVector2;
            VectorOfVectorOfDMatch goodMatchesVector;
            Mat funMat;
            using (var detector = new ORBDetector()) //TODO: more usings?
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

                var vectorOfMatches = new VectorOfVectorOfDMatch();

                //var indexParams = new LshIndexParams(6, 12, 1); //OpenCV people say this, FLANN people say 12,20,2
                //var matcher = new FlannBasedMatcher(indexParams, new SearchParams()); //TODO: tune this?
                //matcher.Add(descriptors1);
                //matcher.KnnMatch(descriptors2, vectorOfMatches, 2, null);

                var matcher = new BFMatcher(DistanceType.Hamming);
                matcher.Add(descriptors1);
                matcher.KnnMatch(descriptors2, vectorOfMatches, 5, null);

                //var standOutMatches = new List<MDMatch>();
                //for (var ii = 0; ii < vectorOfMatches.Size; ii++)
                //{
                //    if (vectorOfMatches[ii][0].Distance < 0.75 * vectorOfMatches[ii][1].Distance)
                //    {
                //        standOutMatches.Add(vectorOfMatches[ii][0]);
                //    }
                //}

                //var matchesQuota = Math.Min(20, standOutMatches.Count);
                //var distances = standOutMatches.Select(d => d.Distance).ToArray();
                //var mean = distances.Average();
                //var median = distances.ElementAt(distances.Length / 2);

                //double sumOfSquares = 0;
                //foreach (var distance in distances)
                //{
                //    sumOfSquares += Math.Pow(distance - mean, 2);
                //}
                //var stdDev = Math.Sqrt(sumOfSquares / distances.Length);

                //var goodMatches = standOutMatches.OrderBy(m => m.Distance).ToList().GetRange(0, matchesQuota);

                //var allowedSpan = 0.05 * Math.Sqrt(Math.Pow(grayscale1.Size.Height, 2) + Math.Pow(grayscale1.Size.Width, 2));
                var goodMatches = new List<MDMatch>();
                var scaledDistanceTarget = Math.Sqrt(Math.Pow(skMatrix.TransX, 2) + Math.Pow(skMatrix.TransY, 2)) * SCALING_FACTOR;
                //string unfilteredPhysicalDistances = "";
                for (var i = 0; i < vectorOfMatches.Size; i++)
                {
                    var orderedAcceptableMatches = vectorOfMatches[i].ToArray()
                        .OrderBy(m => CalculatePhysicalDistanceBetweenPoints(
                                        allKeyPointsVector1[m.QueryIdx].Point,
                                        allKeyPointsVector2[m.TrainIdx].Point)).ToList();

                    //if (orderedAcceptableMatches.Any())
                    //{
                    //    goodMatches.Add(orderedAcceptableMatches.First());
                    //}

                    //foreach (var m in vectorOfMatches[i].ToArray())
                    //{
                    //    unfilteredPhysicalDistances += CalculatePhysicalDistanceBetweenPoints(
                    //                        allKeyPointsVector1[m.QueryIdx].Point,
                    //                        allKeyPointsVector2[m.TrainIdx].Point).ToString("F1") + ",";
                    //}


                    //if (vectorOfMatches[i][0].Distance < 0.75 * vectorOfMatches[i][1].Distance)
                    //{
                    //    goodMatches.Add(vectorOfMatches[i][0]);
                    //}



                    //for (var j = 0; j < vectorOfMatches[i].Size; j++)
                    //{
                    //    var from = allKeyPointsVector1[vectorOfMatches[i][j].QueryIdx].Point;
                    //    var to = allKeyPointsVector2[vectorOfMatches[i][j].TrainIdx].Point;

                    //    //calculate local distance for each possible match
                    //    var physicalDistance = Math.Sqrt(Math.Pow(from.X - to.X, 2) + Math.Pow(from.Y - to.Y, 2));

                    //    //save as best match if local distance is in specified area and on same height
                    //    if (physicalDistance < allowedSpan)
                    //    {
                    //        goodMatches.Add(vectorOfMatches[i][j]);
                    //        break;
                    //    }
                    //}
                }
                //Debug.WriteLine("");
                //Debug.WriteLine("### UNFILTERED PHYSICAL DISTANCES");
                //Debug.WriteLine(unfilteredPhysicalDistances);


                //var filteredPhysicalDistances = "";
                //foreach (var m in goodMatches)
                //{
                //    filteredPhysicalDistances += CalculatePhysicalDistanceBetweenPoints(
                //                    allKeyPointsVector1[m.QueryIdx].Point,
                //                    allKeyPointsVector2[m.TrainIdx].Point).ToString("F1") + ",";
                //}
                //Debug.WriteLine("");
                //Debug.WriteLine("### FILTERED AND FIRST PHYSICAL DISTANCES");
                //Debug.WriteLine(filteredPhysicalDistances);

                //CvInvoke.CalcOpticalFlowFarneback();


                //const int QUOTA = 30;

                //var toTake = Math.Min(QUOTA, goodMatches.Count);
                //goodMatches = goodMatches.OrderBy(m =>
                //    CalculatePhysicalDistanceBetweenPoints(allKeyPointsVector1[m.QueryIdx].Point,
                //        allKeyPointsVector2[m.TrainIdx].Point)).ToList().GetRange(goodMatches.Count / 2 - toTake / 2, toTake);

                //goodMatches = goodMatches.OrderBy(m =>
                //    CalculatePhysicalDistanceBetweenPoints(allKeyPointsVector1[m.QueryIdx].Point,
                //        allKeyPointsVector2[m.TrainIdx].Point)).ToList().GetRange(0, Math.Min(QUOTA, goodMatches.Count));

                //goodMatches = goodMatches.OrderBy(m => m.Distance).ToList().GetRange(0, Math.Min(QUOTA, goodMatches.Count));


                var tempGoodPoints1List = new List<PointF>();
                var tempGoodKeyPoints1 = new List<MKeyPoint>();
                var tempGoodPoints2List = new List<PointF>();
                var tempGoodKeyPoints2 = new List<MKeyPoint>();

                var tempAllKeyPoints1List = allKeyPointsVector1.ToArray().ToList();
                var tempAllKeyPoints2List = allKeyPointsVector2.ToArray().ToList();

                var goodMatchesVectorList = new List<MDMatch[]>();
                for (var ii = 0; ii < goodMatches.Count; ii++)
                {
                    tempGoodPoints1List.Add(tempAllKeyPoints1List.ElementAt(goodMatches[ii].QueryIdx).Point);
                    tempGoodKeyPoints1.Add(tempAllKeyPoints1List.ElementAt(goodMatches[ii].QueryIdx));
                    tempGoodPoints2List.Add(tempAllKeyPoints2List.ElementAt(goodMatches[ii].TrainIdx).Point);
                    tempGoodKeyPoints2.Add(tempAllKeyPoints2List.ElementAt(goodMatches[ii].TrainIdx));
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

                //funMat = CvInvoke.FindFundamentalMat(goodPointsVector1, goodPointsVector2);

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
                //CvInvoke.Imdecode(GetBytes(firstImage, SCALING_FACTOR), ImreadModes.Color, fullSizeColor1);
                //CvInvoke.Imdecode(GetBytes(secondImage, SCALING_FACTOR), ImreadModes.Color, fullSizeColor2);
                //CvInvoke.StereoRectifyUncalibrated(goodKeyPointsVector1, goodKeyPointsVector2, funMat, fullSizeColor1.Size,
                //    matrix1, matrix2);

                //CvInvoke.WarpPerspective(fullSizeColor1, alignedMat1, matrix1, fullSizeColor1.Size);
                //CvInvoke.WarpPerspective(fullSizeColor2, alignedMat2, matrix2, fullSizeColor2.Size);

                //var drawnResult = new Mat();
                //Features2DToolbox.DrawMatches(fullSizeColor1, goodKeyPointsVector1, fullSizeColor2, goodKeyPointsVector2, goodMatchesVector, drawnResult, new MCvScalar(0, 255, 0), new MCvScalar(255, 255, 0));
                //Features2DToolbox.DrawKeypoints(fullSizeColor1, goodKeyPointsVector1, alignedMat1, new Bgr(Color.LawnGreen));
                //Features2DToolbox.DrawKeypoints(fullSizeColor2, goodKeyPointsVector2, alignedMat2, new Bgr(Color.LawnGreen));

#if __IOS__
                //result.AlignedFirstBitmap = drawnResult.ToCGImage().ToSKBitmap();
                //result.AlignedSecondBitmap = drawnResult.ToCGImage().ToSKBitmap();
                //result.AlignedFirstBitmap = alignedMat1.ToCGImage().ToSKBitmap();
                //result.AlignedSecondBitmap = alignedMat2.ToCGImage().ToSKBitmap();

                CvInvoke.Imdecode(GetBytes(firstImage, 1), ImreadModes.Color, fullSizeColor1);
                CvInvoke.Imdecode(GetBytes(secondImage, 1), ImreadModes.Color, fullSizeColor2);

                //CvInvoke.WarpPerspective(fullSizeColor2, alignedMat2, eccWarpMatrix, fullSizeColor2.Size);
                CvInvoke.WarpAffine(fullSizeColor2, alignedMat2, eccWarpMatrix, fullSizeColor2.Size);

                result.AlignedFirstBitmap = fullSizeColor1.ToCGImage().ToSKBitmap();
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