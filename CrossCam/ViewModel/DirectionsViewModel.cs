using System;
using CrossCam.Model;
using FreshMvvm;

namespace CrossCam.ViewModel
{
    public class DirectionsViewModel : FreshBasePageModel
    {
        private Settings _settings;

        public string ViewModeIng
        {
            get
            {
                var retString = "";
                switch (_settings.Mode)
                {
                    case DrawMode.Parallel:
                        retString = "parallel";
                        break;
                    case DrawMode.Cross:
                        retString = "cross";
                        break;
                    case DrawMode.RedCyanAnaglyph:
                        retString = "cross";
                        break;
                    case DrawMode.GrayscaleRedCyanAnaglyph:
                        retString = "cross";
                        break;
                    case DrawMode.Cardboard:
                        retString = "cardboard";
                        break;
                }

                retString += " viewing";

                return retString;
            }
        }
        
        public string DirectionToMove
        {
            get
            {
                if (_settings.IsCaptureLeftFirst && (_settings.Mode == DrawMode.Parallel || _settings.Mode == DrawMode.Cardboard) ||
                    !_settings.IsCaptureLeftFirst && _settings.Mode != DrawMode.Parallel && _settings.Mode != DrawMode.Cardboard)
                    return "RIGHT";
                return "LEFT";
            }
        }

        public override void Init(object initData)
        {
            base.Init(initData);
            _settings = (Settings)initData;
        }
    }
}