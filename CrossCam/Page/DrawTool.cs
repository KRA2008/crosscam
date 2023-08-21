using System;
using System.Drawing;
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
        public const float BORDER_CONVERSION_FACTOR = 0.001f;
        private const float FUSE_GUIDE_WIDTH_RATIO = 0.0127f;
        private const int FUSE_GUIDE_MARGIN_HEIGHT_RATIO = 5;

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
        public static readonly SKEncodedOrigin[] Orientations90deg = 
        {
            SKEncodedOrigin.RightTop,
            SKEncodedOrigin.LeftBottom,
            SKEncodedOrigin.RightBottom,
            SKEncodedOrigin.LeftTop
        };

        public static void DrawImagesOnCanvas(SKSurface surface,
            SKBitmap leftBitmap, SKMatrix leftAlignmentMatrix, 
            SKBitmap rightBitmap, SKMatrix rightAlignmentMatrix,
            Settings settings, Edits edits, DrawMode drawMode, bool wasPairedCapture, 
            bool isLeftFrontFacing = false, SKEncodedOrigin leftOrientation = SKEncodedOrigin.Default, 
            bool isRightFrontFacing = false, SKEncodedOrigin rightOrientation = SKEncodedOrigin.Default, bool withSwap = false,
            DrawQuality drawQuality = DrawQuality.Save, float cardboardVert = 0, float cardboardHor = 0, bool isFovStage = false,
            bool useFullscreen = false, bool useMirrorCapture = false)
        {
            var fuseGuideRequested = drawQuality != DrawQuality.Preview && 
                                     settings.SaveWithFuseGuide ||
                                     drawQuality == DrawQuality.Preview &&
                                     settings.ShowPreviewFuseGuide;

            var addBarrelDistortion =
                settings.CardboardSettings.AddBarrelDistortion && settings.CardboardSettings.AddBarrelDistortionFinalOnly && drawQuality != DrawQuality.Preview ||
                settings.CardboardSettings.AddBarrelDistortion && !settings.CardboardSettings.AddBarrelDistortionFinalOnly;

            if (drawMode == DrawMode.Cardboard &&
                useFullscreen &&
                drawQuality == DrawQuality.Review)
            {
                drawMode = DrawMode.Parallel;
                cardboardHor = 0;
                cardboardVert = 0;
            }

            float cardboardWidthProportion = 0;
            if (drawMode == DrawMode.Cardboard)
            {
                cardboardWidthProportion = (float) (settings.CardboardSettings.CardboardIpd /
                                                    (Math.Max(DeviceDisplay.MainDisplayInfo.Width,
                                                         DeviceDisplay.MainDisplayInfo.Height) /
                                                     DeviceDisplay.MainDisplayInfo.Density / 2f) / 2f);
            }

            var cardboardDownsizeProportion = drawQuality != DrawQuality.Save &&
                                              drawMode == DrawMode.Cardboard &&
                                              settings.CardboardSettings.CardboardDownsize ? settings.CardboardSettings.CardboardDownsizePercentage / 100f : 1f;
            float vert = 0, hor = 0;
            if (settings.CardboardSettings.ImmersiveCardboardFinal && 
                settings.Mode == DrawMode.Cardboard)
            {
                vert = cardboardVert;
                hor = cardboardHor;
            }

            bool shouldMirrorLeftParallel = false, 
                shouldMirrorRightParallel = false, 
                shouldMirrorLeftDefault = false, 
                shouldMirrorRightDefault = false;

            if (useMirrorCapture)
            {
                switch (drawMode)
                {
                    case DrawMode.Cardboard:
                    case DrawMode.Parallel:
                        shouldMirrorLeftParallel = !settings.IsCaptureLeftFirst;
                        shouldMirrorRightParallel = settings.IsCaptureLeftFirst;
                        break;
                    default:
                        shouldMirrorLeftDefault = !settings.IsCaptureLeftFirst;
                        shouldMirrorRightDefault = settings.IsCaptureLeftFirst;
                        break;
                }
            }

            if (withSwap)
            {
                DrawImagesOnCanvasInternal(surface, 
                    rightBitmap, rightAlignmentMatrix, rightOrientation, isRightFrontFacing, false,
                    leftBitmap, leftAlignmentMatrix, leftOrientation, isLeftFrontFacing, false,
                    settings.BorderWidthProportion, settings.AddBorder2 && drawQuality != DrawQuality.Preview, settings.BorderColor,
                    edits.InsideCrop + edits.LeftCrop,
                    edits.LeftCrop + edits.OutsideCrop,
                    edits.TopCrop,
                    edits.RightRotation, edits.LeftRotation,
                    -edits.VerticalAlignment,
                    edits.RightZoom, edits.LeftZoom,
                    wasPairedCapture && drawQuality == DrawQuality.Preview || isFovStage ? edits.FovRightCorrection : 0,
                    wasPairedCapture && drawQuality == DrawQuality.Preview || isFovStage ? edits.FovLeftCorrection : 0,
                    edits.Keystone,
                    drawMode, fuseGuideRequested,
                    addBarrelDistortion, settings.CardboardSettings.CardboardBarrelDistortion,
                    drawQuality,
                    useFullscreen,
                    cardboardWidthProportion, vert, hor,
                    cardboardDownsizeProportion,
                    settings.CardboardSettings.CardboardIpd,
                    edits);
            }
            else
            {
                DrawImagesOnCanvasInternal(surface, 
                    leftBitmap, leftAlignmentMatrix, leftOrientation, isLeftFrontFacing, shouldMirrorLeftDefault || shouldMirrorLeftParallel,
                    rightBitmap, rightAlignmentMatrix, rightOrientation, isRightFrontFacing, shouldMirrorRightDefault || shouldMirrorRightParallel,
                    settings.BorderWidthProportion, settings.AddBorder2 && drawQuality != DrawQuality.Preview, settings.BorderColor,
                    edits.LeftCrop + edits.OutsideCrop + (shouldMirrorRightDefault || shouldMirrorLeftParallel ? 0.5f : 0),
                    edits.InsideCrop + edits.LeftCrop + (shouldMirrorRightDefault || shouldMirrorLeftParallel ? 0.5f : 0),
                    edits.TopCrop,
                    edits.LeftRotation, edits.RightRotation,
                    edits.VerticalAlignment,
                    edits.LeftZoom, edits.RightZoom,
                    wasPairedCapture && drawQuality == DrawQuality.Preview || isFovStage ? edits.FovLeftCorrection : 0,
                    wasPairedCapture && drawQuality == DrawQuality.Preview || isFovStage ? edits.FovRightCorrection : 0,
                    edits.Keystone,
                    drawMode, fuseGuideRequested,
                    addBarrelDistortion, settings.CardboardSettings.CardboardBarrelDistortion,
                    drawQuality,
                    useFullscreen,
                    cardboardWidthProportion, vert, hor,
                    cardboardDownsizeProportion,
                    settings.CardboardSettings.CardboardIpd,
                    edits);
            }
        }

        private static void DrawImagesOnCanvasInternal(SKSurface surface,
            SKBitmap leftBitmap, SKMatrix leftAlignmentMatrix, SKEncodedOrigin leftOrientation, bool isLeftFrontFacing, bool mirrorLeft,
            SKBitmap rightBitmap, SKMatrix rightAlignmentMatrix, SKEncodedOrigin rightOrientation, bool isRightFrontFacing, bool mirrorRight,
            uint borderThicknessSetting, bool borderRequested, BorderColor borderColor,
            float leftLeftCrop, float rightLeftCrop,
            float topCrop,
            float leftRotation, float rightRotation, float alignment,
            float leftZoom, float rightZoom,
            float leftFovCorrection, float rightFovCorrection,
            float keystone,
            DrawMode drawMode, bool fuseGuideRequested,
            bool addBarrelDistortion, uint barrelStrength,
            DrawQuality drawQuality, bool useFullscreen,
            float cardboardWidthProportion,
            float cardboardVert,
            float cardboardHor,
            float cardboardDownsize,
            uint cardboardIpd,
            Edits edits)
        {
            if (leftBitmap == null && rightBitmap == null) return;

            var canvasWidth = surface.Canvas.DeviceClipBounds.Width;
            var canvasHeight = surface.Canvas.DeviceClipBounds.Height;

            float baseHeight, baseWidth, leftWidth = 0, leftHeight = 0, rightWidth = 0, rightHeight = 0;

            //Debug.WriteLine("leftOrientation: " + leftOrientation);
            //Debug.WriteLine("rightOrientation: " + rightOrientation);
            var isLeft90Oriented = Orientations90deg.Contains(leftOrientation);
            var isRight90Oriented = Orientations90deg.Contains(rightOrientation);
            if (leftBitmap != null)
            {
                if (isLeft90Oriented)
                {
                    leftWidth = leftBitmap.Height;
                    leftHeight = leftBitmap.Width;
                }
                else
                {
                    leftWidth = leftBitmap.Width;
                    leftHeight = leftBitmap.Height;
                }

                if (rightBitmap == null)
                {
                    rightWidth = leftWidth;
                    rightHeight = leftHeight;
                }
            }

            if (rightBitmap != null)
            {
                if (isRight90Oriented)
                {
                    rightWidth = rightBitmap.Height;
                    rightHeight = rightBitmap.Width;
                }
                else
                {
                    rightWidth = rightBitmap.Width;
                    rightHeight = rightBitmap.Height;
                }

                if (leftBitmap == null)
                {
                    leftWidth = rightWidth;
                    leftHeight = rightHeight;
                }
            }

            if (rightFovCorrection != 0)
            {
                baseWidth = leftWidth;
                baseHeight = leftHeight;
            }
            else
            {
                baseWidth = rightWidth;
                baseHeight = rightHeight;
            }

            TrimAdjustment alignmentTrim = new TrimAdjustment(),
                leftEditTrim = new TrimAdjustment(),
                rightEditTrim = new TrimAdjustment(),
                editTrim = new TrimAdjustment();
            if (drawQuality != DrawQuality.Preview)
            {
                alignmentTrim = OrientAndCombineAlignmentTrims(
                    leftBitmap, leftAlignmentMatrix, leftOrientation, isLeftFrontFacing,
                    rightBitmap, rightAlignmentMatrix, rightOrientation, isRightFrontFacing);
                editTrim = FindEditTrimAdjustment(edits, drawMode, baseWidth, baseHeight, out leftEditTrim,
                    out rightEditTrim);

                leftEditTrim.Left = rightEditTrim.Right = Math.Max(leftEditTrim.Left, rightEditTrim.Right);
                leftEditTrim.Right = rightEditTrim.Left = Math.Max(leftEditTrim.Right, rightEditTrim.Left);
                leftEditTrim.Top = rightEditTrim.Top = Math.Max(leftEditTrim.Top, rightEditTrim.Top);
                leftEditTrim.Bottom = rightEditTrim.Bottom = Math.Max(leftEditTrim.Bottom, rightEditTrim.Bottom);
            }
            


            var sideBitmapWidthLessCrop = CalculateJoinedImageWidthWithEditsNoBorder(leftBitmap,
                rightBitmap, edits, alignmentTrim, editTrim, isLeft90Oriented || isRight90Oriented) / 2f;

            if (mirrorLeft || mirrorRight)
            {
                sideBitmapWidthLessCrop /= 2f;
            }

            var overlayDrawing =
                drawMode == DrawMode.GrayscaleRedCyanAnaglyph ||
                drawMode == DrawMode.RedCyanAnaglyph ||
                useFullscreen;
            var addBorder = leftBitmap != null &&
                            rightBitmap != null &&
                            borderRequested &&
                            drawMode != DrawMode.Cardboard &&
                            !overlayDrawing;
            var realBorderThickness = addBorder ?
                CalculateBorderThickness(sideBitmapWidthLessCrop, borderThicknessSetting) :
                0;

            var widthRatio = (sideBitmapWidthLessCrop + realBorderThickness * 1.5f) /
                             (canvasWidth / 2f);
            if (overlayDrawing)
            {
                widthRatio /= 2f;
            }



            var sideBitmapHeightLessCrop = CalculateImageHeightWithEditsNoBorder(leftBitmap,
                rightBitmap, edits, alignmentTrim, editTrim, isLeft90Oriented || isRight90Oriented);

            var bitmapHeightWithEditsAndBorder = sideBitmapHeightLessCrop + realBorderThickness * 2;
            var drawFuseGuide = fuseGuideRequested &&
                                drawMode != DrawMode.Cardboard &&
                                !overlayDrawing;

            float fuseGuideIconWidth = 0, fuseGuideMarginMinimum = 0, topMarginFuseGuideModifier = 0;
            if (drawFuseGuide)
            {
                fuseGuideIconWidth = CalculateFuseGuideWidth(bitmapHeightWithEditsAndBorder);
                fuseGuideMarginMinimum = CalculateFuseGuideMarginHeight(bitmapHeightWithEditsAndBorder);
                topMarginFuseGuideModifier = Math.Max(fuseGuideMarginMinimum - realBorderThickness, 0);
            }

            bitmapHeightWithEditsAndBorder += topMarginFuseGuideModifier;

            var heightRatio = bitmapHeightWithEditsAndBorder / (1f * canvasHeight);


            var fillsWidth = widthRatio > heightRatio;

            var scalingRatio = fillsWidth ? widthRatio : heightRatio;

            fuseGuideIconWidth /= scalingRatio;
            topMarginFuseGuideModifier /= scalingRatio;
            fuseGuideMarginMinimum /= scalingRatio;

            var clipWidth = sideBitmapWidthLessCrop / scalingRatio;
            var clipHeight = sideBitmapHeightLessCrop / scalingRatio;
            
            var scaledBorderThickness = addBorder ? CalculateBorderThickness(clipWidth, borderThicknessSetting) : 0;
            float leftClipX, rightClipX, clipY;
            if (overlayDrawing)
            {
                leftClipX = rightClipX = canvasWidth / 2f - clipWidth / 2f;
                clipY = canvasHeight / 2f - clipHeight / 2f;
            }
            else
            {
                leftClipX = canvasWidth / 2f - 
                            (clipWidth + scaledBorderThickness / 2f);
                rightClipX = canvasWidth / 2f +
                             scaledBorderThickness / 2f;
                clipY = canvasHeight / 2f - clipHeight / 2f;
            }

            var leftDestX = leftClipX - baseWidth / scalingRatio * (leftLeftCrop + alignmentTrim.Left + leftEditTrim.Left);
            var rightDestX = rightClipX - baseWidth / scalingRatio * (rightLeftCrop + alignmentTrim.Left + rightEditTrim.Left);
            var destY = clipY - baseHeight / scalingRatio * (topCrop + alignmentTrim.Top + leftEditTrim.Top);
            var destWidth = baseWidth / scalingRatio;
            var destHeight = baseHeight / scalingRatio;

            if (drawFuseGuide)
            {
                destY += topMarginFuseGuideModifier / 2f;
                clipY += topMarginFuseGuideModifier / 2f;
            }

            var cardboardSeparationMod = 0f;
            if (drawMode == DrawMode.Cardboard)
            {
                var croppedSeparation = (rightClipX - leftClipX) / DeviceDisplay.MainDisplayInfo.Density;
                cardboardSeparationMod = (float) ((cardboardIpd - croppedSeparation) * DeviceDisplay.MainDisplayInfo.Density / 2f);
            }

            var cardboardHorDelta = cardboardHor * destWidth;
            var cardboardVertDelta = cardboardVert * destWidth; // use same property for both to make move speed the same

            var leftIntermediateWidth = leftWidth / scalingRatio;
            var leftIntermediateHeight = leftHeight / scalingRatio;

            var rightIntermediateWidth = rightWidth / scalingRatio;
            var rightIntermediateHeight = rightHeight / scalingRatio;

            var leftXCorrectionToOrigin = leftDestX + leftIntermediateWidth / 2f;
            var leftYCorrectionToOrigin = destY + leftIntermediateHeight / 2f;
            var rightXCorrectionToOrigin = rightDestX + rightIntermediateWidth / 2f;
            var rightYCorrectionToOrigin = destY + rightIntermediateHeight / 2f;

            var skFilterQuality = drawQuality == DrawQuality.Save ? SKFilterQuality.High : SKFilterQuality.Low;

            if (leftBitmap != null)
            {
                var leftScaledAlignmentMatrix = FindScaledAlignmentMatrix(
                    leftIntermediateWidth, leftIntermediateHeight, leftXCorrectionToOrigin, leftYCorrectionToOrigin,
                    leftAlignmentMatrix, scalingRatio);
                var leftOrientationMatrix = FindOrientationMatrix(leftOrientation, leftXCorrectionToOrigin,
                    leftYCorrectionToOrigin, isLeftFrontFacing, mirrorLeft);
                var leftEditMatrix = FindEditMatrix(true, drawMode, leftZoom + leftFovCorrection, leftRotation, keystone,
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
                var rightScaledAlignmentMatrix = FindScaledAlignmentMatrix(
                    rightIntermediateWidth, rightIntermediateHeight, rightXCorrectionToOrigin, rightYCorrectionToOrigin,
                    rightAlignmentMatrix, scalingRatio);
                var rightOrientationMatrix = FindOrientationMatrix(rightOrientation, rightXCorrectionToOrigin,
                    rightYCorrectionToOrigin, isRightFrontFacing, mirrorRight);
                var rightEditMatrix = FindEditMatrix(false, drawMode, rightZoom + rightFovCorrection, rightRotation, keystone,
                    cardboardHorDelta, cardboardVertDelta, alignment,
                    rightDestX, destY, destWidth, destHeight,
                    cardboardSeparationMod);
                DrawSide(surface.Canvas, rightBitmap, false, drawMode,
                    cardboardHorDelta, cardboardVertDelta,
                    rightClipX, clipY, clipWidth, clipHeight,
                    rightDestX, destY, destWidth, destHeight,
                    leftBitmap != null && useFullscreen, cardboardSeparationMod,
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
                var sideHeight = surface.Canvas.DeviceClipBounds.Height * 1f;

                using var smallSurface = SKSurface.Create(new SKImageInfo((int) sideWidth, (int) sideHeight));

                if (leftBitmap != null)
                {
                    smallSurface.Canvas.Clear();
                    smallSurface.Canvas.DrawSurface(surface, 0, 0, paint);
                    using var leftSnapshot = smallSurface.Snapshot();
                    using var distortedLeft = openCv.AddBarrelDistortion(leftSnapshot,
                        cardboardDownsize, barrelStrength / 100f, 1 - cardboardWidthProportion);

                    surface.Canvas.DrawImage(
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
                    using var distortedRight = openCv.AddBarrelDistortion(rightSnapshot,
                        cardboardDownsize, barrelStrength / 100f, cardboardWidthProportion);

                    surface.Canvas.DrawImage(
                        distortedRight,
                        SKRect.Create(
                            sideWidth,
                            0,
                            sideWidth,
                            sideHeight),
                        paint);
                }
            }
            
            var originX = leftClipX - scaledBorderThickness;
            var originY = clipY - scaledBorderThickness;
            var fullPreviewWidth = 2 * clipWidth + 3 * scaledBorderThickness;

            if (scaledBorderThickness > 0)
            {
                using var borderPaint = new SKPaint
                {
                    Color = borderColor == BorderColor.Black ? SKColor.Parse("000000") : SKColor.Parse("ffffff"),
                    Style = SKPaintStyle.StrokeAndFill,
                    FilterQuality = skFilterQuality
                };

                var fullPreviewHeight = clipHeight + 2 * scaledBorderThickness;
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
                var fuseGuideY = clipY - fuseGuideIconWidth / 2f - fuseGuideMarginMinimum / 2f;
                using var guidePaint = new SKPaint
                {
                    Color = borderColor == BorderColor.Black ? 
                        new SKColor(byte.MaxValue, byte.MaxValue, byte.MaxValue) :
                        new SKColor(0,0,0),
                    FilterQuality = skFilterQuality
                };
                surface.Canvas.DrawRect(
                    originX,
                    originY - topMarginFuseGuideModifier,
                    fullPreviewWidth, topMarginFuseGuideModifier,
                    new SKPaint
                    {
                        Color = borderColor == BorderColor.Black ?
                            new SKColor(0, 0, 0) :
                            new SKColor(byte.MaxValue, byte.MaxValue, byte.MaxValue)
                    });
                surface.Canvas.DrawRect(
                    canvasWidth / 2f - scaledBorderThickness / 2f - clipWidth / 2f - fuseGuideIconWidth / 2f,
                    fuseGuideY, fuseGuideIconWidth, fuseGuideIconWidth, guidePaint);
                surface.Canvas.DrawRect(
                    canvasWidth / 2f + scaledBorderThickness / 2f + clipWidth / 2f - fuseGuideIconWidth / 2f,
                    fuseGuideY, fuseGuideIconWidth, fuseGuideIconWidth, guidePaint);
            }
            
            //var dottedPaint = new SKPaint
            //{
            //    Color = SKColors.White,
            //    Style = SKPaintStyle.Stroke,
            //    StrokeWidth = 1f,
            //    StrokeCap = SKStrokeCap.Butt,
            //    PathEffect = SKPathEffect.CreateDash(new []{1f,1f},0)
            //};

            //horizontal
            //var horizontalPath = new SKPath();
            //horizontalPath.MoveTo(0, surface.Canvas.DeviceClipBounds.MidY);
            //horizontalPath.LineTo(surface.Canvas.DeviceClipBounds.Width, surface.Canvas.DeviceClipBounds.MidY);
            //surface.Canvas.DrawPath(horizontalPath, dottedPaint);
            //surface.Canvas.DrawRect(0,surface.Canvas.DeviceClipBounds.MidY, surface.Canvas.DeviceClipBounds.Width,surface.Canvas.DeviceClipBounds.Height*0.05f, new SKPaint
            //{
            //    Color = SKColor.Parse("#ff0000")
            //});

            //vertical
            //var verticalPath = new SKPath();
            //verticalPath.MoveTo(surface.Canvas.DeviceClipBounds.MidX / 2f, 0);
            //verticalPath.LineTo(surface.Canvas.DeviceClipBounds.MidX / 2f, surface.Canvas.DeviceClipBounds.Height);
            //surface.Canvas.DrawPath(verticalPath, dottedPaint);
            //surface.Canvas.DrawRect(surface.Canvas.DeviceClipBounds.MidX / 2f, 0, surface.Canvas.DeviceClipBounds.Width * 0.05f, surface.Canvas.DeviceClipBounds.Height, new SKPaint
            //{
            //    Color = SKColor.Parse("#00ff00")
            //});
        }

        private static TrimAdjustment OrientAndCombineAlignmentTrims(
            SKBitmap leftBitmap, SKMatrix leftAlignment, SKEncodedOrigin leftOrientation, bool isLeftFront,
            SKBitmap rightBitmap, SKMatrix rightAlignment, SKEncodedOrigin rightOrientation, bool isRightFront)
        {
            var leftAlignmentTrim = new TrimAdjustment();
            if (!leftAlignment.IsIdentity &&
                leftBitmap != null)
            {
                leftAlignmentTrim = OrientAlignmentTrim(leftBitmap, leftAlignment, leftOrientation, isLeftFront);
            }

            var rightAlignmentTrim = new TrimAdjustment();
            if (!rightAlignment.IsIdentity &&
                rightBitmap != null)
            {
                rightAlignmentTrim = OrientAlignmentTrim(rightBitmap, rightAlignment, rightOrientation, isRightFront);
            }
            return CombineMaxTrim(leftAlignmentTrim, rightAlignmentTrim);
        }

        private static TrimAdjustment CombineMaxTrim(TrimAdjustment trim1, TrimAdjustment trim2)
        {
            return new TrimAdjustment
            {
                Left = Math.Max(trim1.Left, trim2.Left),
                Right = Math.Max(trim1.Right, trim2.Right),
                Bottom = Math.Max(trim1.Bottom, trim2.Bottom),
                Top = Math.Max(trim1.Top, trim2.Top)
            };
        }

        private static TrimAdjustment OrientAlignmentTrim(SKBitmap bitmap, SKMatrix alignmentMatrix,
            SKEncodedOrigin orientation, bool isFrontFacing)
        {
            var trim = GetTrimAdjustmentFromMatrix(bitmap.Width, bitmap.Height, alignmentMatrix);
            FindOrientationCorrectionDirections(orientation, isFrontFacing, out var needsMirror,
                out var rotationalInc);

            switch (rotationalInc)
            {
                case 0:
                    break;
                case 1:
                    (trim.Top, trim.Right, trim.Bottom, trim.Left) = 
                        (trim.Left, trim.Top, trim.Right, trim.Bottom);
                    break;
                case 2:
                    (trim.Top, trim.Right, trim.Bottom, trim.Left) = 
                        (trim.Bottom, trim.Left, trim.Top, trim.Right);
                    break;
                case -1:
                    (trim.Top, trim.Right, trim.Bottom, trim.Left) = 
                        (trim.Right, trim.Bottom, trim.Left, trim.Top);
                    break;
            }

            if (needsMirror)
            {
                (trim.Left, trim.Right) = (trim.Right, trim.Left);
            }

            return trim;
        }

        private static TrimAdjustment GetTrimAdjustmentFromMatrix(float width, float height, SKMatrix matrix)
        {
            if (matrix.IsIdentity) return new TrimAdjustment();

            var originalPoints = new[]
            {
                new SKPoint(0, 0),
                new SKPoint(width - 1f, 0),
                new SKPoint(width - 1f, height - 1f),
                new SKPoint(0, height - 1f)
            };
            var mappedPoints = matrix.MapPoints(originalPoints);

            return new TrimAdjustment
            {
                Top = Math.Clamp(Math.Max(mappedPoints[0].Y, mappedPoints[1].Y), 0, height) /
                      (height * 1f),
                Left = Math.Clamp(Math.Max(mappedPoints[0].X, mappedPoints[3].X), 0, width) /
                       (width * 1f),
                Right =
                    (width - Math.Clamp(Math.Min(mappedPoints[1].X, mappedPoints[2].X), 0, width)) /
                    (width * 1f),
                Bottom = (height -
                          Math.Clamp(Math.Min(mappedPoints[2].Y, mappedPoints[3].Y), 0, height)) /
                         (height * 1f)
            };
        }

        private static SKMatrix FindScaledAlignmentMatrix(float destWidth, float destHeight,
            float xCorrectionToOrigin, float yCorrectionToOrigin,
            SKMatrix originalAlignment, float scalingRatio)
        {
            var transform3D = SKMatrix.Identity;

            if (!originalAlignment.IsIdentity)
            {
                transform3D = transform3D.PostConcat(SKMatrix.CreateTranslation(-xCorrectionToOrigin, -yCorrectionToOrigin));
                transform3D = transform3D.PostConcat(SKMatrix.CreateScale(scalingRatio, scalingRatio));
                transform3D = transform3D.PostConcat(SKMatrix.CreateTranslation(destWidth * scalingRatio / 2f, destHeight * scalingRatio / 2f));
                transform3D = transform3D.PostConcat(originalAlignment);
                transform3D = transform3D.PostConcat(SKMatrix.CreateTranslation(-destWidth * scalingRatio / 2f, -destHeight * scalingRatio / 2f));
                transform3D = transform3D.PostConcat(SKMatrix.CreateScale(1 / scalingRatio, 1 / scalingRatio));
                transform3D = transform3D.PostConcat(SKMatrix.CreateTranslation(xCorrectionToOrigin, yCorrectionToOrigin));
            }

            return transform3D;
        }

        private static SKMatrix FindOrientationMatrix(SKEncodedOrigin orientation,
            float xCorrectionToOrigin, float yCorrectionToOrigin, bool isFrontFacing, bool withMirror)
        {
            FindOrientationCorrectionDirections(orientation, isFrontFacing, out var orientationNeedsMirror, out var rotationalInc);
            var orientationRotation = (float)(rotationalInc * Math.PI / 2f);

            var transform3D = SKMatrix.Identity;
            transform3D = transform3D.PostConcat(SKMatrix.CreateTranslation(-xCorrectionToOrigin, -yCorrectionToOrigin));
            transform3D = transform3D.PostConcat(SKMatrix.CreateRotation(orientationRotation));
            if (withMirror ^ orientationNeedsMirror)
            {
                transform3D = transform3D.PostConcat(SKMatrix.CreateScale(-1, 1));
            }
            transform3D = transform3D.PostConcat(SKMatrix.CreateTranslation(xCorrectionToOrigin, yCorrectionToOrigin));

            return transform3D;
        }

        private static void FindOrientationCorrectionDirections(SKEncodedOrigin origin, bool isFrontFacing, 
            out bool needsMirror, out int rotationalInc)
        {
            //positive rotation is clockwise for back facing
            //positive rotation is counterclockwise for front facing
            //forward facing adds 180 and a mirror
            //see https://www.nosco.ch/blog/en/2020/10/photo-orientation
            switch (origin)
            {
                case 0:
                    rotationalInc = 0;
                    needsMirror = false;
                    break;
                case SKEncodedOrigin.TopLeft:
                    rotationalInc = isFrontFacing ? 2 : 0;
                    needsMirror = isFrontFacing;
                    break;
                case SKEncodedOrigin.LeftBottom:
                    rotationalInc = -1;
                    needsMirror = isFrontFacing;
                    break;
                case SKEncodedOrigin.BottomRight:
                    rotationalInc = 2 - (isFrontFacing ? 2 : 0);
                    needsMirror = isFrontFacing;
                    break;
                case SKEncodedOrigin.RightTop:
                    rotationalInc = 1;
                    needsMirror = isFrontFacing;
                    break;
                case SKEncodedOrigin.TopRight:
                    rotationalInc = isFrontFacing ? 2 : 0;
                    needsMirror = !isFrontFacing;
                    break;
                case SKEncodedOrigin.RightBottom:
                    rotationalInc = -1;
                    needsMirror = !isFrontFacing;
                    break;
                case SKEncodedOrigin.BottomLeft:
                    rotationalInc = 2 - (isFrontFacing ? 2 : 0);
                    needsMirror = !isFrontFacing;
                    break;
                case SKEncodedOrigin.LeftTop:
                    rotationalInc = 1;
                    needsMirror = !isFrontFacing;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
            }
        }

        private static TrimAdjustment FindEditTrimAdjustment(Edits edits, DrawMode drawMode, float width, float height,
            out TrimAdjustment leftTrim, out TrimAdjustment rightTrim)
        {
            var leftEditTrimMatrix = FindEditMatrix(true, drawMode, edits.LeftZoom + edits.FovLeftCorrection, edits.LeftRotation, edits.Keystone,
                0, 0, edits.VerticalAlignment, 0, 0, width, height, 0);
            var rightEditTrimMatrix = FindEditMatrix(false, drawMode, edits.RightZoom + edits.FovRightCorrection, edits.RightRotation, edits.Keystone,
                0, 0, edits.VerticalAlignment, 0, 0, width, height, 0);
            leftTrim = GetTrimAdjustmentFromMatrix(width, height, leftEditTrimMatrix);
            rightTrim = GetTrimAdjustmentFromMatrix(width, height, rightEditTrimMatrix);
            var combinedTrim = CombineMaxTrim(leftTrim, rightTrim);
            return combinedTrim;
        }

        private static SKMatrix FindEditMatrix(bool isLeft, DrawMode drawMode,
            float zoom, float rotation, float keystone,
            float cardboardHorDelta, float cardboardVertDelta, float alignment,
            float destX, float destY, float destWidth, float destHeight,
            float cardboardSeparationMod)
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
                var isKeystoneSwapped = drawMode == DrawMode.Parallel || drawMode == DrawMode.Cardboard;
                var keystoneRotation = isLeft && !isKeystoneSwapped || !isLeft && isKeystoneSwapped ? keystone : -keystone;

                var axisPositionX = keystoneRotation > 0 ? destX : destX + destWidth;

                var keystoneTransform = SKMatrix44.CreateIdentity();
                keystoneTransform.PostConcat(SKMatrix44.CreateTranslate(-axisPositionX, -yCorrectionToOrigin, 0));
                keystoneTransform.PostConcat(SKMatrix44.CreateRotationDegrees(0, 1, 0, keystoneRotation));
                keystoneTransform.PostConcat(MakePerspective(destWidth));
                keystoneTransform.PostConcat(SKMatrix44.CreateTranslate(axisPositionX, yCorrectionToOrigin, 0));
                
                var leftPoint = new SKPoint(destX, 0);
                var rightPoint = new SKPoint(destX + destWidth, 0);

                var leftTransformed = keystoneTransform.Matrix.MapPoint(leftPoint);
                var rightTransformed = keystoneTransform.Matrix.MapPoint(rightPoint);

                var newWidth = rightTransformed.X - leftTransformed.X;
                var widthChange = destWidth - newWidth;

                keystoneTransform.PostConcat(
                    SKMatrix44.CreateTranslate((keystoneRotation > 0 ? 1 : -1) * widthChange / 2f, 0, 0));

                transform4D.PostConcat(keystoneTransform);
            }

            if (Math.Abs(zoom) > 0)
            {
                transform4D.PostConcat(SKMatrix44.CreateTranslate(-xCorrectionToOrigin, -yCorrectionToOrigin, 0));
                transform4D.PostConcat(SKMatrix44.CreateScale(1 + zoom, 1 + zoom, 0));
                transform4D.PostConcat(SKMatrix44.CreateTranslate(xCorrectionToOrigin, yCorrectionToOrigin, 0));
            }

            if (Math.Abs(alignment) > 0)
            {
                var yCorrection = isLeft
                    ? alignment > 0 ? -alignment * destHeight : 0
                    : alignment < 0 ? alignment * destHeight : 0;
                transform4D.PostConcat(SKMatrix44.CreateTranslate(0, yCorrection, 0));
            }

            transform4D.PostConcat(FindCardboardMovementMatrix(cardboardHorDelta, cardboardVertDelta, cardboardSeparationMod));

            return transform4D.Matrix;
        }

        private static void DrawSide(SKCanvas canvas, SKBitmap bitmap,
            bool isLeft, DrawMode drawMode,
            float cardboardHorDelta, float cardboardVertDelta,
            float clipX, float clipY, float clipWidth, float clipHeight,
            float destX, float destY, float destWidth, float destHeight,
            bool useGhostOverlay, float cardboardSeparationMod,
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

            var clipRect = SKRect.Create(clipX, clipY, clipWidth, clipHeight);
            if (drawMode == DrawMode.Cardboard)
            {
                var cardboardClipRect =
                    FindCardboardMovementMatrix(cardboardHorDelta, cardboardVertDelta, cardboardSeparationMod)
                        .Matrix.MapRect(clipRect);

                if (isLeft)
                {
                    if (cardboardClipRect.Right > DeviceDisplay.MainDisplayInfo.Width / 2f)
                    {
                        cardboardClipRect.Right = (float)(DeviceDisplay.MainDisplayInfo.Width / 2f);
                    }
                }
                else
                {
                    if (cardboardClipRect.Left < DeviceDisplay.MainDisplayInfo.Width / 2f)
                    {
                        cardboardClipRect.Left = (float)(DeviceDisplay.MainDisplayInfo.Width / 2f);
                    }
                }
                canvas.ClipRect(cardboardClipRect);
            }
            else
            {
                canvas.ClipRect(clipRect);
            }


            var destinationRect = SKRect.Create(
                destX,
                destY,
                destWidth,
                destHeight);

            var correctedRect = orientationMatrix.Invert().MapRect(destinationRect);

            var transform = 
                alignmentMatrix.PostConcat(
                orientationMatrix).PostConcat(
                editMatrix);

            canvas.SetMatrix(transform);
            canvas.DrawBitmap(
                bitmap,
                correctedRect, //canvas size is int, but drawing is done with rect of floats - so does drawing truncate or round?
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

        private static SKMatrix44 FindCardboardMovementMatrix(float cardboardHorDelta, float cardboardVertDelta, float cardboardSeparationMod)
        {
            var transform4D = SKMatrix44.CreateIdentity();
            if (Math.Abs(cardboardHorDelta) > 0 ||
                Math.Abs(cardboardVertDelta) > 0 ||
                Math.Abs(cardboardSeparationMod) > 0)
            {
                transform4D.PostConcat(SKMatrix44.CreateTranslate(-cardboardHorDelta + cardboardSeparationMod, -cardboardVertDelta, 0));
            }
            return transform4D;
        }

        public static SKMatrix44 MakePerspective(float maxDepth)
        {
            var perspectiveMatrix = SKMatrix44.CreateIdentity();
            perspectiveMatrix[3, 2] = -1 / maxDepth;
            return perspectiveMatrix;
        }

        private static float CalculateOverlayedImageWidthWithEditsNoBorder(
            SKBitmap leftBitmap, SKBitmap rightBitmap, Edits edits, 
            TrimAdjustment alignmentTrimAdjustment, TrimAdjustment editTrimAdjustment,
            bool is90degOrientation = false)
        {
            if (leftBitmap == null && rightBitmap == null) return 0;

            if (leftBitmap == null ^ rightBitmap == null)
            {
                if (is90degOrientation)
                {
                    return leftBitmap?.Height ?? rightBitmap.Height;
                }
                return leftBitmap?.Width ?? rightBitmap.Width;
            }

            var baseWidth = is90degOrientation
                ? Math.Min(leftBitmap.Height, rightBitmap.Height)
                : Math.Min(leftBitmap.Width, rightBitmap.Width);
            
            return baseWidth *
                   (1 - (edits.LeftCrop + edits.InsideCrop + edits.OutsideCrop + edits.RightCrop +
                         alignmentTrimAdjustment.Left + alignmentTrimAdjustment.Right +
                         editTrimAdjustment.Left + editTrimAdjustment.Right));
        }

        private static float CalculateJoinedImageWidthWithEditsNoBorder(
            SKBitmap leftBitmap, SKBitmap rightBitmap, Edits edits, 
            TrimAdjustment alignmentTrimAdjustment, TrimAdjustment editTrimAdjustment,
            bool is90degOrientation = false)
        {
            return 2 * CalculateOverlayedImageWidthWithEditsNoBorder(
                leftBitmap, rightBitmap, edits, alignmentTrimAdjustment, editTrimAdjustment, 
                is90degOrientation);
        }

        private static float CalculateImageHeightWithEditsNoBorder(
            SKBitmap leftBitmap, SKBitmap rightBitmap, Edits edits, 
            TrimAdjustment alignmentTrimAdjustment, TrimAdjustment editTrimAdjustment,
            bool is90degOrientation = false)
        {
            if (leftBitmap == null && rightBitmap == null) return 0;

            if (leftBitmap == null ^ rightBitmap == null)
            {
                if (is90degOrientation)
                {
                    return leftBitmap?.Width ?? rightBitmap.Width;
                }
                return leftBitmap?.Height ?? rightBitmap.Height;
            }

            float baseHeight = is90degOrientation
                ? Math.Min(leftBitmap.Width, rightBitmap.Width)
                : Math.Min(leftBitmap.Height, rightBitmap.Height);
            return baseHeight * 
                   (1 - (edits.TopCrop + edits.BottomCrop + Math.Abs(edits.VerticalAlignment) +
                         alignmentTrimAdjustment.Top + alignmentTrimAdjustment.Bottom +
                         editTrimAdjustment.Top + editTrimAdjustment.Bottom));
        }

        public static SizeF CalculateOverlayedImageSizeOrientedWithEditsNoBorder(Edits edits, Settings settings, SKBitmap leftBitmap,
            SKMatrix leftAlignmentTransform, SKBitmap rightBitmap, SKMatrix rightAlignmentTransform)
        {
            var editTrim = FindEditTrimAdjustment(edits, settings.Mode,
                leftBitmap.Width, leftBitmap.Height, out _, out _);
            var alignmentTrim = OrientAndCombineAlignmentTrims(
                leftBitmap, leftAlignmentTransform, SKEncodedOrigin.Default, false,
                rightBitmap, rightAlignmentTransform, SKEncodedOrigin.Default, false);
            var width = CalculateOverlayedImageWidthWithEditsNoBorder(
                leftBitmap, rightBitmap, edits, alignmentTrim, editTrim);
            var height = CalculateImageHeightWithEditsNoBorder(
                leftBitmap, rightBitmap, edits, alignmentTrim, editTrim);

            return new SizeF(width, height);
        }

        public static SizeF CalculateJoinedImageSizeOrientedWithEditsNoBorder(Edits edits, Settings settings, SKBitmap leftBitmap, 
            SKMatrix leftAlignmentTransform, SKBitmap rightBitmap, SKMatrix rightAlignmentTransform)
        {
            var overlayedSize = CalculateOverlayedImageSizeOrientedWithEditsNoBorder(edits, settings, leftBitmap, leftAlignmentTransform,
                rightBitmap, rightAlignmentTransform);
            
            return new SizeF(2 * overlayedSize.Width, overlayedSize.Height);
        }

        public static float CalculateBorderThickness(float sideWidth, float thicknessSetting)
        {
            return sideWidth * thicknessSetting * BORDER_CONVERSION_FACTOR;
        }

        public static float CalculateFuseGuideMarginHeight(float imageHeightWithEditsAndBorder)
        {
            return CalculateFuseGuideWidth(imageHeightWithEditsAndBorder) * FUSE_GUIDE_MARGIN_HEIGHT_RATIO;
        }

        private static float CalculateFuseGuideWidth(float imageHeightWithEditsAndBorder)
        {
            return imageHeightWithEditsAndBorder * FUSE_GUIDE_WIDTH_RATIO;
        }
    }
}