using System;
using System.Collections.Generic;
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
            Mat warpMatrix;
            VectorOfVectorOfDMatch goodMatchesVector;
            VectorOfKeyPoint goodKeyPointsVector1;
            VectorOfKeyPoint goodKeyPointsVector2;
            using (var detector = new ORBDetector())
            {
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
                var matcher = new BFMatcher(DistanceType.Hamming); //TODO: consider using crosscheck...?... (to be accepted, keypoints must be closest to each other in both directions)
                matcher.Add(descriptors1);
                matcher.KnnMatch(descriptors2, vectorOfMatches, 2, new VectorOfMat(mask));

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

                goodMatchesVector = new VectorOfVectorOfDMatch(goodMatchesVectorList.ToArray());
                goodKeyPointsVector1 = new VectorOfKeyPoint(tempGoodKeyPoints1.ToArray());
                goodKeyPointsVector2 = new VectorOfKeyPoint(tempGoodKeyPoints2.ToArray());
                var goodPointsVector1 = new VectorOfPointF(tempGoodPoints1List.ToArray());
                var goodPointsVector2 = new VectorOfPointF(tempGoodPoints2List.ToArray());



//#region STEREORECTIFYUNCALIBRATED

//                var funMat = CvInvoke.FindFundamentalMat(goodPointsVector1, goodPointsVector2); //TODO: try different orders?

//                var fullSizeColor1 = new Mat();
//                var fullSizeColor2 = new Mat();
//                CvInvoke.Imdecode(GetBytes(firstImage, 1), ImreadModes.Color, fullSizeColor1);
//                CvInvoke.Imdecode(GetBytes(secondImage, 1), ImreadModes.Color, fullSizeColor2);

//                var matrix1 = new Mat();
//                var matrix2 = new Mat();
//                var yesNo= CvInvoke.StereoRectifyUncalibrated(goodPointsVector1, goodPointsVector2, funMat, fullSizeColor1.Size,
//                    matrix1, matrix2);

//                var alignedMat1 = new Mat();
//                var alignedMat2 = new Mat();
//                CvInvoke.WarpPerspective(fullSizeColor1, alignedMat1, matrix1, fullSizeColor1.Size);
//                CvInvoke.WarpPerspective(fullSizeColor2, alignedMat2, matrix2, fullSizeColor2.Size);

//#if __ANDROID__
//                result.Rectified1 = alignedMat1.ToBitmap().ToSKBitmap();
//                result.Rectified2 = alignedMat2.ToBitmap().ToSKBitmap();
//#elif __IOS__
//                result.Rectified1 = alignedMat1.ToCGImage().ToSKBitmap();
//                result.Rectified2 = alignedMat2.ToCGImage().ToSKBitmap();
//#endif
//#endregion


                try
                {
                    warpMatrix = CvInvoke.FindHomography(goodPointsVector1, goodPointsVector2);
                }
                catch
                {
                    return null;
                }
            }

            //TODO: maybe just say it failed if the skew is too big? or try again without skew and return that result
            var result = new AlignedResult
            {
                TransformMatrix = ConvertCvMatOfFloatsToSkMatrix(warpMatrix, discardTransX)
            };

            using var fullSizeColor1 = new Mat();
            using var fullSizeColor2 = new Mat();
            using var alignedMat = new Mat();
            CvInvoke.Imdecode(GetBytes(firstImage, 1), ImreadModes.Color, fullSizeColor1);
            CvInvoke.Imdecode(GetBytes(secondImage, 1), ImreadModes.Color, fullSizeColor2);

            CvInvoke.WarpPerspective(fullSizeColor2, alignedMat, warpMatrix, fullSizeColor2.Size);

            using(var drawnResult = new Mat())
            {
                Features2DToolbox.DrawMatches(fullSizeColor1, goodKeyPointsVector1, fullSizeColor2, goodKeyPointsVector2, goodMatchesVector, drawnResult, new MCvScalar(0, 255, 0), new MCvScalar(255, 255, 0));
#if __IOS__
                    result.DrawnMatches = drawnResult.ToCGImage().ToSKBitmap();
                    result.AlignedBitmap = alignedMat.ToCGImage().ToSKBitmap();
#elif __ANDROID__
                result.DrawnMatches = drawnResult.ToBitmap().ToSKBitmap();
                result.AlignedBitmap = alignedMat.ToBitmap().ToSKBitmap();
#endif
            }
            return result;
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