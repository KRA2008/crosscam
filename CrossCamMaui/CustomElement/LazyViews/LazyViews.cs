using CommunityToolkit.Maui.Views;

namespace CrossCam.CustomElement.LazyViews
{
    public abstract class MyLazyView<T> : LazyView<T> where T : View, new()
    {
        public override async ValueTask LoadViewAsync(CancellationToken token)
        {
            await base.LoadViewAsync(token);
        }
    }

    public class AlignmentSettingsLazyView : MyLazyView<AlignmentSettingsView>;
    public class BorderSettingsLazyView : MyLazyView<BorderSettingsView>;
    public class CameraSettingsLazyView : MyLazyView<CameraSettingsView>;
    public class CaptureMethodSettingsLazyView : MyLazyView<CaptureMethodSettingsView>;
    public class EditingSettingsLazyView : MyLazyView<EditingSettingsView>;
    public class GuidesSettingsLazyView : MyLazyView<GuidesSettingsView>;
    public class PairSettingsLazyView : MyLazyView<PairSettingsView>;
    public class PreviewMethodSettingsLazyView : MyLazyView<PreviewMethodSettingsView>;
    public class SavingSettingsLazyView : MyLazyView<SavingSettingsView>;
}
