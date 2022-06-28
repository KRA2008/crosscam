using System.ComponentModel;
using CrossCam.Model;
using CrossCam.ViewModel;

namespace CrossCam.Page
{
	public partial class SettingsPage
    {
        private SettingsViewModel _viewModel;

		public SettingsPage ()
		{
			InitializeComponent ();
		}

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            _viewModel = BindingContext as SettingsViewModel;
            _viewModel.Settings.PropertyChanged += SettingsOnPropertyChanged;
            _viewModel.Settings.AlignmentSettings.PropertyChanged += AlignmentSettingsOnPropertyChanged;
        }

        private void AlignmentSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AlignmentSettings.ShowAdvancedAlignmentSettings))
            {
                _alignmentExpander.ForceUpdateSize();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.Settings.PropertyChanged -= SettingsOnPropertyChanged;
            _viewModel.Settings.AlignmentSettings.PropertyChanged -= AlignmentSettingsOnPropertyChanged;
        }

        private void SettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.Mode))
            {
                _viewModeExpander.ForceUpdateSize();
            }

            if (e.PropertyName == nameof(Settings.IsPairedPrimary))
            {
                _pairingExpander.ForceUpdateSize();
            }
        }
    }
}