﻿using CrossCam.Wrappers;
using Microsoft.AppCenter.Crashes;
using StoreKit;
using UIKit;

namespace CrossCam.Platforms.iOS.CustomRenderer
{
    public class StoreReviewOpener : IStoreReviewOpener
    {
        public Task TryOpenStoreReview()
        {
            var result = new TaskCompletionSource<bool>();
            try
            {
                if (UIDevice.CurrentDevice.CheckSystemVersion(10, 3))
                {
                    SKStoreReviewController.RequestReview();
                }
            }
            catch (Exception e)
            {
                Crashes.TrackError(e);
                result.SetResult(false);
                return result.Task;
            }

            result.SetResult(true);
            return result.Task;
        }
    }
}