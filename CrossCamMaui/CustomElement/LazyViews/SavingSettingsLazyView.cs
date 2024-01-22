using CommunityToolkit.Maui.Views;

namespace CrossCam.CustomElement.LazyViews
{
    public class SavingSettingsLazyView : LazyView<SavingSettingsView>
    {
        public override async ValueTask LoadViewAsync(CancellationToken token)
        {
            await base.LoadViewAsync(token);
        }
    };
}
