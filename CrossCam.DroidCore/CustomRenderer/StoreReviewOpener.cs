using System.Threading.Tasks;
using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using Microsoft.Maui.Controls;

[assembly: Dependency(typeof(StoreReviewOpener))]
namespace CrossCam.Droid.CustomRenderer
{
    public class StoreReviewOpener : IStoreReviewOpener
    {
        private TaskCompletionSource<bool> _taskCompletionSource;

        public Task TryOpenStoreReview()
        {
            _taskCompletionSource = new TaskCompletionSource<bool>();
            MainActivity.Instance.RequestReview(_taskCompletionSource);
            return _taskCompletionSource.Task;
        }
    }
}