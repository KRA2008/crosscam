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
    private bool _isCleanMatches;

    public AutoAlignmentExperimentsPage()
    {
        _canvas = new SKCanvasView
        {
            BackgroundColor = Colors.Blue
        };
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

        Content = new AbsoluteLayout
        {
            BackgroundColor = Colors.Green,
            Children =
            {
                _canvas,
                button
            }
        };
    }

    private void CanvasOnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        Debug.WriteLine("WHAT IS GOING ON?");
        var matches = _isCleanMatches && _alignedResult.DrawnCleanMatches != null ? 
            _alignedResult.DrawnCleanMatches : 
            _alignedResult.DrawnDirtyMatches;
        var aspectRatio = matches.Width / (matches.Height * 1f);
        var matchesWidth = Height * aspectRatio;
        e.Surface.Canvas.DrawBitmap(matches,
            new SKRect(0, 0, (float)matchesWidth, (float)Height));
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

            var alignmentSettings = new AlignmentSettings();
            alignmentSettings.ResetToDefaults();
            alignmentSettings.DrawKeypointMatches = true;
            alignmentSettings.DiscardOutliersByDistance = true;
            alignmentSettings.DiscardOutliersBySlope1 = true;
            alignmentSettings.PhysicalDistanceThreshold = 0.3f;

            _alignedResult = autoAlignment.CreateAlignedSecondImageKeypoints(leftBitmap, rightBitmap, alignmentSettings, false);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _canvas.InvalidateSurface();
            });
        }
        catch (Exception e)
        {
            Debugger.Break();
        }
    }

    private void Button_OnClicked(object sender, EventArgs e)
    {
        _isCleanMatches = !_isCleanMatches;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _canvas.InvalidateSurface();
        });
    }
}