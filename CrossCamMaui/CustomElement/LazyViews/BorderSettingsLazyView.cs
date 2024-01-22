using CommunityToolkit.Maui.Views;

namespace CrossCam.CustomElement.LazyViews
{
    public class BorderSettingsLazyView : LazyView<BorderSettingsView>
    {
        public override async ValueTask LoadViewAsync(CancellationToken token)
        {
            await base.LoadViewAsync(token);
        }
    };
}
