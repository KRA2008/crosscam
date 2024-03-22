using CrossCam.Wrappers;

namespace CrossCam.Platforms.Android.CustomRenderer
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