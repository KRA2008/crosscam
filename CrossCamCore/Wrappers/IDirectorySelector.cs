namespace CrossCam.Wrappers
{
    public interface IDirectorySelector
    {
        string GetExternalSaveDirectory();
        bool CanSaveToArbitraryDirectory();
        Task<string> SelectDirectory();
    }
}