namespace CrossCam.Wrappers
{
    public interface IPhotoSaver
    {
        Task<bool> SavePhoto(byte[] image, string saveOuterFolder, string saveInnerFolder, string viewMethod, bool saveToSd);
    }
}