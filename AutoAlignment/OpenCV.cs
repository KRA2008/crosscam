using AutoAlignment;
using CrossCam.Model;
using CrossCam.Wrappers;
#if !__NO_EMGU__
using Emgu.CV;
using Emgu.CV.CvEnum;
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
            int epsilonLevel, int eccCutoff, bool discardTransX)
        {
#if __NO_EMGU__
            return null;
#else
            var downsizeFactor = downsizePercentage / 100f;

            using (var downsizedFirstGrayMat = new Mat())
            using (var downsizedSecondGrayMat = new Mat())
            {
                CvInvoke.Imdecode(GetBytes(firstImage, downsizeFactor), ImreadModes.Grayscale,
                    downsizedFirstGrayMat);
                CvInvoke.Imdecode(GetBytes(secondImage, downsizeFactor), ImreadModes.Grayscale,
                    downsizedSecondGrayMat);

                using (var transformMatrix = Mat.Eye(2, 3, DepthType.Cv32F, 1))
                {
                    var criteria = new MCvTermCriteria(iterations, System.Math.Pow(10, -epsilonLevel));
                    double ecc;
                    try
                    {
                        ecc = CvInvoke.FindTransformECC(downsizedSecondGrayMat, downsizedFirstGrayMat, transformMatrix,
                            MotionType.Euclidean, criteria);
                    }
                    catch (CvException e)
                    {
                        if (e.Status == (int)ErrorCodes.StsNoConv)
                        {
                            return null;
                        }
                        throw;
                    }

                    if (transformMatrix.IsEmpty ||
                        ecc * 100 < eccCutoff)
                    {
                        return null;
                    }

                    var skMatrix = SKMatrix.MakeIdentity();
                    unsafe
                    {
                        //|ScaleX SkewX  TransX|
                        //|SkewY  ScaleY TransY|
                        //|Persp0 Persp1 Persp2| (row omitted for affine)

                        var ptr = (float*)transformMatrix.DataPointer.ToPointer(); //ScaleX
                        skMatrix.ScaleX = *ptr;
                        ptr++; //SkewX
                        skMatrix.SkewX = *ptr;
                        ptr++; //TransX
                        if (!discardTransX)
                        {
                            *ptr /= downsizeFactor; //scale up the shifting
                        }
                        else
                        {
                            *ptr = 0; //discard for side-by-side
                        }
                        skMatrix.TransX = *ptr;
                        ptr++; //SkewY
                        skMatrix.SkewY = *ptr;
                        ptr++; //ScaleY
                        skMatrix.ScaleY = *ptr;
                        ptr++; //TransY
                        *ptr /= downsizeFactor; //scale up the shifting
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
                        CvInvoke.WarpAffine(fullSizeColorSecondMat, alignedMat, transformMatrix,
                            fullSizeColorSecondMat.Size);

#if __IOS__
                        result.AlignedBitmap = alignedMat.ToCGImage().ToSKBitmap();
#elif __ANDROID__
                        result.AlignedBitmap = alignedMat.ToBitmap().ToSKBitmap();
#endif
                        return result;
                    }
                }
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