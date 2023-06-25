﻿using System;
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
        public static readonly SKEncodedOrigin[] Orientations90deg = new[]
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
            DrawQuality drawQuality = DrawQuality.Save, double cardboardVert = 0, double cardboardHor = 0, bool isFovStage = false,
            bool useFullscreen = false, bool useMirrorCapture = false)
        {
            var fuseGuideRequested = drawQuality != DrawQuality.Preview && 
                                     settings.SaveWithFuseGuide ||
                                     drawQuality == DrawQuality.Preview &&
                                     settings.ShowPreviewFuseGuide;

            var skFilterQuality = drawQuality == DrawQuality.Preview || 
                                  drawQuality == DrawQuality.Review ? 
                                  SKFilterQuality.Low : SKFilterQuality.High;

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

            double cardboardWidthProportion = 0;
            if (drawMode == DrawMode.Cardboard)
            {
                cardboardWidthProportion = settings.CardboardSettings.CardboardIpd /
                                           (Math.Max(DeviceDisplay.MainDisplayInfo.Width,
                                                DeviceDisplay.MainDisplayInfo.Height) /
                                            DeviceDisplay.MainDisplayInfo.Density / 2d) / 2d;
            }

            var cardboardDownsizeProportion = drawQuality != DrawQuality.Save &&
                                              drawMode == DrawMode.Cardboard &&
                                              settings.CardboardSettings.CardboardDownsize ? settings.CardboardSettings.CardboardDownsizePercentage / 100d : 1d;
            double vert = 0, hor = 0;
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
                    (int)settings.BorderWidthProportion, settings.AddBorder2 && drawQuality != DrawQuality.Preview, settings.BorderColor,
                    edits.InsideCrop + edits.LeftCrop,
                    edits.RightCrop + edits.OutsideCrop,
                    edits.LeftCrop + edits.OutsideCrop,
                    edits.InsideCrop + edits.RightCrop,
                    edits.TopCrop, 
                    edits.BottomCrop,
                    edits.RightRotation, edits.LeftRotation,
                    -edits.VerticalAlignment,
                    edits.RightZoom, edits.LeftZoom,
                    wasPairedCapture && drawQuality == DrawQuality.Preview || isFovStage ? edits.FovRightCorrection : 0,
                    wasPairedCapture && drawQuality == DrawQuality.Preview || isFovStage ? edits.FovLeftCorrection : 0,
                    edits.Keystone,
                    drawMode, fuseGuideRequested,
                    addBarrelDistortion, (int)settings.CardboardSettings.CardboardBarrelDistortion,
                    skFilterQuality,
                    useFullscreen,
                    cardboardWidthProportion, vert, hor,
                    (float)cardboardDownsizeProportion,
                    (int)settings.CardboardSettings.CardboardIpd);
            }
            else
            {
                DrawImagesOnCanvasInternal(surface, 
                    leftBitmap, leftAlignmentMatrix, leftOrientation, isLeftFrontFacing, shouldMirrorLeftDefault || shouldMirrorLeftParallel,
                    rightBitmap, rightAlignmentMatrix, rightOrientation, isRightFrontFacing, shouldMirrorRightDefault || shouldMirrorRightParallel,
                    (int)settings.BorderWidthProportion, settings.AddBorder2 && drawQuality != DrawQuality.Preview, settings.BorderColor,
                    edits.LeftCrop + edits.OutsideCrop + (shouldMirrorRightDefault || shouldMirrorLeftParallel ? 0.5 : 0),
                    edits.InsideCrop + edits.RightCrop + (shouldMirrorLeftDefault || shouldMirrorRightParallel ? 0.5 : 0),
                    edits.InsideCrop + edits.LeftCrop + (shouldMirrorRightDefault || shouldMirrorLeftParallel ? 0.5 : 0),
                    edits.RightCrop + edits.OutsideCrop + (shouldMirrorLeftDefault || shouldMirrorRightParallel ? 0.5 : 0),
                    edits.TopCrop, 
                    edits.BottomCrop,
                    edits.LeftRotation, edits.RightRotation,
                    edits.VerticalAlignment,
                    edits.LeftZoom, edits.RightZoom,
                    wasPairedCapture && drawQuality == DrawQuality.Preview || isFovStage ? edits.FovLeftCorrection : 0,
                    wasPairedCapture && drawQuality == DrawQuality.Preview || isFovStage ? edits.FovRightCorrection : 0,
                    edits.Keystone,
                    drawMode, fuseGuideRequested,
                    addBarrelDistortion, (int)settings.CardboardSettings.CardboardBarrelDistortion,
                    skFilterQuality,
                    useFullscreen,
                    cardboardWidthProportion, vert, hor,
                    (float)cardboardDownsizeProportion,
                    (int)settings.CardboardSettings.CardboardIpd);
            }
        }

        private static void DrawImagesOnCanvasInternal(SKSurface surface,
            SKBitmap leftBitmap, SKMatrix leftAlignmentMatrix, SKEncodedOrigin leftOrientation, bool isLeftFrontFacing, bool mirrorLeft,
            SKBitmap rightBitmap, SKMatrix rightAlignmentMatrix, SKEncodedOrigin rightOrientation, bool isRightFrontFacing, bool mirrorRight,
            int borderThickness, bool addBorder, BorderColor borderColor,
            double leftLeftCrop, double leftRightCrop, double rightLeftCrop, double rightRightCrop,
            double topCrop, double bottomCrop,
            float leftRotation, float rightRotation, double alignment,
            double leftZoom, double rightZoom,
            double leftFovCorrection, double rightFovCorrection,
            float keystone,
            DrawMode drawMode, bool fuseGuideRequested,
            bool addBarrelDistortion, int barrelStrength,
            SKFilterQuality skFilterQuality, bool useFullscreen,
            double cardboardWidthProportion,
            double cardboardVert,
            double cardboardHor,
            float cardboardDownsize,
            int cardboardIpd)
        {
            if (leftBitmap == null && rightBitmap == null) return;

            var canvasWidth = surface.Canvas.DeviceClipBounds.Width;
            var canvasHeight = surface.Canvas.DeviceClipBounds.Height;

            double baseHeight, baseWidth, leftWidth = 0, leftHeight = 0, rightWidth = 0, rightHeight = 0, netSideCrop = 0;

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

                netSideCrop = leftLeftCrop + leftRightCrop;
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

                netSideCrop = rightLeftCrop + rightRightCrop;
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

            var alignmentTrim = GetAlignmentTrim(
                leftBitmap, leftAlignmentMatrix, leftOrientation, isLeftFrontFacing,
                rightBitmap, rightAlignmentMatrix, rightOrientation, isRightFrontFacing);
            
            var leftEditTrimMatrix = FindEditMatrix(true, drawMode, leftZoom + leftFovCorrection, leftRotation, keystone,
                0, 0, alignment, 0, 0, (float) leftWidth, (float) leftHeight, 0);
            var rightEditTrimMatrix = FindEditMatrix(false, drawMode, rightZoom + rightFovCorrection, rightRotation, keystone,
                0, 0, alignment, 0, 0, (float) rightWidth, (float) rightHeight, 0);

            var leftEditTrim = new TrimAdjustment();
            if (leftBitmap != null)
            {
                leftEditTrim = FindMatrixTrimAdjustment((int) leftWidth, (int) leftHeight, leftEditTrimMatrix);
            }

            var rightEditTrim = new TrimAdjustment();
            if (rightBitmap != null)
            {
                rightEditTrim = FindMatrixTrimAdjustment((int) rightWidth, (int) rightHeight, rightEditTrimMatrix);
            }

            leftEditTrim.Left = rightEditTrim.Right = Math.Max(leftEditTrim.Left, rightEditTrim.Right);
            leftEditTrim.Right = rightEditTrim.Left = Math.Max(leftEditTrim.Right, rightEditTrim.Left);
            leftEditTrim.Top = rightEditTrim.Top = Math.Max(leftEditTrim.Top, rightEditTrim.Top);
            leftEditTrim.Bottom = rightEditTrim.Bottom = Math.Max(leftEditTrim.Bottom, rightEditTrim.Bottom);

            netSideCrop += alignmentTrim.Left + alignmentTrim.Right + leftEditTrim.Left + leftEditTrim.Right;
            var sideBitmapWidthLessCrop = baseWidth * (1 - netSideCrop);

            var sideBitmapHeightLessCrop = baseHeight * (1 - (topCrop + bottomCrop + Math.Abs(alignment) +
                                                              alignmentTrim.Top + alignmentTrim.Bottom +
                                                              leftEditTrim.Top + leftEditTrim.Bottom));
            var overlayDrawing =
                drawMode == DrawMode.GrayscaleRedCyanAnaglyph ||
                drawMode == DrawMode.RedCyanAnaglyph ||
                useFullscreen;
            var innerBorderThicknessProportion = leftBitmap != null &&
                                                 rightBitmap != null &&
                                                 addBorder &&
                                                 drawMode != DrawMode.Cardboard &&
                                                 !overlayDrawing ?
                BORDER_CONVERSION_FACTOR * borderThickness :
                0;

            var widthRatio = sideBitmapWidthLessCrop * (1 + innerBorderThicknessProportion * 1.5) /
                             (canvasWidth / 2d);
            if (overlayDrawing)
            {
                widthRatio /= 2d;
            }

            var realBorderTopHeight = sideBitmapWidthLessCrop * innerBorderThicknessProportion;
            var bitmapHeightWithEditsAndBorder =
                sideBitmapHeightLessCrop + realBorderTopHeight * 2;
            var drawFuseGuide = fuseGuideRequested &&
                                drawMode != DrawMode.Cardboard &&
                                !overlayDrawing;

            var fuseGuideIconWidth = CalculateFuseGuideWidth((float)bitmapHeightWithEditsAndBorder);
            var fuseGuideMarginMinimum = CalculateFuseGuideMarginHeight((float)bitmapHeightWithEditsAndBorder);
            var topMarginFuseGuideModifier = 0f;

            if (drawFuseGuide)
            {
                if (realBorderTopHeight < fuseGuideMarginMinimum)
                {
                    topMarginFuseGuideModifier = (float) (fuseGuideMarginMinimum - realBorderTopHeight);
                }
                else
                {
                    topMarginFuseGuideModifier = 0;
                }
            }

            bitmapHeightWithEditsAndBorder += topMarginFuseGuideModifier;

            var heightRatio = bitmapHeightWithEditsAndBorder / (1d * canvasHeight);

            var fillsWidth = widthRatio > heightRatio;

            var scalingRatio = fillsWidth ? widthRatio : heightRatio;

            fuseGuideIconWidth = (float) (fuseGuideIconWidth / scalingRatio);
            topMarginFuseGuideModifier = (float) (topMarginFuseGuideModifier / scalingRatio);
            fuseGuideMarginMinimum = (float) (fuseGuideMarginMinimum / scalingRatio);

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

            var leftDestX = (float)(leftClipX - baseWidth / scalingRatio * (leftLeftCrop + alignmentTrim.Left + leftEditTrim.Left));
            var rightDestX = (float)(rightClipX - baseWidth / scalingRatio * (rightLeftCrop + alignmentTrim.Left + rightEditTrim.Left));
            var destY = (float)(clipY - baseHeight / scalingRatio * (topCrop + alignmentTrim.Top + leftEditTrim.Top));
            var destWidth = (float)(baseWidth / scalingRatio);
            var destHeight = (float)(baseHeight / scalingRatio);

            if (drawFuseGuide)
            {
                destY += topMarginFuseGuideModifier / 2f;
                clipY += topMarginFuseGuideModifier / 2f;
            }

            var cardboardSeparationMod = 0d;
            if (drawMode == DrawMode.Cardboard)
            {
                var croppedSeparation = (rightClipX - leftClipX) / DeviceDisplay.MainDisplayInfo.Density;
                cardboardSeparationMod = (cardboardIpd - croppedSeparation) * DeviceDisplay.MainDisplayInfo.Density / 2d;
            }

            var cardboardHorDelta = cardboardHor * destWidth;
            var cardboardVertDelta = cardboardVert * destWidth; // use same property for both to make move speed the same

            var leftIntermediateWidth = (float) (leftWidth / scalingRatio);
            var leftIntermediateHeight = (float) (leftHeight / scalingRatio);

            var rightIntermediateWidth = (float) (rightWidth / scalingRatio);
            var rightIntermediateHeight = (float) (rightHeight / scalingRatio);

            var leftXCorrectionToOrigin = leftDestX + leftIntermediateWidth / 2f;
            var leftYCorrectionToOrigin = destY + leftIntermediateHeight / 2f;
            var rightXCorrectionToOrigin = rightDestX + rightIntermediateWidth / 2f;
            var rightYCorrectionToOrigin = destY + rightIntermediateHeight / 2f;

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
                var sideHeight = surface.Canvas.DeviceClipBounds.Height;

                using var smallSurface = SKSurface.Create(new SKImageInfo((int) sideWidth, sideHeight));

                if (leftBitmap != null)
                {
                    smallSurface.Canvas.Clear();
                    smallSurface.Canvas.DrawSurface(surface, 0, 0, paint);
                    using var leftSnapshot = smallSurface.Snapshot();
                    using var distortedLeft = openCv.AddBarrelDistortion(leftSnapshot,
                        cardboardDownsize, barrelStrength / 100f, (float)(1 - cardboardWidthProportion));

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
                        cardboardDownsize, barrelStrength / 100f, (float)cardboardWidthProportion);

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

            var originX = (float)(leftClipX - innerBorderThicknessProportion * clipWidth);
            var originY = (float)(clipY - innerBorderThicknessProportion * clipWidth);
            var fullPreviewWidth = (float)(2 * clipWidth + 3 * innerBorderThicknessProportion * clipWidth);


            if (innerBorderThicknessProportion > 0)
            {
                using var borderPaint = new SKPaint
                {
                    Color = borderColor == BorderColor.Black ? SKColor.Parse("000000") : SKColor.Parse("ffffff"),
                    Style = SKPaintStyle.StrokeAndFill,
                    FilterQuality = skFilterQuality
                };

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
                    canvasWidth / 2f - previewBorderThickness / 2f - clipWidth / 2f - fuseGuideIconWidth,
                    fuseGuideY, fuseGuideIconWidth, fuseGuideIconWidth, guidePaint);
                surface.Canvas.DrawRect(
                    canvasWidth / 2f + previewBorderThickness / 2f + clipWidth / 2f + fuseGuideIconWidth,
                    fuseGuideY, fuseGuideIconWidth, fuseGuideIconWidth, guidePaint);
            }
        }

        private static TrimAdjustment GetAlignmentTrim(
            SKBitmap leftBitmap, SKMatrix leftAlignment, SKEncodedOrigin leftOrientation, bool isLeftFront,
            SKBitmap rightBitmap, SKMatrix rightAlignment, SKEncodedOrigin rightOrientation, bool isRightFront)
        {
            var leftAlignmentTrim = new TrimAdjustment();
            if (!leftAlignment.IsIdentity &&
                leftBitmap != null)
            {
                leftAlignmentTrim = FindAlignmentTrim(leftBitmap, leftAlignment, leftOrientation, isLeftFront);
            }

            var rightAlignmentTrim = new TrimAdjustment();
            if (!rightAlignment.IsIdentity &&
                rightBitmap != null)
            {
                rightAlignmentTrim = FindAlignmentTrim(rightBitmap, rightAlignment, rightOrientation, isRightFront);
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

        private static TrimAdjustment FindAlignmentTrim(SKBitmap bitmap, SKMatrix alignmentMatrix,
            SKEncodedOrigin orientation, bool isFrontFacing)
        {
            var trim = FindMatrixTrimAdjustment(bitmap.Width, bitmap.Height, alignmentMatrix);
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

        private static TrimAdjustment FindMatrixTrimAdjustment(int width, int height, SKMatrix matrix)
        {
            if (matrix.IsIdentity) return new TrimAdjustment();

            var mappedPoints = matrix.MapPoints(new[]
            {
                new SKPoint(0, 0),
                new SKPoint(width - 1, 0),
                new SKPoint(width - 1, height - 1),
                new SKPoint(0, height - 1)
            });
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

        public static SKMatrix FindOrientationMatrix(SKEncodedOrigin orientation,
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

        public static void FindOrientationCorrectionDirections(SKEncodedOrigin origin, bool isFrontFacing, 
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

            transform4D.PostConcat(FindCardboardMovementMatrix(cardboardHorDelta, cardboardVertDelta, cardboardSeparationMod));

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

        private static SKMatrix44 FindCardboardMovementMatrix(double cardboardHorDelta, double cardboardVertDelta, double cardboardSeparationMod)
        {
            var transform4D = SKMatrix44.CreateIdentity();
            if (Math.Abs(cardboardHorDelta) > 0 ||
                Math.Abs(cardboardVertDelta) > 0 ||
                Math.Abs(cardboardSeparationMod) > 0)
            {
                transform4D.PostConcat(SKMatrix44.CreateTranslate((float)(-cardboardHorDelta + cardboardSeparationMod), (float)-cardboardVertDelta, 0));
            }
            return transform4D;
        }

        public static SKMatrix44 MakePerspective(float maxDepth)
        {
            var perspectiveMatrix = SKMatrix44.CreateIdentity();
            perspectiveMatrix[3, 2] = -1 / maxDepth;
            return perspectiveMatrix;
        }

        public static int CalculateOverlayedCanvasWidthWithEditsNoBorder(
            SKBitmap leftBitmap, SKMatrix leftAlignment, SKBitmap rightBitmap, SKMatrix rightAlignment, Edits edits)
        {
            if (leftBitmap == null && rightBitmap == null) return 0;

            if (leftBitmap == null ^ rightBitmap == null)
            {
                return leftBitmap?.Width ?? rightBitmap.Width;
            }

            var alignmentTrim = GetAlignmentTrim(
                leftBitmap, leftAlignment, SKEncodedOrigin.Default, false,
                rightBitmap, rightAlignment, SKEncodedOrigin.Default, false);

            var baseWidth = Math.Min(leftBitmap.Width, rightBitmap.Width);
            
            return (int)(baseWidth *
                (1 - (edits.LeftCrop + edits.InsideCrop + edits.OutsideCrop + edits.RightCrop +
                      alignmentTrim.Left + alignmentTrim.Right)));
        }

        public static int CalculateJoinedCanvasWidthWithEditsNoBorder(
            SKBitmap leftBitmap, SKMatrix leftAlignment, SKBitmap rightBitmap, SKMatrix rightAlignment, Edits edits)
        {
            return 2 * CalculateOverlayedCanvasWidthWithEditsNoBorder(
                leftBitmap, leftAlignment, rightBitmap, rightAlignment, edits);
            }

        public static int CalculateCanvasHeightWithEditsNoBorder(
            SKBitmap leftBitmap, SKMatrix leftAlignment, SKBitmap rightBitmap, SKMatrix rightAlignment, Edits edits)
        {
            if (leftBitmap == null && rightBitmap == null) return 0;

            if (leftBitmap == null ^ rightBitmap == null)
            {
                return leftBitmap?.Height ?? rightBitmap.Height;
            }

            var alignmentTrim = GetAlignmentTrim(
                leftBitmap, leftAlignment, SKEncodedOrigin.Default, false,
                rightBitmap, rightAlignment, SKEncodedOrigin.Default, false);

            var baseHeight = Math.Min(leftBitmap.Height, rightBitmap.Height);
            return (int) (baseHeight * 
                          (1 - (edits.TopCrop + edits.BottomCrop + Math.Abs(edits.VerticalAlignment) +
                                alignmentTrim.Top + alignmentTrim.Bottom)));
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