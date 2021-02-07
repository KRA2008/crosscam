﻿using CrossCam.ViewModel;
using SkiaSharp;

namespace CrossCam.Model
{
    public class AlignedResult
    {
        public AlignmentMode AlignmentMode { get; set; }
        public SKBitmap AlignedBitmap { get; set; }
        public SKBitmap DrawnCleanMatches { get; set; }
        public SKBitmap DrawnDirtyMatches { get; set; }
        public SKMatrix TransformMatrix { get; set; }
    }
}