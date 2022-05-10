﻿using System.Threading.Tasks;

namespace CrossCam.Wrappers
{
    public interface IPhotoSaver
    {
        Task<bool> SavePhoto(byte[] image, string saveOuterFolder, string saveInnerFolder, bool saveToSd);
    }
}