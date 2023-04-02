using System.Threading.Tasks;

namespace CrossCam.Wrappers
{
    public interface IStoreReviewOpener
    {
        public Task TryOpenStoreReview();
    }
}