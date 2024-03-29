﻿using CrossCam.Wrappers;

namespace CrossCam.Platforms.iOS.CustomRenderer
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