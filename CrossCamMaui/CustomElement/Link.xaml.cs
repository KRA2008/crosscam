namespace CrossCam.CustomElement;

public partial class Link
{
    public Link()
    {
        InitializeComponent();
    }

    private async void TapGestureRecognizer_OnTapped(object sender, TappedEventArgs e)
    {
        await Browser.OpenAsync(new Uri(Text));
    }
}