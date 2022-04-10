using System;
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

            var useGhost = drawQuality == DrawQuality.Preview &&
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
                    useGhost,
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
                    useGhost,
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
            SKFilterQuality skFilterQuality, bool useGhost, 
            double cardboardWidthProportion,
            double cardboardVert,
            double cardboardHor,
            float cardboardDownsize)
        {
            if (leftBitmap == null && rightBitmap == null) return;

            var canvasWidth = surface.Canvas.DeviceClipBounds.Width;
            var canvasHeight = surface.Canvas.DeviceClipBounds.Height;

            double sideBitmapWidthLessCrop, baseHeight, baseWidth;

            if (leftBitmap != null)
            {
                baseHeight = leftBitmap.Height;
                sideBitmapWidthLessCrop = leftBitmap.Width * (1 - (leftLeftCrop + leftRightCrop));
                baseWidth = leftBitmap.Width;
            }
            else
            {
                baseHeight = rightBitmap.Height;
                sideBitmapWidthLessCrop = rightBitmap.Width * (1 - (rightLeftCrop + rightRightCrop));
                baseWidth = rightBitmap.Width;
            }

            var sideBitmapHeightLessCrop = baseHeight * (1 - (topCrop + bottomCrop + Math.Abs(alignment)));
            var overlayDrawing =
                drawMode == DrawMode.GrayscaleRedCyanAnaglyph ||
                drawMode == DrawMode.RedCyanAnaglyph ||
                useGhost;
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

            var heightRatio = bitmapHeightWithEditsAndBorder / (1d * canvasHeight);

            var fillsWidth = widthRatio > heightRatio;

            var scalingRatio = fillsWidth ? widthRatio : heightRatio;

            fuseGuideIconWidth = (float) (fuseGuideIconWidth / scalingRatio);
            fuseGuideMarginHeight = (float) (fuseGuideMarginHeight / scalingRatio);

            var clipWidth = (float) (sideBitmapWidthLessCrop / scalingRatio);
            var clipHeight = (float) (sideBitmapHeightLessCrop / scalingRatio);

            float leftClipX, rightClipX, clipY;
            float leftDestX, rightDestX, destY, destWidth, destHeight;
            if (overlayDrawing)
            {
                leftClipX = rightClipX = canvasWidth / 2f - clipWidth / 2f;
                clipY = canvasHeight / 2f - clipHeight / 2f;
            }
            else
            {
                leftClipX = (float) (canvasWidth / 2f - 
                                     (clipWidth + innerBorderThicknessProportion * clipWidth / 2f));
                rightClipX = (float) (canvasWidth / 2f + 
                                      innerBorderThicknessProportion * clipWidth / 2f);
                clipY = canvasHeight / 2f - clipHeight / 2f;
            }

            leftDestX = (float)(leftClipX - baseWidth / scalingRatio * leftLeftCrop);
            rightDestX = (float)(rightClipX - baseWidth / scalingRatio * rightLeftCrop);
            destY = (float)(clipY - baseHeight / scalingRatio * topCrop);
            destWidth = (float)(baseWidth / scalingRatio);
            destHeight = (float)(baseHeight / scalingRatio);

            if (drawFuseGuide)
            {
                clipY += fuseGuideMarginHeight / 2f;
            }

            var isRightRotated = Math.Abs(rightRotation) > FLOATY_ZERO;
            var isLeftRotated = Math.Abs(leftRotation) > FLOATY_ZERO;
            var isRightKeystoned = Math.Abs(rightKeystone) > FLOATY_ZERO;
            var isLeftKeystoned = Math.Abs(leftKeystone) > FLOATY_ZERO;

            if (leftBitmap != null)
            {
                DrawSide(surface.Canvas, leftBitmap, true, drawMode, leftZoom,
                    isLeftRotated, leftRotation, isLeftKeystoned, leftKeystone,
                    leftLeftCrop, leftRightCrop, topCrop, bottomCrop,
                    cardboardHor, cardboardVert, alignment,
                    leftClipX, clipY, clipWidth, clipHeight,
                    leftDestX, destY, destWidth, destHeight,
                    (float)scalingRatio,
                    false, skFilterQuality);
            }

            if (rightBitmap != null)
            {
                DrawSide(surface.Canvas, rightBitmap, false, drawMode, rightZoom,
                    isRightRotated, rightRotation, isRightKeystoned, rightKeystone,
                    rightLeftCrop, rightRightCrop, topCrop, bottomCrop,
                    cardboardHor, cardboardVert, alignment,
                    rightClipX, clipY, clipWidth, clipHeight,
                    rightDestX, destY, destWidth, destHeight,
                    (float)scalingRatio,
                    leftBitmap != null && useGhost, skFilterQuality);
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

                var originX = (float)(leftClipX - innerBorderThicknessProportion * clipWidth);
                var originY = (float)(clipY - innerBorderThicknessProportion * clipWidth);
                var fullPreviewWidth = (float)(2 * clipWidth + 3 * innerBorderThicknessProportion * clipWidth);
                var fullPreviewHeight = (float)(clipHeight + 2 * innerBorderThicknessProportion * clipWidth);
                var scaledBorderThickness = (float)(innerBorderThicknessProportion * clipWidth);
                var endX = rightClipX + clipWidth;
                var endY = clipY + clipHeight;
                surface.Canvas.DrawRect(originX, originY, fullPreviewWidth, scaledBorderThickness, borderPaint);
                surface.Canvas.DrawRect(originX, originY, scaledBorderThickness, fullPreviewHeight, borderPaint);
                surface.Canvas.DrawRect(canvasWidth / 2f - scaledBorderThickness / 2f, originY, scaledBorderThickness, fullPreviewHeight, borderPaint);
                surface.Canvas.DrawRect(endX, originY, scaledBorderThickness, fullPreviewHeight, borderPaint);
                surface.Canvas.DrawRect(originX, endY, fullPreviewWidth, scaledBorderThickness, borderPaint);
            }

            if (drawFuseGuide)
            {
                var previewBorderThickness = canvasWidth / 2f - (leftClipX + clipWidth);
                var fuseGuideY = clipY - 2 * previewBorderThickness - fuseGuideMarginHeight / 2f; //why 2x border? why doesn't this have to account for icon height? i don't know.
                using var whitePaint = new SKPaint
                {
                    Color = new SKColor(byte.MaxValue, byte.MaxValue, byte.MaxValue),
                    FilterQuality = skFilterQuality
                };
                surface.Canvas.DrawRect(
                    canvasWidth / 2f - previewBorderThickness - clipWidth / 2f - fuseGuideIconWidth / 2f,
                    fuseGuideY, fuseGuideIconWidth, fuseGuideIconWidth, whitePaint);
                surface.Canvas.DrawRect(
                    canvasWidth / 2f + previewBorderThickness + clipWidth / 2f + fuseGuideIconWidth / 2f,
                    fuseGuideY, fuseGuideIconWidth, fuseGuideIconWidth, whitePaint);
            }
        }

        private static void DrawSide(SKCanvas canvas, SKBitmap bitmap, bool isLeft, DrawMode drawMode, double zoom,
            bool isRotated, float rotation, bool isKeystoned, float keystone,
            double leftCrop, double rightCrop, double topCrop, double bottomCrop,
            double cardboardHor, double cardboardVert, double alignment,
            float clipX, float clipY, float clipWidth, float clipHeight,
            float destX, float destY, float destWidth, float destHeight,
            float scalingRatio,
            bool useGhostOverlay, SKFilterQuality quality)
        {
            // TODO: test "whole for overlay, plus mod for border and fuse guide (height)"
            var cardboardHorDelta = cardboardHor * bitmap.Width; //TODO: are these appropriate here? or should be added after transforms?
            var cardboardVertDelta = cardboardVert * bitmap.Height; //TODO: DON'T JUDGE CORRECTNESS OF EDITS UNTIL CROPPING IS GOOD AND THE ABOVE CAN BE TESTED

            var fullTransform4D = SKMatrix44.CreateIdentity();

            if (isRotated)
            {
                var xCorrection = (float) (destX + destWidth / 2f - cardboardHorDelta);
                var yCorrection = (float) (destY + destHeight / 2f - cardboardVertDelta);
                fullTransform4D.PostConcat(SKMatrix44.CreateTranslate(-xCorrection, -yCorrection, 0));
                fullTransform4D.PostConcat(SKMatrix44.CreateRotationDegrees(0, 0, 1, rotation));
                fullTransform4D.PostConcat(SKMatrix44.CreateTranslate(xCorrection, yCorrection, 0));
            }

            if (isKeystoned)
            {
                // TODO cropping sides a lot makes the keystoning act weird
                // TODO looking far left and right in immersive cardboard like crops the inside edge???
                // TODO (or TODON'T): the axis of this rotation is fixed, but it could be needed in any direction really, so enable that?
                var isKeystoneSwapped = drawMode == DrawMode.Parallel || drawMode == DrawMode.Cardboard;
                var xCorrection =
                    (float) ((isLeft && !isKeystoneSwapped || !isLeft && isKeystoneSwapped
                        ? destX
                        : destX + destWidth) + cardboardHorDelta);
                var yCorrection = (float) (destY + destHeight / 2f - cardboardVertDelta);
                fullTransform4D.PostConcat(SKMatrix44.CreateTranslate(-xCorrection, -yCorrection, 0));
                fullTransform4D.PostConcat(SKMatrix44.CreateRotationDegrees(0, 1, 0,
                    isLeft && !isKeystoneSwapped || !isLeft && isKeystoneSwapped ? keystone : -keystone));
                fullTransform4D.PostConcat(MakePerspective(destWidth));
                fullTransform4D.PostConcat(SKMatrix44.CreateTranslate(xCorrection, yCorrection, 0));
                
            }

            if (zoom != 0)
            {
                var xCorrection = (float) (destX + destWidth / 2f - cardboardHorDelta);
                var yCorrection = (float) (destY + destHeight / 2f - cardboardVertDelta);
                fullTransform4D.PostConcat(SKMatrix44.CreateTranslate(-xCorrection, -yCorrection, 0));
                fullTransform4D.PostConcat(SKMatrix44.CreateScale((float) (1 + zoom), (float) (1 + zoom), 0));
                fullTransform4D.PostConcat(SKMatrix44.CreateTranslate(xCorrection, yCorrection, 0));
            }

            if (alignment != 0)
            {
                var yCorrection = isLeft
                    ? alignment > 0 ? -alignment * destHeight : 0
                    : alignment < 0 ? alignment * destHeight : 0;
                fullTransform4D.PostConcat(SKMatrix44.CreateTranslate(0, (float) yCorrection, 0));
            }

            if (cardboardHorDelta != 0 ||
                cardboardVertDelta != 0)
            {
                fullTransform4D.PostConcat(SKMatrix44.CreateTranslate((float) -cardboardHorDelta, (float) -cardboardVertDelta, 0));
            }

            var fullTransform3D = fullTransform4D.Matrix;

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

            if (useGhostOverlay)
            {
                paint.Color = paint.Color.WithAlpha((byte)(0xFF * 0.5f));
            }

            canvas.Save();
            canvas.ClipRect(
                SKRect.Create(
                    (float) (clipX - cardboardHorDelta), //TODO: add a ceiling
                    (float) (clipY - cardboardVertDelta),
                    clipWidth,
                    clipHeight));

            canvas.SetMatrix(fullTransform3D);
            canvas.DrawBitmap(
                bitmap,
                SKRect.Create(
                    destX,
                    destY,
                    destWidth,
                    destHeight),
                paint);
            canvas.ResetMatrix();

            canvas.Restore();

            //canvas.DrawRect(clipX, clipY, clipWidth, clipHeight, new SKPaint
            //{
            //    Color = new SKColor(isLeft ? byte.MaxValue : (byte)0, byte.MaxValue, 0, byte.MaxValue / 3)
            //});
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