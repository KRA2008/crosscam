using System;
using System.Diagnostics;
using System.Linq;
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
        private const double FUSE_GUIDE_WIDTH_RATIO = 0.0127;
        private const int FUSE_GUIDE_MARGIN_HEIGHT_RATIO = 7;

        private static readonly SKColorFilter CyanAnaglyph = SKColorFilter.CreateColorMatrix(new[]
        {
            0f, 0, 0, 0, 0,
             0, 1, 0, 0, 0,
             0, 0, 1, 0, 0,
             0, 0, 0, 1, 0
        }); 
        private static readonly SKColorFilter RedAnaglyph = SKColorFilter.CreateColorMatrix(new[]
        {
            1f, 0, 0, 0, 0,
             0, 0, 0, 0, 0,
             0, 0, 0, 0, 0,
             0, 0, 0, 1, 0
        }); 
        private static readonly SKColorFilter CyanGrayAnaglyph = SKColorFilter.CreateColorMatrix(new[]
        {
                0,     0,     0, 0, 0,
            0.21f, 0.72f, 0.07f, 0, 0,
            0.21f, 0.72f, 0.07f, 0, 0,
                0,     0,     0, 1, 0
        });
        private static readonly SKColorFilter RedGrayAnaglyph = SKColorFilter.CreateColorMatrix(new[]
        {
            0.21f, 0.72f, 0.07f, 0, 0,
                0,     0,     0, 0, 0,
                0,     0,     0, 0, 0,
                0,     0,     0, 1, 0
        });
        private static readonly SKEncodedOrigin[] Orientations90deg = new[]
        {
            SKEncodedOrigin.RightTop,
            SKEncodedOrigin.LeftBottom,
            SKEncodedOrigin.RightBottom,
            SKEncodedOrigin.LeftTop
        };

        public static void DrawImagesOnCanvas(SKSurface surface, 
            SKBitmap leftBitmap, SKMatrix leftAlignmentMatrix, SKEncodedOrigin leftOrientation, bool isLeftFrontFacing,
            SKBitmap rightBitmap, SKMatrix rightAlignmentMatrix, SKEncodedOrigin rightOrientation, bool isRightFrontFacing,
            Settings settings, Edits edits, DrawMode drawMode, bool isFov = false, bool withSwap = false,
            DrawQuality drawQuality = DrawQuality.Save, double cardboardVert = 0, double cardboardHor = 0)
        {
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
                                              settings.CardboardDownsize ? settings.CardboardDownsizePercentage / 100d : 1d;
            double vert = 0, hor = 0;
            if (settings.ImmersiveCardboardFinal && 
                settings.Mode == DrawMode.Cardboard)
            {
                vert = cardboardVert;
                hor = cardboardHor;
            }

            if (withSwap)
            {
                DrawImagesOnCanvasInternal(surface, 
                    rightBitmap, rightAlignmentMatrix, rightOrientation, isRightFrontFacing,
                    leftBitmap, leftAlignmentMatrix, leftOrientation, isLeftFrontFacing,
                    settings.BorderWidthProportion, settings.AddBorder && drawQuality != DrawQuality.Preview, settings.BorderColor,
                    edits.InsideCrop + edits.LeftCrop, edits.RightCrop + edits.OutsideCrop,
                    edits.LeftCrop + edits.OutsideCrop, edits.InsideCrop + edits.RightCrop,
                    edits.TopCrop, edits.BottomCrop,
                    edits.RightRotation, edits.LeftRotation,
                    edits.VerticalAlignment,
                    edits.RightZoom + (isFov ? edits.FovRightCorrection : 0), edits.LeftZoom + (isFov ? edits.FovLeftCorrection : 0),
                    edits.RightKeystone, edits.LeftKeystone,
                    drawMode, fuseGuideRequested,
                    addBarrelDistortion, settings.CardboardBarrelDistortion,
                    skFilterQuality,
                    useGhost,
                    cardboardWidthProportion, vert, hor,
                    (float)cardboardDownsizeProportion,
                    settings.CardboardIpd);
            }
            else
            {
                DrawImagesOnCanvasInternal(surface, 
                    leftBitmap, leftAlignmentMatrix, leftOrientation, isLeftFrontFacing,
                    rightBitmap, rightAlignmentMatrix, rightOrientation, isRightFrontFacing,
                    settings.BorderWidthProportion, settings.AddBorder && drawQuality != DrawQuality.Preview, settings.BorderColor,
                    edits.LeftCrop + edits.OutsideCrop, edits.InsideCrop + edits.RightCrop, edits.InsideCrop + edits.LeftCrop,
                    edits.RightCrop + edits.OutsideCrop,
                    edits.TopCrop, edits.BottomCrop,
                    edits.LeftRotation, edits.RightRotation,
                    edits.VerticalAlignment,
                    edits.LeftZoom + (isFov ? edits.FovLeftCorrection : 0), edits.RightZoom + (isFov ? edits.FovRightCorrection : 0),
                    edits.LeftKeystone, edits.RightKeystone,
                    drawMode, fuseGuideRequested,
                    addBarrelDistortion, settings.CardboardBarrelDistortion,
                    skFilterQuality,
                    useGhost,
                    cardboardWidthProportion, vert, hor,
                    (float)cardboardDownsizeProportion,
                    settings.CardboardIpd);
            }
        }

        private static void DrawImagesOnCanvasInternal(
            SKSurface surface, 
            SKBitmap leftBitmap, SKMatrix leftAlignmentMatrix, SKEncodedOrigin leftOrientation, bool isLeftFrontFacing,
            SKBitmap rightBitmap, SKMatrix rightAlignmentMatrix, SKEncodedOrigin rightOrientation, bool isRightFrontFacing,
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
            float cardboardDownsize,
            int cardboardIpd)
        {
            if (leftBitmap == null && rightBitmap == null) return;

            var canvasWidth = surface.Canvas.DeviceClipBounds.Width;
            var canvasHeight = surface.Canvas.DeviceClipBounds.Height;

            double sideBitmapWidthLessCrop, baseHeight, baseWidth, netSideCrop;

            var isLeft90Oriented = Orientations90deg.Contains(leftOrientation);
            var isRight90Oriented = Orientations90deg.Contains(rightOrientation);
            if (leftBitmap != null)
            {
                if (isLeft90Oriented)
                {
                    baseWidth = leftBitmap.Height;
                    baseHeight = leftBitmap.Width;
                }
                else
                {
                    baseWidth = leftBitmap.Width;
                    baseHeight = leftBitmap.Height;
                }
                netSideCrop = leftLeftCrop + leftRightCrop;
                sideBitmapWidthLessCrop = baseWidth * (1 - netSideCrop);
            }
            else
            {
                if (isRight90Oriented)
                {
                    baseHeight = rightBitmap.Width;
                    baseWidth = rightBitmap.Height;
                }
                else
                {
                    baseHeight = rightBitmap.Height;
                    baseWidth = rightBitmap.Width;
                }
                netSideCrop = rightLeftCrop + rightRightCrop;
                sideBitmapWidthLessCrop = baseWidth * (1 - netSideCrop);
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

            var leftDestX = (float)(leftClipX - baseWidth / scalingRatio * leftLeftCrop);
            var rightDestX = (float)(rightClipX - baseWidth / scalingRatio * rightLeftCrop);
            var destY = (float)(clipY - baseHeight / scalingRatio * topCrop);
            var destWidth = (float)(baseWidth / scalingRatio);
            var destHeight = (float)(baseHeight / scalingRatio);

            if (drawFuseGuide)
            {
                destY += fuseGuideMarginHeight / 2f;
                clipY += fuseGuideMarginHeight / 2f;
            }

            var cardboardSeparationMod = 0d;
            if (drawMode == DrawMode.Cardboard)
            {
                var displayLandscapeSideWidth = (rightClipX - leftClipX) / DeviceDisplay.MainDisplayInfo.Density;
                cardboardSeparationMod = (cardboardIpd - displayLandscapeSideWidth) * DeviceDisplay.MainDisplayInfo.Density / 2d;
            }

            var cardboardHorDelta = cardboardHor * destWidth;
            var cardboardVertDelta = cardboardVert * destHeight; //TODO: use same property for both to make move speed the same?

            var leftXCorrectionToOrigin = leftDestX + destWidth /2f;
            var leftYCorrectionToOrigin = destY + destWidth / 2f;
            var rightXCorrectionToOrigin = rightDestX + destWidth / 2f;
            var rightYCorrectionToOrigin = destY + destWidth / 2f;
            var leftIntermediateWidth = destWidth;
            var leftIntermediateHeight = destHeight;
            var rightIntermediateWidth = destWidth;
            var rightIntermediateHeight = destHeight;

            if (isLeft90Oriented)
            {
                (leftXCorrectionToOrigin, leftYCorrectionToOrigin, leftIntermediateWidth, leftIntermediateHeight) = 
                    (leftYCorrectionToOrigin, leftXCorrectionToOrigin, leftIntermediateHeight, leftIntermediateWidth);
            }

            if (isRight90Oriented)
            {
                (rightXCorrectionToOrigin, rightYCorrectionToOrigin, rightIntermediateWidth, rightIntermediateHeight) = 
                    (rightYCorrectionToOrigin, rightXCorrectionToOrigin, rightIntermediateHeight, rightIntermediateWidth);
            }

            if (leftBitmap != null)
            {
                var leftOrientationMatrix = FindOrientationMatrix(leftOrientation, leftXCorrectionToOrigin,
                    leftYCorrectionToOrigin, isLeftFrontFacing);
                var leftScaledAlignmentMatrix = FindScaledAlignmentMatrix(
                    leftIntermediateWidth, leftIntermediateHeight, leftXCorrectionToOrigin, leftYCorrectionToOrigin,
                    leftAlignmentMatrix, scalingRatio);
                var leftEditMatrix = FindEditMatrix(true, drawMode, leftZoom, leftRotation, leftKeystone,
                    cardboardHorDelta, cardboardVertDelta, alignment,
                    leftDestX, destY, destWidth, destHeight,
                    -cardboardSeparationMod);
                DrawSide(surface.Canvas, leftBitmap, true, drawMode, 
                    cardboardHorDelta, cardboardVertDelta,
                    leftClipX, clipY, clipWidth, clipHeight,
                    leftDestX, destY, destWidth, destHeight,
                    false, -cardboardSeparationMod,
                    leftScaledAlignmentMatrix, leftOrientationMatrix, leftEditMatrix, 
                    skFilterQuality);
            }

            if (rightBitmap != null)
            {
                var rightOrientationMatrix = FindOrientationMatrix(rightOrientation, rightXCorrectionToOrigin,
                    rightYCorrectionToOrigin, isRightFrontFacing);
                var rightScaledAlignmentMatrix = FindScaledAlignmentMatrix(
                    rightIntermediateWidth, rightIntermediateHeight, rightXCorrectionToOrigin, rightYCorrectionToOrigin,
                    rightAlignmentMatrix, scalingRatio);
                var rightEditMatrix = FindEditMatrix(false, drawMode, rightZoom, rightRotation, rightKeystone,
                    cardboardHorDelta, cardboardVertDelta, alignment,
                    rightDestX, destY, destWidth, destHeight,
                    cardboardSeparationMod);
                DrawSide(surface.Canvas, rightBitmap, false, drawMode,
                    cardboardHorDelta, cardboardVertDelta,
                    rightClipX, clipY, clipWidth, clipHeight,
                    rightDestX, destY, destWidth, destHeight,
                    leftBitmap != null && useGhost, cardboardSeparationMod,
                    rightScaledAlignmentMatrix, rightOrientationMatrix, rightEditMatrix, 
                    skFilterQuality);
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

        private static SKMatrix FindScaledAlignmentMatrix(float destWidth, float destHeight,
            float xCorrectionToOrigin, float yCorrectionToOrigin,
            SKMatrix originalAlignment, double scalingRatio)
        {
            var transform3D = SKMatrix.Identity;

            if (!originalAlignment.IsIdentity)
            {
                transform3D = transform3D.PostConcat(SKMatrix.CreateTranslation(-xCorrectionToOrigin, -yCorrectionToOrigin));
                transform3D = transform3D.PostConcat(SKMatrix.CreateScale((float)scalingRatio, (float)scalingRatio));
                transform3D = transform3D.PostConcat(SKMatrix.CreateTranslation((float)(destWidth * scalingRatio / 2f), (float)(destHeight * scalingRatio / 2f)));
                transform3D = transform3D.PostConcat(originalAlignment);
                transform3D = transform3D.PostConcat(SKMatrix.CreateTranslation((float)(-destWidth * scalingRatio / 2f), (float)(-destHeight * scalingRatio / 2f)));
                transform3D = transform3D.PostConcat(SKMatrix.CreateScale((float)(1 / scalingRatio), (float)(1 / scalingRatio)));
                transform3D = transform3D.PostConcat(SKMatrix.CreateTranslation(xCorrectionToOrigin, yCorrectionToOrigin));
            }

            return transform3D;
        }

        private static SKMatrix FindOrientationMatrix(SKEncodedOrigin orientation,
            float xCorrectionToOrigin, float yCorrectionToOrigin, bool isFrontFacing)
        {
            float orientationRotation;
            bool needsMirror;

            //positive rotation is clockwise for back facing
            //positive rotation is counterclockwise for front facing
            //forward facing adds 180 and a mirror
            //see https://www.nosco.ch/blog/en/2020/10/photo-orientation
            switch (orientation)
            {
                case 0:
                    orientationRotation = 0;
                    needsMirror = false;
                    break;
                case SKEncodedOrigin.TopLeft: //confirmed iOS(both), Android(both)
                    orientationRotation = (float)(isFrontFacing ? Math.PI : 0);
                    needsMirror = isFrontFacing;
                    break;
                case SKEncodedOrigin.LeftBottom: //confirmed Android(both)
                    orientationRotation = (float)(-Math.PI / 2f);
                    needsMirror = isFrontFacing;
                    break;
                case SKEncodedOrigin.BottomRight: //confirmed iOS(both), Android(both)
                    orientationRotation = (float)(Math.PI - (isFrontFacing ? Math.PI : 0));
                    needsMirror = isFrontFacing;
                    break;
                case SKEncodedOrigin.RightTop: //confirmed iOS(both), Android(both)
                    orientationRotation = (float)(Math.PI / 2f);
                    needsMirror = isFrontFacing;
                    break;
                case SKEncodedOrigin.TopRight:
                    orientationRotation = (float)(isFrontFacing ? Math.PI : 0);
                    needsMirror = !isFrontFacing;
                    break;
                case SKEncodedOrigin.RightBottom:
                    orientationRotation = (float)(-Math.PI / 2f);
                    needsMirror = !isFrontFacing;
                    break;
                case SKEncodedOrigin.BottomLeft:
                    orientationRotation = (float)(Math.PI - (isFrontFacing ? Math.PI : 0));
                    needsMirror = !isFrontFacing;
                    break;
                case SKEncodedOrigin.LeftTop:
                    orientationRotation = (float)(Math.PI / 2f);
                    needsMirror = !isFrontFacing;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(orientation), orientation, null);
            }

            var transform3D = SKMatrix.Identity;
            transform3D = transform3D.PostConcat(SKMatrix.CreateTranslation(-xCorrectionToOrigin, -yCorrectionToOrigin));
            transform3D = transform3D.PostConcat(SKMatrix.CreateRotation(orientationRotation));
            if (needsMirror)
            {
                transform3D = transform3D.PostConcat(SKMatrix.CreateScale(-1, 1));
            }
            transform3D = transform3D.PostConcat(SKMatrix.CreateTranslation(xCorrectionToOrigin, yCorrectionToOrigin));

            return transform3D;
        }

        private static SKMatrix FindEditMatrix(bool isLeft, DrawMode drawMode,
            double zoom, float rotation, float keystone,
            double cardboardHorDelta, double cardboardVertDelta, double alignment,
            float destX, float destY, float destWidth, float destHeight,
            double cardboardSeparationMod)
        {
            var xCorrectionToOrigin = destX + destWidth / 2f;
            var yCorrectionToOrigin = destY + destHeight / 2f;

            using var transform4D = SKMatrix44.CreateIdentity();

            if (Math.Abs(rotation) > 0)
            {
                transform4D.PostConcat(SKMatrix44.CreateTranslate(-xCorrectionToOrigin, -yCorrectionToOrigin, 0));
                transform4D.PostConcat(SKMatrix44.CreateRotationDegrees(0, 0, 1, rotation));
                transform4D.PostConcat(SKMatrix44.CreateTranslate(xCorrectionToOrigin, yCorrectionToOrigin, 0));
            }

            if (Math.Abs(keystone) > 0)
            {
                // TODO (or TODON'T): the axis of this rotation is fixed, but it could be needed in any direction really, so enable that?
                var isKeystoneSwapped = drawMode == DrawMode.Parallel || drawMode == DrawMode.Cardboard;
                var xCorrection =
                    isLeft && !isKeystoneSwapped || !isLeft && isKeystoneSwapped
                        ? destX
                        : destX + destWidth;
                transform4D.PostConcat(SKMatrix44.CreateTranslate(-xCorrection, -yCorrectionToOrigin, 0));
                transform4D.PostConcat(SKMatrix44.CreateRotationDegrees(0, 1, 0,
                    isLeft && !isKeystoneSwapped || !isLeft && isKeystoneSwapped ? keystone : -keystone));
                transform4D.PostConcat(MakePerspective(destWidth));
                transform4D.PostConcat(SKMatrix44.CreateTranslate(xCorrection, yCorrectionToOrigin, 0));
                
            }

            if (Math.Abs(zoom) > 0)
            {
                transform4D.PostConcat(SKMatrix44.CreateTranslate(-xCorrectionToOrigin, -yCorrectionToOrigin, 0));
                transform4D.PostConcat(SKMatrix44.CreateScale((float) (1 + zoom), (float) (1 + zoom), 0));
                transform4D.PostConcat(SKMatrix44.CreateTranslate(xCorrectionToOrigin, yCorrectionToOrigin, 0));
            }

            if (Math.Abs(alignment) > 0)
            {
                var yCorrection = isLeft
                    ? alignment > 0 ? -alignment * destHeight : 0
                    : alignment < 0 ? alignment * destHeight : 0;
                transform4D.PostConcat(SKMatrix44.CreateTranslate(0, (float) yCorrection, 0));
            }

            if (Math.Abs(cardboardHorDelta) > 0 ||
                Math.Abs(cardboardVertDelta) > 0 ||
                Math.Abs(cardboardSeparationMod) > 0)
            {
                transform4D.PostConcat(SKMatrix44.CreateTranslate((float)(-cardboardHorDelta + cardboardSeparationMod), (float)-cardboardVertDelta, 0));
            }

            return transform4D.Matrix;
        }

        private static void DrawSide(SKCanvas canvas, SKBitmap bitmap,
            bool isLeft, DrawMode drawMode,
            double cardboardHorDelta, double cardboardVertDelta,
            float clipX, float clipY, float clipWidth, float clipHeight,
            float destX, float destY, float destWidth, float destHeight,
            bool useGhostOverlay, double cardboardSeparationMod,
            SKMatrix alignmentMatrix, SKMatrix orientationMatrix, SKMatrix editMatrix,
            SKFilterQuality quality)
        {
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
            
            var adjClipX = Math.Max(clipX - cardboardHorDelta + cardboardSeparationMod, clipX);
            var adjClipWidth = clipWidth - Math.Abs(cardboardHorDelta - cardboardSeparationMod); //TODO: due to some other stuff, the left and right ends never go further than the clip width from the middle (fix it?)
            var adjClipY = clipY - cardboardVertDelta;

            canvas.ClipRect(
                SKRect.Create(
                    (float)adjClipX,
                    (float)adjClipY,
                    (float)adjClipWidth,
                    clipHeight));

            var destinationRect = SKRect.Create(
                destX,
                destY,
                destWidth,
                destHeight);
            
            var correctedRect = orientationMatrix.Invert().MapRect(destinationRect);

            canvas.SetMatrix(alignmentMatrix.PostConcat(orientationMatrix).PostConcat(editMatrix));
            canvas.DrawBitmap(
                bitmap,
                correctedRect,
                paint);
            canvas.ResetMatrix();

            canvas.Restore();

            //canvas.DrawRect(transformedRect, new SKPaint
            //{
            //    Color = new SKColor(isLeft ? byte.MaxValue : (byte)0, byte.MaxValue, 0, byte.MaxValue / 3)
            //});

            //canvas.DrawRect((float) adjClipX, (float) adjClipY, (float) adjClipWidth, clipHeight, new SKPaint
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