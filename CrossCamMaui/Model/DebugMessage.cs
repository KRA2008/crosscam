using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CrossCam.Model;

public class DebugMessage : ValueChangedMessage<string>
{
    public DebugMessage(string message) : base(message) { }
}