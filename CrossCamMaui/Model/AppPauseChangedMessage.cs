using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CrossCam.Model;

public class AppPauseChangedMessage : ValueChangedMessage<bool>
{
    public AppPauseChangedMessage(bool isPaused) : base(isPaused) { }
}