using CommunityToolkit.Maui.Views;

namespace CrossCam.CustomElement.LazyViews
{
    public class EditingSettingsLazyView : LazyView<EditingSettingsView>
    {
        public override async ValueTask LoadViewAsync(CancellationToken token)
        {
            await base.LoadViewAsync(token);
        }
    };
}
