﻿using Android.Content;
using CrossCam.Wrappers;

namespace CrossCam.Platforms.Android.CustomRenderer
{
    public class PhotoPicker : IPhotoPicker
    {
        public Task<byte[][]> GetImages()
        {
            // Define the Intent for getting images
            var intent = new Intent();
            intent.SetType("image/*");
            intent.SetAction(Intent.ActionGetContent);
            intent.PutExtra(Intent.ExtraAllowMultiple, true);

            // Start the picture-picker activity (resumes in MainActivity.cs)
            MainActivity.Instance.StartActivityForResult(
                Intent.CreateChooser(intent, "Select Picture"),
                MainActivity.PICK_PHOTO_ID);

            // Save the TaskCompletionSource object as a MainActivity property
            MainActivity.Instance.PickPhotoTaskCompletionSource = new TaskCompletionSource<byte[][]>();

            // Return Task object
            return MainActivity.Instance.PickPhotoTaskCompletionSource.Task;
        }
    }
}