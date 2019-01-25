using CrossCam.iOS.CustomRenderer;
using CrossCam.Wrappers;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SkiaSharp;
using SkiaSharp.Views.iOS;
using Xamarin.Forms;

[assembly: Dependency(typeof(OpenCv))]
namespace CrossCam.iOS.CustomRenderer
{
    public class OpenCv : IOpenCv
    {
        public byte[] CreateAlignedSecondImage(SKBitmap firstImage, SKBitmap secondImage)
        {
            byte[] alignedBytes;
            
            using (var firstMat = new Mat())
            using (var secondMat = new Mat())
            {
                CvInvoke.Imdecode(GetBytes(firstImage), ImreadModes.Color, firstMat);
                CvInvoke.Imdecode(GetBytes(secondImage), ImreadModes.Color, secondMat);
                
                using (var transform = CvInvoke.EstimateRigidTransform(firstMat, secondMat, true))
                {
                    if (transform.IsEmpty)
                    {
                        return null;
                    }

                    using (var alignedMat = new Mat())
                    {
                        CvInvoke.WarpAffine(secondMat, alignedMat, transform,
                            secondMat.Size);
                        
                        alignedBytes = GetBytes(alignedMat.ToCGImage().ToSKBitmap()); //TODO: just change signature to return SKBitmap?
                    }
                }
            }

            return alignedBytes;
        }

        private static byte[] GetBytes(SKBitmap bitmap)
        {
            using (var skImage = SKImage.FromBitmap(bitmap))
            using (var data = skImage.Encode(SKEncodedImageFormat.Jpeg, 100))
            {
                return data.ToArray();
            }
        }
    }
}