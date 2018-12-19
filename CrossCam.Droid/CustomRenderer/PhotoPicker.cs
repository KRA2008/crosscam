using System.Threading.Tasks;
using Android.Content;
using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using Xamarin.Forms;

[assembly: Dependency(typeof(PhotoPicker))]

namespace CrossCam.Droid.CustomRenderer
{
    public class PhotoPicker : IPhotoPicker
    {
        public Task<byte[]> GetImage()
        {
            // Define the Intent for getting images
            var intent = new Intent();
            intent.SetType("image/*");
            intent.SetAction(Intent.ActionGetContent);

            // Start the picture-picker activity (resumes in MainActivity.cs)
            MainActivity.Instance.StartActivityForResult(
                Intent.CreateChooser(intent, "Select Picture"),
                MainActivity.PICK_PHOTO_ID);

            // Save the TaskCompletionSource object as a MainActivity property
            MainActivity.Instance.PickPhotoTaskCompletionSource = new TaskCompletionSource<byte[]>();

            // Return Task object
            return MainActivity.Instance.PickPhotoTaskCompletionSource.Task;
        }
    }
}