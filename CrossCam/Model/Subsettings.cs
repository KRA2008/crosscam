using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CrossCam.Model
{
    public abstract class Subsettings : INotifyPropertyChanged
    {
        public abstract void ResetToDefaults();
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}