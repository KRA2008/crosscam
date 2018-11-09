using SkiaSharp;

namespace CrossCam.Page
{
    public class DrawTool
    {
        public static void DrawImagesOnCanvas(
            SKImageInfo info, SKCanvas canvas, SKBitmap leftBitmap, SKBitmap rightBitmap, float borderThickness,
            float leftLeftCrop, float leftRightCrop, float rightLeftCrop, float rightRightCrop,
            float topCrop, float bottomCrop)
        {
            if (leftBitmap == null && rightBitmap == null) return;

            var screenWidth = info.Width;
            var screenHeight = info.Height;

            var orientationRegulatedBorder = borderThickness * screenWidth;

            var sideWidthLessBorder = screenWidth / 2f - 2 * orientationRegulatedBorder;
            var sideHeightLessBorder = screenHeight - 2 * orientationRegulatedBorder;
            
            float leftBitmapWidthLessCrop = 0;
            float rightBitmapWidthLessCrop = 0;
            float bitmapHeightLessCrop = 0;
            float leftWidthRatio = 0;
            float rightWidthRatio = 0;
            float heightRatio = 0;
            float topConvertedCrop = 0;

            if (leftBitmap != null)
            {
                leftBitmapWidthLessCrop = leftBitmap.Width * (1 - (leftLeftCrop + leftRightCrop));
                leftWidthRatio = leftBitmapWidthLessCrop / sideWidthLessBorder;
                bitmapHeightLessCrop = leftBitmap.Height * (1 - (topCrop + bottomCrop));
                heightRatio = bitmapHeightLessCrop / sideHeightLessBorder;
                topConvertedCrop = leftBitmap.Height * topCrop;
            }

            if (rightBitmap != null)
            {
                rightBitmapWidthLessCrop = rightBitmap.Width * (1 - (rightLeftCrop + rightRightCrop));
                rightWidthRatio = rightBitmapWidthLessCrop / sideWidthLessBorder;
                if (leftBitmap == null)
                {
                    bitmapHeightLessCrop = rightBitmap.Height * (1 - (topCrop + bottomCrop));
                    heightRatio = bitmapHeightLessCrop / sideHeightLessBorder;
                    topConvertedCrop = rightBitmap.Height * topCrop;
                }
            }

            float scalingRatio = 0;
            if (leftBitmap != null &&
                rightBitmap == null) // left only
            {
                scalingRatio = leftWidthRatio >= heightRatio ? leftWidthRatio : heightRatio;
            }
            else if (leftBitmap == null) //right only
            {
                scalingRatio = rightWidthRatio >= heightRatio ? rightWidthRatio : heightRatio;
            }
            else if (leftWidthRatio >= rightWidthRatio &&
                     leftWidthRatio >= heightRatio) // both!
            {
                scalingRatio = leftWidthRatio;
            }
            else if (rightWidthRatio >= leftWidthRatio &&
                     rightWidthRatio >= heightRatio) // both!
            {
                scalingRatio = rightWidthRatio;
            }
            else if (heightRatio >= leftWidthRatio &&
                     heightRatio >= rightWidthRatio) // both!
            {
                scalingRatio = heightRatio;
            }

            var previewHeight = bitmapHeightLessCrop / scalingRatio;

            if (leftBitmap != null)
            {
                var leftPreviewWidth = leftBitmapWidthLessCrop / scalingRatio;
                canvas.DrawBitmap(
                    leftBitmap,
                    SKRect.Create(
                        leftLeftCrop * leftBitmap.Width,
                        topConvertedCrop,
                        leftBitmapWidthLessCrop,
                        bitmapHeightLessCrop),
                    SKRect.Create(
                        screenWidth / 2f - orientationRegulatedBorder - leftPreviewWidth,
                        (screenHeight - previewHeight) / 2f,
                        leftPreviewWidth,
                        previewHeight));
            }

            if (rightBitmap != null)
            {
                var rightPreviewWidth = rightBitmapWidthLessCrop / scalingRatio;
                canvas.DrawBitmap(
                    rightBitmap,
                    SKRect.Create(
                        rightLeftCrop * rightBitmap.Width,
                        topConvertedCrop,
                        rightBitmapWidthLessCrop,
                        bitmapHeightLessCrop),
                    SKRect.Create(
                        screenWidth / 2f + orientationRegulatedBorder,
                        (screenHeight - previewHeight) / 2f,
                        rightPreviewWidth,
                        previewHeight));
            }
        }
    }
}