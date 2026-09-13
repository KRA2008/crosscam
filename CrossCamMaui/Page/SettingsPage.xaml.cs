using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;

namespace CrossCam.Page
{
	public partial class SettingsPage
    {
        private List<Expander> expanders;

        public SettingsPage ()
		{
			InitializeComponent ();
            expanders =
            [
                _previewMethodExpander,
                _pairingExpander,
                _alignmentExpander,
                _savingExpander,
                _borderExpander,
                _guidesExpander,
                _editingExpander,
                _cameraExpander
            ];
        }

        

        private async void ExpanderChanged(object sender, ExpandedChangedEventArgs e)
        {
            var expander = (Expander)sender;
            if (expander.Content is LazyView lazyView)
            {
                if (expander.IsExpanded &&
                    !lazyView.HasLazyViewLoaded)
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    await lazyView.LoadViewAsync(cts.Token);
                }

                if (!expander.IsExpanded) return;

                foreach (var expanderToClose in expanders)
                {
                    if (expanderToClose != expander)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            expanderToClose.IsExpanded = false;
                        });
                    }
                }
            }
        }
    }
}