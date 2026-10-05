using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CrossCam.Model;

public class ExpanderExpandedChangedMessage : ValueChangedMessage<Expander>
{
    public ExpanderExpandedChangedMessage(Expander value) : base(value) { }
}