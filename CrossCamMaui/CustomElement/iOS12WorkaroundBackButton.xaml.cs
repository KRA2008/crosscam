namespace CrossCam.CustomElement;

public partial class iOS12WorkaroundBackButton : ContentView
{
	public iOS12WorkaroundBackButton()
	{
		InitializeComponent();
#if __ANDROID__
#elif __IOS__
        if (DeviceInfo.Version.Major > 12)
        {
            IsVisible = false;
        }
#endif
    }

    private async void Button_OnClicked(object sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Navigation.PopAsync();
        });
    }
}