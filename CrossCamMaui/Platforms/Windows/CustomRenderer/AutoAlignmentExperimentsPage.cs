using System.Diagnostics;
using System.Reflection;
using CrossCam.Model;
using CrossCam.Wrappers;
using Microsoft.Maui.Layouts;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace CrossCam.Platforms.Windows.CustomRenderer;

public class AutoAlignmentExperimentsPage : ContentPage
{
    private SKCanvasView _canvas;
    private AlignedResult _alignedResult;
    private Label _infoLabel;
    private DisplayMode _displayMode;

    public AutoAlignmentExperimentsPage()
    {
        _canvas = new SKCanvasView();
        _canvas.PaintSurface += CanvasOnPaintSurface;
        AbsoluteLayout.SetLayoutFlags(_canvas, AbsoluteLayoutFlags.All);
        AbsoluteLayout.SetLayoutBounds(_canvas, new Rect(0, 0, 1, 1));

        var button = new Button
        {
            BackgroundColor = Colors.Yellow,
            Text = "Toggle"
        };
        button.Clicked += Button_OnClicked;
        AbsoluteLayout.SetLayoutFlags(button, AbsoluteLayoutFlags.PositionProportional);
        AbsoluteLayout.SetLayoutBounds(button, new Rect(1, 1, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));

        _infoLabel = new Label
        {
            TextColor = Colors.Green
        };
        AbsoluteLayout.SetLayoutFlags(_infoLabel, AbsoluteLayoutFlags.PositionProportional);
        AbsoluteLayout.SetLayoutBounds(_infoLabel, new Rect(0,0, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));

        Content = new AbsoluteLayout
        {
            Children =
            {
                _canvas,
                button,
                _infoLabel
            }
        };
    }

    private void CanvasOnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        e.Surface.Canvas.Clear();
        if (_displayMode == DisplayMode.DirtyMatches ||
            _displayMode == DisplayMode.CleanMatches)
        {
            var bitmapToDraw = _displayMode == DisplayMode.DirtyMatches
                ? _alignedResult.DrawnDirtyMatches
                : _alignedResult.DrawnCleanMatches;
            _infoLabel.Text = _displayMode == DisplayMode.DirtyMatches
                ? "Dirty: " + _alignedResult.DirtyMatchesCount
                : "Clean: " + _alignedResult.CleanMatchesCount;
            if (bitmapToDraw == null) return;
            var aspectRatio = bitmapToDraw.Width / (bitmapToDraw.Height * 1f);
            var matchesWidth = Height * aspectRatio;
            e.Surface.Canvas.DrawBitmap(bitmapToDraw,
                new SKRect(0, 0, (float)matchesWidth, (float)Height));
        }
        else
        {
            _infoLabel.Text = "Aligned";
            var aspectRatio = _alignedResult.Warped1.Width / (_alignedResult.Warped1.Height * 1f);
            var aspectFillWidth = Height * aspectRatio;
            e.Surface.Canvas.DrawBitmap(_alignedResult.Warped1,
                new SKRect(0, 0, (float)aspectFillWidth, (float)Height));
            e.Surface.Canvas.DrawBitmap(_alignedResult.Warped2,
                new SKRect((float)aspectFillWidth, 0, (float)(2*aspectFillWidth), (float)Height));
        }
    }

    protected override async void OnAppearing()
    {
        try
        {
            base.OnAppearing();

            var autoAlignment = new OpenCv();
            var assembly = GetType().GetTypeInfo().Assembly;
            var resourceBase = "CrossCam.Platforms.Windows.Resources.moiraine";
            await using var leftStream = assembly.GetManifestResourceStream(resourceBase + "left.JPG");
            await using var rightStream = assembly.GetManifestResourceStream(resourceBase + "right.JPG");
            using var leftBitmap = SKBitmap.Decode(leftStream);
            using var rightBitmap = SKBitmap.Decode(rightStream);

            var alignmentSettings = new AlignmentSettings
            {
                DrawKeypointMatches = true,
                UseCrossCheck = false,
                MinimumKeypoints1 = 0,
                DiscardOutliersByDistance = true,
                DiscardOutliersBySlope1 = true,
                TransformationFindingMethod = (uint) TransformationFindingMethod.BinarySearch
                //ReadModeColor = false
            };

            _alignedResult = autoAlignment.ComboAlign(leftBitmap, rightBitmap, alignmentSettings);

            if (_alignedResult == null) throw new Exception("the alignment failed.");

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _infoLabel.Text = _alignedResult.DirtyMatchesCount.ToString();
                _canvas.InvalidateSurface();
            });
        }
        catch (Exception e)
        {
            Debugger.Break();
            throw;
        }
    }

    private void Button_OnClicked(object sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _displayMode += 1;
            if (_displayMode == (DisplayMode)4) _displayMode = 0;
            _canvas.InvalidateSurface();
        });
    }

    private enum DisplayMode
    {
        DirtyMatches,
        CleanMatches,
        Aligned
    }
}