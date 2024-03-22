using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;

namespace CrossCam.Page
{
	public partial class SettingsPage
    {
		public SettingsPage ()
		{
			InitializeComponent ();
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
            }
        }
    }
}