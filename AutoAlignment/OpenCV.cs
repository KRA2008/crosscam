using System;
using AutoAlignment;
using CrossCam.Wrappers;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
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
        public SKBitmap CreateAlignedSecondImage(SKBitmap firstImage, SKBitmap secondImage, int downsizePercentage, int iterations,
            int epsilonLevel, int eccCutoff)
        {
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
                    var criteria = new MCvTermCriteria(iterations, Math.Pow(10, -epsilonLevel));
                    var ecc = CvInvoke.FindTransformECC(downsizedSecondGrayMat, downsizedFirstGrayMat, transformMatrix,
                        MotionType.Euclidean, criteria);

                    if (transformMatrix.IsEmpty ||
                        ecc * 100 < eccCutoff)
                    {
                        return null;
                    }

                    unsafe
                    {
                        //|ScaleX SkewX  TransX|
                        //|SkewY  ScaleY TransY|
                        //|Persp0 Persp1 Persp2| (row omitted for affine)

                        var ptr = (float*)transformMatrix.DataPointer.ToPointer(); //ScaleX
                        ptr++; //SkewX
                        ptr++; //TransX
                        *ptr = 0; //we don't need any horizontal shifting
                        ptr++; //SkewY
                        ptr++; //ScaleY
                        ptr++; //TransY
                        *ptr /= downsizeFactor; //scale up the vertical shifting
                    }

                    using (var alignedMat = new Mat())
                    using (var fullSizeColorSecondMat = new Mat())
                    {
                        CvInvoke.Imdecode(GetBytes(secondImage, 1), ImreadModes.Color, fullSizeColorSecondMat);
                        CvInvoke.WarpAffine(fullSizeColorSecondMat, alignedMat, transformMatrix,
                            fullSizeColorSecondMat.Size);

#if __IOS__
                        return alignedMat.ToCGImage().ToSKBitmap();
#elif __ANDROID__
                        return alignedMat.ToBitmap().ToSKBitmap();
#endif
                    }
                }
            }
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