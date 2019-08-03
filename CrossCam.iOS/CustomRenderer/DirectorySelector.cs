using System.Threading.Tasks;
using CrossCam.iOS.CustomRenderer;
using CrossCam.Wrappers;
using Xamarin.Forms;

[assembly: Dependency(typeof(DirectorySelector))]
namespace CrossCam.iOS.CustomRenderer
{
    public class DirectorySelector : IDirectorySelector
    {
        public bool CanSaveToArbitraryDirectory()
        {
            return false;
        }

        public string GetExternalSaveDirectory()
        {
            return null;
        }

        public async Task<string> SelectDirectory()
        {
            return await Task.FromResult((string)null);
        }
    }
}