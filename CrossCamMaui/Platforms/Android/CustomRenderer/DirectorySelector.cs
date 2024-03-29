﻿using Android.Content;
using Android.OS;
using CrossCam.Wrappers;
using Environment = Android.OS.Environment;
using Uri = Android.Net.Uri;

namespace CrossCam.Platforms.Android.CustomRenderer
{
    public class DirectorySelector : IDirectorySelector
    {
        private static TaskCompletionSource<string> _completionSource;

        public bool CanSaveToArbitraryDirectory()
        {
            return Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop;
        }

        public string GetExternalSaveDirectory()
        {
            try
            {
                var externalDirectory = MainActivity.Instance.GetExternalFilesDirs(Environment.DirectoryPictures)
                    .ElementAtOrDefault(1);
                if (Build.VERSION.SdkInt < BuildVersionCodes.Lollipop &&
                    Environment.ExternalStorageState == Environment.MediaMounted &&
                    externalDirectory != null)
                {
                    return externalDirectory.AbsolutePath;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<string> SelectDirectory()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                var intent = new Intent(Intent.ActionOpenDocumentTree);
                MainActivity.Instance.StartActivityForResult(intent, (int)MainActivity.RequestCodes.BrowseDirectoriesRequestCode);
                _completionSource = new TaskCompletionSource<string>();
                return await _completionSource.Task;
            }

            return await Task.FromResult((string)null);
        }

        public static void DirectorySelected(Uri directory)
        {
            var contentResolver = MainActivity.Instance.ContentResolver;
            contentResolver.TakePersistableUriPermission(directory,
                ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
            _completionSource.SetResult(directory.ToString());
        }

        public static void DirectorySelectionCancelled()
        {
            _completionSource?.SetResult(null);
        }
    }
}