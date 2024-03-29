﻿using CrossCam.Model;

namespace CrossCam.ViewModel
{
    public class DirectionsViewModel : BaseViewModel
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
                        retString = "anaglyph";
                        break;
                    case DrawMode.GrayscaleRedCyanAnaglyph:
                        retString = "anaglyph";
                        break;
                    case DrawMode.Cardboard:
                        retString = "cardboard";
                        break;
                }

                retString += " viewing";

                return retString;
            }
        }

        public override void Init(object initData)
        {
            base.Init(initData);
            _settings = (Settings)initData;
        }
    }
}