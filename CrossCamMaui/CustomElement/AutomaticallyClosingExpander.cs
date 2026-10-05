using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using CrossCam.Model;
using System.ComponentModel;

namespace CrossCam.CustomElement;

public class AutomaticallyClosingExpander : Expander, IRecipient<ExpanderExpandedChangedMessage>
{
    public AutomaticallyClosingExpander()
    {
        PropertyChanged += OnPropertyChanged;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    protected void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsExpanded))
        {
            if (IsExpanded)
            {
                WeakReferenceMessenger.Default.Send(new ExpanderExpandedChangedMessage(this));
            }
        }
    }

    public void Receive(ExpanderExpandedChangedMessage message)
    {
        if (!ReferenceEquals(message.Value, this) &&
            IsExpanded)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsExpanded = false;
            });
        }
    }
}