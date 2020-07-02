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
using Emgu.CV.Flann;
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
            var warpMatrix = new Mat();

            using (var detector = new ORBDetector()) //TODO: more usings?
            {
                // 1 = "object"
                // 2 = "scene"
                var grayscale1 = new Mat();
                var descriptors1 = new Mat();
                var keyPoints1 = new VectorOfKeyPoint();
                CvInvoke.Imdecode(GetBytes(firstImage, 1), ImreadModes.Grayscale, grayscale1);
                detector.DetectAndCompute(grayscale1, null, keyPoints1, descriptors1, false);

                var grayscale2 = new Mat();
                var descriptors2 = new Mat();
                var keyPoints2 = new VectorOfKeyPoint();
                CvInvoke.Imdecode(GetBytes(secondImage, 1), ImreadModes.Grayscale, grayscale2);
                detector.DetectAndCompute(grayscale2, null, keyPoints2, descriptors2, false);

                var indexParams = new LshIndexParams(6, 12, 1); //OpenCV people say this, FLANN people say 12,20,2
                var matcher = new FlannBasedMatcher(indexParams, new SearchParams()); //TODO: tune this?
                matcher.Add(descriptors1);
                var vectorOfMatches = new VectorOfVectorOfDMatch();
                matcher.KnnMatch(descriptors2, vectorOfMatches, 2, null);

                const float SUCCESS_THRESHOLD = 0.75f;
                var goodMatches = new List<MDMatch>();
                for (var ii = 0; ii < vectorOfMatches.Size; ii++)
                {
                    if (vectorOfMatches[ii].Size > 1)
                    {
                        var matches = vectorOfMatches[ii].ToArray();
                        if (matches[0].Distance < SUCCESS_THRESHOLD * matches[1].Distance)
                        {
                            goodMatches.Add(matches[0]);
                        }
                    }
                }

                if (goodMatches.Count > 10)
                {
                    var points1 = new List<PointF>();
                    var points2 = new List<PointF>();

                    var keyPoints1List = keyPoints1.ToArray().ToList();
                    var keyPoints2List = keyPoints2.ToArray().ToList();

                    foreach (var goodMatch in goodMatches)
                    {
                        points1.Add(keyPoints1List.ElementAt(goodMatch.QueryIdx).Point);
                        points2.Add(keyPoints2List.ElementAt(goodMatch.TrainIdx).Point);
                    }

                    warpMatrix = CvInvoke.FindHomography(points1.ToArray(), points2.ToArray(), HomographyMethod.Ransac);
                    if (warpMatrix == null || warpMatrix.IsEmpty)
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }



            //var mat1 = new Mat();
            //var mat2 = new Mat();
            
            //var topDownsizeFactor = downsizePercentage / 100f;

            //var eccs = new List<double>();
            //var warpMatrix = Mat.Eye(2, 3, DepthType.Cv32F, 1);
            //var termCriteria = new MCvTermCriteria(iterations, Math.Pow(10, -epsilonLevel));
            //for (var ii = pyramidLayers-1; ii >= 0; ii--)
            //{
            //    var downsize = topDownsizeFactor / Math.Pow(2, ii);
            //    CvInvoke.Imdecode(GetBytes(firstImage, downsize), ImreadModes.Grayscale, mat1);
            //    CvInvoke.Imdecode(GetBytes(secondImage, downsize), ImreadModes.Grayscale, mat2);

            //    try
            //    {
            //        var ecc = CvInvoke.FindTransformECC(mat2, mat1, warpMatrix, MotionType.Euclidean, termCriteria);
            //        eccs.Add(ecc);
            //    }
            //    catch (CvException e)
            //    {
            //        if (e.Status == (int)ErrorCodes.StsNoConv)
            //        {
            //            return null;
            //        }
            //        throw;
            //    }

            //    if (warpMatrix.IsEmpty)
            //    {
            //        return null;
            //    }

                //unsafe
                //{
                //    var ptr = (float*)warpMatrix.DataPointer.ToPointer(); //ScaleX
                //    ptr++; //SkewX
                //    ptr++; //TransX
                //    *ptr *= 2; //scale up the shifting
                //    ptr++; //SkewY
                //    ptr++; //ScaleY
                //    ptr++; //TransY
                //    *ptr *= 2; //scale up the shifting
                //}
            //}

            //var lastUpscaleFactor = 1 / ( 2 * topDownsizeFactor );
            //unsafe
            //{
            //    var ptr = (float*)warpMatrix.DataPointer.ToPointer(); //ScaleX
            //    ptr++; //SkewX
            //    ptr++; //TransX
            //    *ptr *= lastUpscaleFactor; //scale up the shifting
            //    ptr++; //SkewY
            //    ptr++; //ScaleY
            //    ptr++; //TransY
            //    *ptr *= lastUpscaleFactor; //scale up the shifting
            //}

            //if (eccs.Last() * 100 < eccCutoff)
            //{
            //    return null;
            //}
            
            var skMatrix = SKMatrix.MakeIdentity();
            unsafe
            {
                var ptr = (float*)warpMatrix.DataPointer.ToPointer(); //ScaleX
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

            var result = new AlignedResult
            {
                TransformMatrix = skMatrix
            };

            using (var alignedMat = new Mat())
            using (var fullSizeColorSecondMat = new Mat())
            {
                CvInvoke.Imdecode(GetBytes(secondImage, 1), ImreadModes.Color, fullSizeColorSecondMat);
                CvInvoke.WarpPerspective(fullSizeColorSecondMat, alignedMat, warpMatrix, fullSizeColorSecondMat.Size);
                //CvInvoke.WarpAffine(fullSizeColorSecondMat, alignedMat, warpMatrix,
                //    fullSizeColorSecondMat.Size);

#if __IOS__
                result.AlignedBitmap = alignedMat.ToCGImage().ToSKBitmap();
#elif __ANDROID__
                result.AlignedBitmap = alignedMat.ToBitmap().ToSKBitmap();
#endif
                return result;
            }
#endif
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