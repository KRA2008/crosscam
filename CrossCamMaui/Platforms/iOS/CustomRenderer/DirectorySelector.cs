using System.Threading.Tasks;
using CrossCam.iOS.CustomRenderer;
using CrossCam.Wrappers;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

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