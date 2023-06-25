﻿using System;

namespace CrossCam.Model
{
    public class EditsSettings : Subsettings
    {
        private double _zoomMax;
        public double ZoomMax
        {
            get => _zoomMax;
            set => _zoomMax = Math.Abs(value);
        }

        private double _sideCropMax;
        public double SideCropMax
        {
            get => _sideCropMax;
            set => _sideCropMax = Math.Abs(value);
        }

        private double _topOrBottomCropMax;
        public double TopOrBottomCropMax
        {
            get => _topOrBottomCropMax;
            set => _topOrBottomCropMax = Math.Abs(value);
        }

        private double _verticalAlignmentMax;
        public double VerticalAlignmentMax
        {
            get => _verticalAlignmentMax;
            set => _verticalAlignmentMax = Math.Abs(value);
        }

        private float _rotationMax;
        public float RotationMax
        {
            get => _rotationMax;
            set => _rotationMax = Math.Abs(value);
        }

        private float _keystoneMax;
        public float KeystoneMax
        {
            get => _keystoneMax;
            set => _keystoneMax = Math.Abs(value);
        }

        public override void ResetToDefaults()
        {
            ZoomMax = 1 / 4d;
            SideCropMax = 1 / 2d;
            TopOrBottomCropMax = 1 / 2d;
            VerticalAlignmentMax = 1 / 8d;
            RotationMax = 5;
            KeystoneMax = 15f;
        }
    }
}