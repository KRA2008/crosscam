﻿using System;
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

        private static readonly SKColorFilter CyanAnaglyph = SKColorFilter.CreateColorMatrix(new float[]
        {
            0, 0, 0, 0, 0,
            0, 1, 0, 0, 0,
            0, 0, 1, 0, 0,
            0, 0, 0, 1, 0
        }); 
        private static readonly SKColorFilter RedAnaglyph = SKColorFilter.CreateColorMatrix(new float[]
        {
            1, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 1, 0
        }); 
        private static readonly SKColorFilter CyanGrayAnaglyph = SKColorFilter.CreateColorMatrix(new float[]
        {
               0f,    0f,    0f, 0f, 0f,
            0.21f, 0.72f, 0.07f, 0f, 0f,
            0.21f, 0.72f, 0.07f, 0f, 0f,
               0f,    0f,    0f, 1f, 0f
        });
        private static readonly SKColorFilter RedGrayAnaglyph = SKColorFilter.CreateColorMatrix(new float[]
        {
            0.21f, 0.72f, 0.07f, 0f, 0f,
               0f,    0f,    0f, 0f, 0f,
               0f,    0f,    0f, 0f, 0f,
               0f,    0f,    0f, 1f, 0f
        });

        public static void DrawImagesOnCanvas(SKSurface surface, SKBitmap leftBitmap, SKBitmap rightBitmap,
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
                DrawImagesOnCanvasInternal(surface, rightBitmap, leftBitmap,
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
                    (float)cardboardDownsizeProportion);
            }
            else
            {
                DrawImagesOnCanvasInternal(surface, leftBitmap, rightBitmap,
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
                    (float)cardboardDownsizeProportion);
            }
        }

        private static void DrawImagesOnCanvasInternal(
            SKSurface surface, SKBitmap leftBitmap, SKBitmap rightBitmap,
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
            float cardboardDownsize)
        {
            if (leftBitmap == null && rightBitmap == null) return;

            var canvasWidth = surface.Canvas.DeviceClipBounds.Width;
            var canvasHeight = surface.Canvas.DeviceClipBounds.Height;

            double sideBitmapWidthLessCrop, baseHeight;
            double leftBitmapLeftCrop = 0, leftBitmapRightCrop = 0, rightBitmapLeftCrop = 0, rightBitmapRightCrop = 0;

            if (leftBitmap != null)
            {
                leftBitmapLeftCrop = leftLeftCrop * leftBitmap.Width;
                leftBitmapRightCrop = leftRightCrop * leftBitmap.Width;
            }

            if (rightBitmap != null)
            {
                rightBitmapLeftCrop = rightLeftCrop * rightBitmap.Width;
                rightBitmapRightCrop = rightRightCrop * rightBitmap.Width;
            }

            double bitmapTopCrop, bitmapBottomCrop;
            if (leftBitmap != null)
            {
                bitmapTopCrop = topCrop * leftBitmap.Height;
                bitmapBottomCrop = bottomCrop * leftBitmap.Height;
                baseHeight = leftBitmap.Height;
                sideBitmapWidthLessCrop = leftBitmap.Width - leftBitmapLeftCrop - leftBitmapRightCrop;
            }
            else
            {
                bitmapTopCrop = topCrop * rightBitmap.Height;
                bitmapBottomCrop = bottomCrop * rightBitmap.Height;
                baseHeight = rightBitmap.Height;
                sideBitmapWidthLessCrop = rightBitmap.Width - rightBitmapLeftCrop - rightBitmapRightCrop;
            }

            var sideBitmapHeightLessCrop = baseHeight * (1 - (topCrop + bottomCrop + Math.Abs(alignment)));
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
                (sideBitmapWidthLessCrop + sideBitmapWidthLessCrop * innerBorderThicknessProportion * 1.5) /
                (canvasWidth / 2d);
            if (overlayDrawing)
            {
                widthRatio /= 2d;
            }

            var bitmapHeightWithEditsAndBorder =
                sideBitmapHeightLessCrop + sideBitmapWidthLessCrop * innerBorderThicknessProportion * 2;
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
            var sidePreviewWidthLessCrop = (float) (sideBitmapWidthLessCrop / scalingRatio);
            var previewHeightLessCrop = (float) (sideBitmapHeightLessCrop / scalingRatio);

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
                DrawSide(surface.Canvas, leftBitmap, true, drawMode, leftZoom, isLeftRotated, leftRotation,
                    isLeftKeystoned, leftKeystone, leftBitmapLeftCrop / scalingRatio,
                    leftBitmapRightCrop / scalingRatio, bitmapTopCrop / scalingRatio, bitmapBottomCrop / scalingRatio,
                    cardboardHor, cardboardVert, alignment, leftPreviewX, previewY, sidePreviewWidthLessCrop,
                    previewHeightLessCrop, skFilterQuality);
            }

            if (rightBitmap != null)
            {
                DrawSide(surface.Canvas, rightBitmap, false, drawMode, rightZoom, isRightRotated, rightRotation,
                    isRightKeystoned, rightKeystone, rightBitmapLeftCrop / scalingRatio,
                    rightBitmapRightCrop / scalingRatio, bitmapTopCrop / scalingRatio, bitmapBottomCrop / scalingRatio,
                    cardboardHor, cardboardVert, alignment, rightPreviewX, previewY, sidePreviewWidthLessCrop,
                    previewHeightLessCrop, skFilterQuality);
            }

            var openCv = DependencyService.Get<IOpenCv>();
            if (drawMode == DrawMode.Cardboard &&
                addBarrelDistortion &&
                openCv?.IsOpenCvSupported() == true)
            {
                using var paint = new SKPaint
                {
                    FilterQuality = skFilterQuality
                };

                var sideWidth = surface.Canvas.DeviceClipBounds.Width / 2f;
                var sideHeight = surface.Canvas.DeviceClipBounds.Height;

                using var smallSurface = SKSurface.Create(new SKImageInfo((int) sideWidth, sideHeight));

                if (leftBitmap != null)
                {
                    smallSurface.Canvas.Clear();
                    smallSurface.Canvas.DrawSurface(surface, 0, 0, paint);
                    using var leftSnapshot = smallSurface.Snapshot();
                    using var distortedLeft = openCv.AddBarrelDistortion(SKBitmap.FromImage(leftSnapshot),
                        cardboardDownsize, barrelStrength / 100f, (float)(1 - cardboardWidthProportion), skFilterQuality);

                    surface.Canvas.DrawBitmap(
                        distortedLeft,
                        SKRect.Create(
                            0,
                            0,
                            sideWidth,
                            sideHeight),
                        paint);
                }

                if (rightBitmap != null)
                {
                    smallSurface.Canvas.Clear();
                    smallSurface.Canvas.DrawSurface(surface, -sideWidth, 0, paint);
                    using var rightSnapshot = smallSurface.Snapshot();
                    using var distortedRight = openCv.AddBarrelDistortion(SKBitmap.FromImage(rightSnapshot),
                        cardboardDownsize, barrelStrength / 100f, (float)cardboardWidthProportion, skFilterQuality);

                    surface.Canvas.DrawBitmap(
                        distortedRight,
                        SKRect.Create(
                            sideWidth,
                            0,
                            sideWidth,
                            sideHeight),
                        paint);
                }
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
                surface.Canvas.DrawRect(originX, originY, fullPreviewWidth, scaledBorderThickness, borderPaint);
                surface.Canvas.DrawRect(originX, originY, scaledBorderThickness, fullPreviewHeight, borderPaint);
                surface.Canvas.DrawRect(canvasWidth / 2f - scaledBorderThickness / 2f, originY, scaledBorderThickness, fullPreviewHeight, borderPaint);
                surface.Canvas.DrawRect(endX, originY, scaledBorderThickness, fullPreviewHeight, borderPaint);
                surface.Canvas.DrawRect(originX, endY, fullPreviewWidth, scaledBorderThickness, borderPaint);
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
                surface.Canvas.DrawRect(
                    canvasWidth / 2f - previewBorderThickness - sidePreviewWidthLessCrop / 2f - fuseGuideIconWidth / 2f,
                    fuseGuideY, fuseGuideIconWidth, fuseGuideIconWidth, whitePaint);
                surface.Canvas.DrawRect(
                    canvasWidth / 2f + previewBorderThickness + sidePreviewWidthLessCrop / 2f + fuseGuideIconWidth / 2f,
                    fuseGuideY, fuseGuideIconWidth, fuseGuideIconWidth, whitePaint);
            }
        }

        private static void DrawSide(SKCanvas canvas, SKBitmap bitmap, bool isLeft, DrawMode drawMode, double zoom,
            bool isRotated, float rotation, bool isKeystoned, float keystone, double leftCrop, double rightCrop, double topCrop,
            double bottomCrop, double cardboardHor, double cardboardVert, double alignment, float visiblePreviewX,
            float visiblePreviewY, float visiblePreviewWidth, float visiblePreviewHeight, SKFilterQuality quality)
        {
            var cardboardHorDelta = cardboardHor * visiblePreviewWidth;
            var cardboardVertDelta = cardboardVert * visiblePreviewHeight;

            var fullTransform3D = SKMatrix.Identity;
            if (isRotated ||
                isKeystoned)
            {
                var fullTransform4D = SKMatrix44.CreateIdentity();

                if (isRotated)
                {
                    var xCorrection = (float)(visiblePreviewX + visiblePreviewWidth / 2f - cardboardHorDelta);
                    var yCorrection = (float)(visiblePreviewY + visiblePreviewHeight / 2f - cardboardVertDelta);
                    fullTransform4D.PostConcat(SKMatrix44.CreateTranslate(-xCorrection, -yCorrection, 0));
                    fullTransform4D.PostConcat(SKMatrix44.CreateRotationDegrees(0, 0, 1, rotation));
                    fullTransform4D.PostConcat(SKMatrix44.CreateTranslate(xCorrection, yCorrection, 0));
                }

                if (isKeystoned)
                { //TODO (or TODON'T): the axis of this rotation is fixed, but it could be needed in any direction really, so enable that?
                    var xCorrection = (float) ((isLeft ? visiblePreviewX : visiblePreviewX + visiblePreviewWidth) - cardboardHorDelta);
                    var yCorrection = (float) (visiblePreviewY + visiblePreviewHeight / 2f - cardboardVertDelta);
                    fullTransform4D.PostConcat(SKMatrix44.CreateTranslate(-xCorrection, -yCorrection, 0));
                    fullTransform4D.PostConcat(SKMatrix44.CreateRotationDegrees(0, 1, 0, isLeft ? keystone : -keystone));
                    fullTransform4D.PostConcat(MakePerspective(visiblePreviewWidth));
                    fullTransform4D.PostConcat(SKMatrix44.CreateTranslate(xCorrection, yCorrection, 0));
                }

                fullTransform3D = fullTransform4D.Matrix;
            }

            using var paint = new SKPaint
            {
                FilterQuality = quality
            };

            switch (drawMode)
            {
                case DrawMode.RedCyanAnaglyph:
                    if (isLeft)
                    {
                        paint.ColorFilter = CyanAnaglyph;
                    }
                    else
                    {
                        paint.ColorFilter = RedAnaglyph;
                        paint.BlendMode = SKBlendMode.Plus;
                    }
                    break;
                case DrawMode.GrayscaleRedCyanAnaglyph:
                    if (isLeft)
                    {
                        paint.ColorFilter = CyanGrayAnaglyph;
                    }
                    else
                    {
                        paint.ColorFilter = RedGrayAnaglyph;
                        paint.BlendMode = SKBlendMode.Plus;
                    }
                    break;
            }

            canvas.Save();
            var xClip = (float) Math.Clamp(visiblePreviewX - cardboardHorDelta, visiblePreviewX,
                visiblePreviewX + visiblePreviewWidth);
            var yClip = (float) Math.Clamp(visiblePreviewY - cardboardVertDelta, 0, double.MaxValue);
            float widthClip;
            if (cardboardHorDelta > 0)
            {
                widthClip = (float) (visiblePreviewWidth - cardboardHorDelta);
            }
            else
            {
                widthClip = (float) (visiblePreviewWidth + cardboardHorDelta);
            }
            var heightClip = visiblePreviewHeight;
            canvas.ClipRect(
                SKRect.Create(
                    xClip,
                    yClip,
                    widthClip,
                    heightClip));

            var destWidth = visiblePreviewWidth * (1 + zoom) + rightCrop + leftCrop;
            var destHeight = visiblePreviewHeight * (1 + zoom) + bottomCrop + topCrop +
                             visiblePreviewHeight * Math.Abs(alignment);
            var destX = visiblePreviewX - leftCrop - zoom * visiblePreviewWidth / 2f - cardboardHorDelta;
            var destY = visiblePreviewY - topCrop - zoom * visiblePreviewHeight / 2f + (isLeft
                ? alignment > 0 ? -alignment * visiblePreviewHeight : 0
                : alignment < 0 ? alignment * visiblePreviewHeight : 0)
                - cardboardVertDelta;
            canvas.SetMatrix(fullTransform3D);
            canvas.DrawBitmap(
                bitmap,
                SKRect.Create(
                    (float)destX,
                    (float)destY,
                    (float)destWidth,
                    (float)destHeight),
                paint);
            canvas.ResetMatrix();

            canvas.Restore();
        }

        private static SKMatrix44 MakePerspective(float maxDepth)
        {
            var perspectiveMatrix = SKMatrix44.CreateIdentity();
            perspectiveMatrix[3, 2] = -1 / maxDepth;
            return perspectiveMatrix;
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