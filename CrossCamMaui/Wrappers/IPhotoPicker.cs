namespace CrossCam.Wrappers
{
    public interface IPhotoPicker
    {
        Task<byte[][]> GetImages();
    }
}