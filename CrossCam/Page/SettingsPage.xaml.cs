using System.ComponentModel;
using CrossCam.Model;
using CrossCam.ViewModel;
using Xamarin.Forms;

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
            if (BindingContext is SettingsViewModel viewModel)
            {
                _viewModel = viewModel;
                _viewModel.Settings.PropertyChanged += SettingsOnPropertyChanged;
                _viewModel.Settings.AlignmentSettings.PropertyChanged += AlignmentSettingsOnPropertyChanged;
            }
            else if (BindingContext == null && _viewModel != null)
            {
                _viewModel.Settings.PropertyChanged -= SettingsOnPropertyChanged;
                _viewModel.Settings.AlignmentSettings.PropertyChanged -= AlignmentSettingsOnPropertyChanged;
            }
        }

        private void AlignmentSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AlignmentSettings.ShowAdvancedAlignmentSettings) ||
                e.PropertyName == nameof(AlignmentSettings.IsAutomaticAlignmentOn))
            {
                _alignmentExpander.ForceUpdateSize();
            }
        }

        private void SettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.Mode) ||
                e.PropertyName == nameof(Settings.AddBarrelDistortion) ||
                e.PropertyName == nameof(Settings.CardboardDownsize))
            {
                //_viewModeExpander.ForceUpdateSize();
            } 
            else if (e.PropertyName == nameof(Settings.IsPairedPrimary))
            {
                _pairingExpander.ForceUpdateSize();
            } 
            else if (e.PropertyName == nameof(Settings.AddBorder2))
            {
                _borderExpander.ForceUpdateSize();
            }
            else if (e.PropertyName == nameof(Settings.AreGuideLinesVisible) ||
                     e.PropertyName == nameof(Settings.IsGuideDonutVisible))
            {
                _guidesExpander.ForceUpdateSize();
            }
        }
    }
}