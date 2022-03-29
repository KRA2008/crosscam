﻿using System;
using System.Diagnostics;
using CrossCam.Model;
using CrossCam.ViewModel;
using CrossCam.Wrappers;
using SkiaSharp;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace CrossCam.Page
{
    public class DrawTool
    {
        public const double BORDER_CONVERSION_FACTOR = 0.001;
        public const float FLOATY_ZERO = 0.00001f;
        private const double FUSE_GUIDE_WIDTH_RATIO = 0.0127;
        private const int FUSE_GUIDE_MARGIN_HEIGHT_RATIO = 7;
        private static readonly double halfScreenAspect =
            Math.Min(DeviceDisplay.MainDisplayInfo.Width, DeviceDisplay.MainDisplayInfo.Height) /
            (Math.Max(DeviceDisplay.MainDisplayInfo.Width, DeviceDisplay.MainDisplayInfo.Height) / 2);

        public static void DrawImagesOnCanvas(SKCanvas canvas, SKBitmap leftBitmap, SKBitmap rightBitmap,
            Settings settings, Edits edits, DrawMode drawMode, bool isFov = false, bool withSwap = false,
            DrawQuality drawQuality = DrawQuality.Save, double cardboardVert = 0, double cardboardHor = 0)
        {
            double innerCardboardCrop = 0;
            double outerCardboardCrop = 0;
            if (drawMode == DrawMode.Cardboard)
            {
                var cardboardCrop = CalculateOutsideCropForCardboardWidthFit(settings);
                if (cardboardCrop > 0)
                {
                    outerCardboardCrop = cardboardCrop;
                }
                else
                {
                    innerCardboardCrop = Math.Abs(cardboardCrop);
                }
            }

            var useGhosts = drawQuality == DrawQuality.Preview &&
                            settings.ShowGhostCaptures &&
                            (drawMode == DrawMode.Cross ||
                             drawMode == DrawMode.Parallel);

            var fuseGuideRequested = drawQuality != DrawQuality.Preview && 
                                     settings.SaveWithFuseGuide;

            var skFilterQuality = drawQuality == DrawQuality.Preview || 
                                  drawQuality == DrawQuality.Review ? 
                                  SKFilterQuality.Low : SKFilterQuality.High;

            var addBarrelDistortion =
                settings.AddBarrelDistortion && settings.AddBarrelDistortionFinalOnly && drawQuality != DrawQuality.Preview ||
                settings.AddBarrelDistortion && !settings.AddBarrelDistortionFinalOnly;

            double cardboardWidthProportion = 0;
            if (drawMode == DrawMode.Cardboard)
            {
                cardboardWidthProportion = settings.CardboardIpd /
                                           (Math.Max(DeviceDisplay.MainDisplayInfo.Width,
                                                DeviceDisplay.MainDisplayInfo.Height) /
                                            DeviceDisplay.MainDisplayInfo.Density / 2d) / 2d;
            }

            var cardboardDownsizeProportion = drawQuality != DrawQuality.Save &&
                                              drawMode == DrawMode.Cardboard &&
                                              settings.CardboardDownsize ? settings.CardboardDownsizePercentage / 100d : 1;

            if (withSwap)
            {
                DrawImagesOnCanvasInternal(canvas, rightBitmap, leftBitmap,
                    settings.BorderWidthProportion, settings.AddBorder && drawQuality != DrawQuality.Preview, settings.BorderColor,
                    edits.InsideCrop + edits.LeftCrop + innerCardboardCrop, edits.RightCrop + edits.OutsideCrop + outerCardboardCrop,
                    edits.LeftCrop + edits.OutsideCrop + outerCardboardCrop, edits.InsideCrop + edits.RightCrop + innerCardboardCrop,
                    edits.TopCrop, edits.BottomCrop,
                    edits.RightRotation, edits.LeftRotation,
                    edits.VerticalAlignment,
                    edits.RightZoom + (isFov ? edits.FovRightCorrection : 0), edits.LeftZoom + (isFov ? edits.FovLeftCorrection : 0),
                    edits.RightKeystone, edits.LeftKeystone,
                    drawMode, fuseGuideRequested,
                    addBarrelDistortion, settings.CardboardBarrelDistortion,
                    skFilterQuality,
                    useGhosts,
                    cardboardWidthProportion, settings.ImmersiveCardboardFinal ? cardboardVert : 0, settings.ImmersiveCardboardFinal ? cardboardHor : 0,
                    cardboardDownsizeProportion);
            }
            else
            {
                DrawImagesOnCanvasInternal(canvas, leftBitmap, rightBitmap,
                    settings.BorderWidthProportion, settings.AddBorder && drawQuality != DrawQuality.Preview, settings.BorderColor,
                    edits.LeftCrop + edits.OutsideCrop + outerCardboardCrop, edits.InsideCrop + edits.RightCrop + innerCardboardCrop, edits.InsideCrop + edits.LeftCrop + innerCardboardCrop,
                    edits.RightCrop + edits.OutsideCrop + outerCardboardCrop,
                    edits.TopCrop, edits.BottomCrop,
                    edits.LeftRotation, edits.RightRotation,
                    edits.VerticalAlignment,
                    edits.LeftZoom + (isFov ? edits.FovLeftCorrection : 0), edits.RightZoom + (isFov ? edits.FovRightCorrection : 0),
                    edits.LeftKeystone, edits.RightKeystone,
                    drawMode, fuseGuideRequested,
                    addBarrelDistortion, settings.CardboardBarrelDistortion,
                    skFilterQuality,
                    useGhosts,
                    cardboardWidthProportion, settings.ImmersiveCardboardFinal ? cardboardVert : 0, settings.ImmersiveCardboardFinal ? cardboardHor : 0,
                    cardboardDownsizeProportion);
            }
        }

        private static void DrawImagesOnCanvasInternal(
            SKCanvas canvas, SKBitmap leftBitmap, SKBitmap rightBitmap,
            int borderThickness, bool addBorder, BorderColor borderColor,
            double leftLeftCrop, double leftRightCrop, double rightLeftCrop, double rightRightCrop,
            double topCrop, double bottomCrop,
            float leftRotation, float rightRotation, double alignment,
            double leftZoom, double rightZoom,
            float leftKeystone, float rightKeystone,
            DrawMode drawMode, bool fuseGuideRequested,
            bool addBarrelDistortion, int barrelStrength,
            SKFilterQuality skFilterQuality, bool useGhosts, 
            double cardboardWidthProportion,
            double cardboardVert,
            double cardboardHor,
            double cardboardDownsize)
        {
            if (leftBitmap == null && rightBitmap == null) return;

            var canvasWidth = canvas.DeviceClipBounds.Width;
            var canvasHeight = canvas.DeviceClipBounds.Height;

            int leftBitmapWidthLessCrop;
            int baseHeight;

            if (leftBitmap != null)
            {
                baseHeight = leftBitmap.Height;
                leftBitmapWidthLessCrop = (int)(leftBitmap.Width * (1 - (leftLeftCrop + leftRightCrop)));
            }
            else
            {
                baseHeight = rightBitmap.Height;
                leftBitmapWidthLessCrop = (int)(rightBitmap.Width * (1 - (rightLeftCrop + rightRightCrop)));
            }

            var leftBitmapHeightLessCrop = (int)(baseHeight * (1 - (topCrop + bottomCrop + Math.Abs(alignment))));
            var overlayDrawing =
                drawMode == DrawMode.GrayscaleRedCyanAnaglyph ||
                drawMode == DrawMode.RedCyanAnaglyph ||
                useGhosts;
            var innerBorderThicknessProportion = leftBitmap != null && 
                                                 rightBitmap != null && 
                                                 addBorder &&
                                                 drawMode != DrawMode.Cardboard &&
                                                 !overlayDrawing ? 
                BORDER_CONVERSION_FACTOR * borderThickness : 
                0;

            var widthRatio =
                (leftBitmapWidthLessCrop + leftBitmapWidthLessCrop * innerBorderThicknessProportion * 1.5) /
                (canvasWidth / 2f);
            if (overlayDrawing)
            {
                widthRatio /= 2;
            }

            var bitmapHeightWithEditsAndBorder =
                leftBitmapHeightLessCrop + leftBitmapWidthLessCrop * innerBorderThicknessProportion * 2;
            var drawFuseGuide = fuseGuideRequested &&
                                drawMode != DrawMode.Cardboard &&
                                !overlayDrawing &&
                                leftBitmap != null && rightBitmap != null;

            float fuseGuideIconWidth = 0;
            float fuseGuideMarginHeight = 0;
            if (drawFuseGuide)
            {
                fuseGuideIconWidth = CalculateFuseGuideWidth((float) bitmapHeightWithEditsAndBorder);
                fuseGuideMarginHeight = CalculateFuseGuideMarginHeight((float) bitmapHeightWithEditsAndBorder);
                bitmapHeightWithEditsAndBorder += fuseGuideMarginHeight;
            }

            var heightRatio = bitmapHeightWithEditsAndBorder / (1f * canvasHeight);
            var scalingRatio = widthRatio > heightRatio ? widthRatio : heightRatio;

            fuseGuideIconWidth = (float) (fuseGuideIconWidth / scalingRatio);
            fuseGuideMarginHeight = (float) (fuseGuideMarginHeight / scalingRatio);

            float leftPreviewX;
            float rightPreviewX;
            float previewY;
            var sidePreviewWidthLessCrop = (float) (leftBitmapWidthLessCrop / scalingRatio);
            var previewHeightLessCrop = (float) (leftBitmapHeightLessCrop / scalingRatio);

            if (overlayDrawing)
            {
                leftPreviewX = rightPreviewX = canvasWidth / 2f - sidePreviewWidthLessCrop / 2f;
                previewY = canvasHeight / 2f - previewHeightLessCrop / 2f;
            }
            else
            {
                leftPreviewX = (float) (canvasWidth / 2f - (sidePreviewWidthLessCrop +
                                                            innerBorderThicknessProportion * sidePreviewWidthLessCrop /
                                                            2f));
                rightPreviewX =
                    (float) (canvasWidth / 2f + innerBorderThicknessProportion * sidePreviewWidthLessCrop / 2f);
                previewY = canvasHeight / 2f - previewHeightLessCrop / 2f;
            }

            if (drawFuseGuide)
            {
                previewY += fuseGuideMarginHeight / 2f;
            }

            var isRightRotated = Math.Abs(rightRotation) > FLOATY_ZERO;
            var isLeftRotated = Math.Abs(leftRotation) > FLOATY_ZERO;
            var isRightKeystoned = Math.Abs(rightKeystone) > FLOATY_ZERO;
            var isLeftKeystoned = Math.Abs(leftKeystone) > FLOATY_ZERO;

            if (leftBitmap != null)
            {
                SKBitmap edited = null;
                if (isLeftRotated ||
                    leftZoom > 0 ||
                    drawMode == DrawMode.GrayscaleRedCyanAnaglyph ||
                    isLeftKeystoned)
                {
                    edited = DoSolitaryEdits(leftBitmap, drawMode, leftZoom, isLeftRotated, leftRotation, isLeftKeystoned, -leftKeystone, skFilterQuality);
                }

                //if (isLeftKeystoned)
                //{
                //    leftBitmap = Keystone(leftBitmap, -leftKeystone, skFilterQuality);
                //}

                using var paint = new SKPaint
                {
                    FilterQuality = skFilterQuality
                };
                
                if (drawMode == DrawMode.RedCyanAnaglyph ||
                    drawMode == DrawMode.GrayscaleRedCyanAnaglyph)
                {
                    paint.ColorFilter =
                        SKColorFilter.CreateColorMatrix(new float[]
                        {
                            0, 0, 0, 0, 0,
                            0, 1, 0, 0, 0,
                            0, 0, 1, 0, 0,
                            0, 0, 0, 1, 0
                        });
                }

                if (drawMode == DrawMode.Cardboard &&
                    addBarrelDistortion)
                {
                    leftBitmap = CameraViewModel.BitmapDownsize(leftBitmap, cardboardDownsize / scalingRatio,
                        skFilterQuality);
                }

                var width = leftBitmap.Width;
                var height = leftBitmap.Height;

                var srcWidth = (float)(width - width * (leftLeftCrop + leftRightCrop));
                var srcHeight = (float)(height - height * (topCrop + bottomCrop + Math.Abs(alignment)));
                var srcX = (float)(width * leftLeftCrop + cardboardHor * srcWidth);
                var srcY = (float) (height * topCrop + (alignment > 0 ? alignment * height : 0) +
                                    cardboardVert * srcHeight);

                if (drawMode == DrawMode.Cardboard &&
                    addBarrelDistortion)
                {
                    var openCv = DependencyService.Get<IOpenCv>();
                    if (openCv.IsOpenCvSupported())
                    {
                        var bitmapAspect = srcHeight / srcWidth;
                        float cx;
                        if (bitmapAspect < halfScreenAspect)
                        {
                            //bitmap will be full width
                            cx = (float)((1 - cardboardWidthProportion) * srcWidth + srcX);
                        }
                        else
                        {
                            //bitmap will be full height
                            var restoredWidth = srcHeight / halfScreenAspect;
                            var missingRestoredWidth = restoredWidth - srcWidth;
                            cx = (float)((1 - cardboardWidthProportion) * (srcWidth - missingRestoredWidth) + srcX);
                        }

                        leftBitmap = openCv.AddBarrelDistortion(leftBitmap, barrelStrength / 100f, cx,
                            srcY + srcHeight / 2f, srcWidth, srcHeight);
                    }
                }

                canvas.DrawBitmap(
                    edited ?? leftBitmap,
                    SKRect.Create(
                        srcX,
                        srcY,
                        srcWidth,
                        srcHeight),
                    SKRect.Create(
                        leftPreviewX,
                        previewY,
                        sidePreviewWidthLessCrop,
                        previewHeightLessCrop),
                    paint);
                edited?.Dispose();
            }

            if (rightBitmap != null)
            {
                SKBitmap edited = null;
                if (isRightRotated ||
                    rightZoom > 0 ||
                    drawMode == DrawMode.GrayscaleRedCyanAnaglyph ||
                    isRightKeystoned)
                {
                    edited = DoSolitaryEdits(rightBitmap, drawMode, rightZoom, isRightRotated, rightRotation, isRightKeystoned, rightKeystone, skFilterQuality);
                }

                //if (isRightKeystoned)
                //{
                //    rightBitmap = Keystone(rightBitmap, rightKeystone, skFilterQuality);
                //}

                using var paint = new SKPaint
                {
                    FilterQuality = skFilterQuality
                };

                if (drawMode == DrawMode.RedCyanAnaglyph ||
                    drawMode == DrawMode.GrayscaleRedCyanAnaglyph)
                {
                    paint.ColorFilter =
                        SKColorFilter.CreateColorMatrix(new float[]
                        {
                            1, 0, 0, 0, 0,
                            0, 0, 0, 0, 0,
                            0, 0, 0, 0, 0,
                            0, 0, 0, 1, 0
                        });
                    paint.BlendMode = SKBlendMode.Plus;
                }

                if (useGhosts && 
                    leftBitmap != null &&
                    rightBitmap != null)
                {
                    paint.Color = paint.Color.WithAlpha((byte) (0xFF * 0.5f));
                }

                if (drawMode == DrawMode.Cardboard &&
                    addBarrelDistortion)
                {
                    rightBitmap = CameraViewModel.BitmapDownsize(rightBitmap, cardboardDownsize / scalingRatio, skFilterQuality);
                }

                var width = rightBitmap.Width;
                var height = rightBitmap.Height;

                var srcWidth = (float) (width - width * (rightLeftCrop + rightRightCrop));
                var srcHeight = (float) (height - height * (topCrop + bottomCrop + Math.Abs(alignment)));
                var srcX = (float)(width * rightLeftCrop + cardboardHor * srcWidth);
                var srcY = (float) (height * topCrop - (alignment < 0 ? alignment * height : 0) +
                                    cardboardVert * srcHeight);

                if (drawMode == DrawMode.Cardboard &&
                    addBarrelDistortion)
                {
                    var openCv = DependencyService.Get<IOpenCv>();
                    if (openCv.IsOpenCvSupported())
                    {
                        var bitmapAspect = srcHeight / srcWidth;
                        float cx;
                        if (bitmapAspect < halfScreenAspect)
                        {
                            //bitmap will be full width
                            cx = (float)(cardboardWidthProportion * srcWidth + srcX);
                        }
                        else
                        {
                            //bitmap will be full height
                            var restoredWidth = srcHeight / halfScreenAspect;
                            var missingRestoredWidth = restoredWidth - srcWidth;
                            cx = (float)(cardboardWidthProportion * (srcWidth + missingRestoredWidth) + srcX);
                        }

                        rightBitmap = openCv.AddBarrelDistortion(rightBitmap, barrelStrength / 100f, cx,
                            srcY + srcHeight / 2f, srcWidth, srcHeight);
                    }
                }

                canvas.DrawBitmap(
                    edited ?? rightBitmap,
                    SKRect.Create(
                        srcX,
                        srcY,
                        srcWidth,
                        srcHeight),
                    SKRect.Create(
                        rightPreviewX,
                        previewY,
                        sidePreviewWidthLessCrop,
                        previewHeightLessCrop),
                    paint);
                edited?.Dispose();
            }

            if (innerBorderThicknessProportion > 0)
            {
                using var borderPaint = new SKPaint
                {
                    Color = borderColor == BorderColor.Black ? SKColor.Parse("000000") : SKColor.Parse("ffffff"),
                    Style = SKPaintStyle.StrokeAndFill,
                    FilterQuality = skFilterQuality
                };

                var originX = (float)(leftPreviewX - innerBorderThicknessProportion * sidePreviewWidthLessCrop);
                var originY = (float)(previewY - innerBorderThicknessProportion * sidePreviewWidthLessCrop);
                var fullPreviewWidth = (float)(2 * sidePreviewWidthLessCrop + 3 * innerBorderThicknessProportion * sidePreviewWidthLessCrop);
                var fullPreviewHeight = (float)(previewHeightLessCrop + 2 * innerBorderThicknessProportion * sidePreviewWidthLessCrop);
                var scaledBorderThickness = (float)(innerBorderThicknessProportion * sidePreviewWidthLessCrop);
                var endX = rightPreviewX + sidePreviewWidthLessCrop;
                var endY = previewY + previewHeightLessCrop;
                canvas.DrawRect(originX, originY, fullPreviewWidth, scaledBorderThickness, borderPaint);
                canvas.DrawRect(originX, originY, scaledBorderThickness, fullPreviewHeight, borderPaint);
                canvas.DrawRect(canvasWidth / 2f - scaledBorderThickness / 2f, originY, scaledBorderThickness, fullPreviewHeight, borderPaint);
                canvas.DrawRect(endX, originY, scaledBorderThickness, fullPreviewHeight, borderPaint);
                canvas.DrawRect(originX, endY, fullPreviewWidth, scaledBorderThickness, borderPaint);
            }

            if (drawFuseGuide)
            {
                var previewBorderThickness = canvasWidth / 2f - (leftPreviewX + sidePreviewWidthLessCrop);
                var fuseGuideY = previewY - 2 * previewBorderThickness - fuseGuideMarginHeight / 2f; //why 2x border? why doesn't this have to account for icon height? i don't know.
                using var whitePaint = new SKPaint
                {
                    Color = new SKColor(byte.MaxValue, byte.MaxValue, byte.MaxValue),
                    FilterQuality = skFilterQuality
                };
                canvas.DrawRect(
                    canvasWidth / 2f - previewBorderThickness - sidePreviewWidthLessCrop / 2f - fuseGuideIconWidth / 2f,
                    fuseGuideY, fuseGuideIconWidth, fuseGuideIconWidth, whitePaint);
                canvas.DrawRect(
                    canvasWidth / 2f + previewBorderThickness + sidePreviewWidthLessCrop / 2f + fuseGuideIconWidth / 2f,
                    fuseGuideY, fuseGuideIconWidth, fuseGuideIconWidth, whitePaint);
            }
        }

        private static SKBitmap DoSolitaryEdits(SKBitmap originalBitmap, DrawMode drawMode, double zoom, bool isRotated, float rotation, bool isKeystoned, float keystone,
             SKFilterQuality quality)
        {
            var resultBitmap = new SKBitmap(originalBitmap.Width, originalBitmap.Height);

            using var skPaint = new SKPaint
            {
                FilterQuality = quality
            };
            if (drawMode == DrawMode.GrayscaleRedCyanAnaglyph)
            {
                skPaint.ColorFilter =
                    SKColorFilter.CreateColorMatrix(new[]
                    {
                        0.21f, 0.72f, 0.07f, 0.0f, 0.0f,
                        0.21f, 0.72f, 0.07f, 0.0f, 0.0f,
                        0.21f, 0.72f, 0.07f, 0.0f, 0.0f,
                        0.0f,  0.0f,  0.0f,  1.0f, 0.0f
                    });
            }

            using var canvas = new SKCanvas(resultBitmap);

            var zoomedX = originalBitmap.Width * zoom / -2f;
            var zoomedY = originalBitmap.Height * zoom / -2f;
            var zoomedWidth = originalBitmap.Width * (1 + zoom);
            var zoomedHeight = originalBitmap.Height * (1 + zoom);
            if (isRotated)
            {
                canvas.RotateDegrees(rotation, originalBitmap.Width / 2f,
                    originalBitmap.Height / 2f);
            }

            if (isKeystoned)
            {
                var currentMatrix = canvas.TotalMatrix;
                var keystoneMatrix = TaperTransform.Make(new SKSize(originalBitmap.Width, originalBitmap.Height),
                    keystone > 0 ? TaperSide.Left : TaperSide.Right, TaperCorner.Both, 1 - Math.Abs(keystone));
                var targetMatrix = SKMatrix.MakeIdentity();
                if (keystone > 0)
                {
                    //SKMatrix.
                    SKMatrix.Concat(ref targetMatrix, currentMatrix, keystoneMatrix);
                    //SKMatrix.PreConcat(ref currentMatrix, keystoneMatrix); //TODO: do pre or post? how to decide?
                    Debug.WriteLine(targetMatrix);
                }
                else
                {
                    SKMatrix.Concat(ref targetMatrix, keystoneMatrix, currentMatrix);
                    //SKMatrix.PostConcat(ref currentMatrix, keystoneMatrix); //TODO: do pre or post? how to decide?
                    Debug.WriteLine(targetMatrix);
                }
                canvas.SetMatrix(currentMatrix);
            }
            canvas.Clear();
            canvas.DrawBitmap(
                originalBitmap,
                SKRect.Create(0, 0, originalBitmap.Width, originalBitmap.Height),
                SKRect.Create((float) zoomedX, (float) zoomedY, (float) zoomedWidth, (float) zoomedHeight),
                skPaint);

            return resultBitmap;
        }

        private static SKBitmap Keystone(SKBitmap originalBitmap, float keystone, SKFilterQuality quality)
        {
            using var skPaint = new SKPaint
            {
                FilterQuality = quality
            };
            var keystoned = new SKBitmap(originalBitmap.Width, originalBitmap.Height);
            using var tempCanvas = new SKCanvas(keystoned);
            tempCanvas.SetMatrix(TaperTransform.Make(new SKSize(originalBitmap.Width, originalBitmap.Height),
                keystone > 0 ? TaperSide.Left : TaperSide.Right, TaperCorner.Both, 1 - Math.Abs(keystone)));
            tempCanvas.Clear();
            tempCanvas.DrawBitmap(
                originalBitmap,
                0,
                0,
                skPaint);
            return keystoned;
        }

        public static int CalculateOverlayedCanvasWidthWithEditsNoBorder(SKBitmap leftBitmap, SKBitmap rightBitmap, Edits edits)
        {
            int baseWidth;
            if (leftBitmap == null || rightBitmap == null)
            {
                baseWidth = leftBitmap?.Width ?? rightBitmap?.Width ?? 0;
            }
            else
            {
                baseWidth = Math.Min(leftBitmap.Width, rightBitmap.Width);
            }
            return (int)(baseWidth - baseWidth *
                (edits.LeftCrop + edits.InsideCrop + edits.OutsideCrop + edits.RightCrop));
        }

        public static double CalculateOutsideCropForCardboardWidthFit(Settings settings)
        {
            var displayLandscapeSideWidth =
                Math.Max(DeviceDisplay.MainDisplayInfo.Width, DeviceDisplay.MainDisplayInfo.Height) /
                (2d * DeviceDisplay.MainDisplayInfo.Density);
            var overrun = settings.CardboardIpd - displayLandscapeSideWidth;
            return overrun / displayLandscapeSideWidth;
        }

        public static int CalculateJoinedCanvasWidthWithEditsNoBorder(SKBitmap leftBitmap, SKBitmap rightBitmap,
            Edits edits)
        {
            return CalculateJoinedCanvasWidthWithEditsNoBorderInternal(leftBitmap, rightBitmap,
                edits.LeftCrop + edits.OutsideCrop, edits.InsideCrop + edits.RightCrop,
                edits.InsideCrop + edits.LeftCrop,
                edits.RightCrop + edits.OutsideCrop);
        }

        private static int CalculateJoinedCanvasWidthWithEditsNoBorderInternal(SKBitmap leftBitmap, SKBitmap rightBitmap, 
            double leftLeftCrop, double leftRightCrop, double rightLeftCrop, double rightRightCrop)
        {
            if (leftBitmap == null || rightBitmap == null)
            {
                return leftBitmap?.Width * 2 ?? rightBitmap?.Width * 2 ?? 0;
            }

            var baseWidth = Math.Min(leftBitmap.Width, rightBitmap.Width);
            return (int) (2 * baseWidth -
                          baseWidth * (leftLeftCrop + leftRightCrop + rightLeftCrop + rightRightCrop));
            }

        public static int CalculateCanvasHeightWithEditsNoBorder(SKBitmap leftBitmap, SKBitmap rightBitmap, Edits edits)
        {
            return CalculateCanvasHeightWithEditsNoBorderInternal(leftBitmap, rightBitmap,
                edits.TopCrop, edits.BottomCrop,
                edits.VerticalAlignment);
        }

        private static int CalculateCanvasHeightWithEditsNoBorderInternal(SKBitmap leftBitmap, SKBitmap rightBitmap,
            double topCrop, double bottomCrop, double alignment)
        {
            if (leftBitmap == null || rightBitmap == null)
            {
                return leftBitmap?.Height ?? rightBitmap?.Height ?? 0;
            }

            var baseHeight = Math.Min(leftBitmap.Height, rightBitmap.Height);
            return (int) (baseHeight - baseHeight * (topCrop + bottomCrop + Math.Abs(alignment)));
        }

        public static float CalculateFuseGuideMarginHeight(float baseHeight)
        {
            return CalculateFuseGuideWidth(baseHeight) * FUSE_GUIDE_MARGIN_HEIGHT_RATIO;
        }

        public static float CalculateFuseGuideWidth(float baseHeight)
        {
            return (float)(baseHeight * FUSE_GUIDE_WIDTH_RATIO);
        }
    }
}