using CommunityToolkit.Maui.Views;

namespace CrossCam.CustomElement.LazyViews
{
    public class AlignmentSettingsLazyView : LazyView<AlignmentSettingsView>
    {
        public override async ValueTask LoadViewAsync(CancellationToken token)
        {
            await base.LoadViewAsync(token);
        }
    };
}
