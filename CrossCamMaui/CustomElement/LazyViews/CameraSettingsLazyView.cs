using CommunityToolkit.Maui.Views;

namespace CrossCam.CustomElement.LazyViews
{
    public class CameraSettingsLazyView : LazyView<CameraSettingsView>
    {
        public override async ValueTask LoadViewAsync(CancellationToken token)
        {
            await base.LoadViewAsync(token);
        }
    };
}
