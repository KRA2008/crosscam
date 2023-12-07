using System.ComponentModel;
using CrossCam.Model;
using CrossCam.ViewModel;
using CommunityToolkit.Maui.Views;

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
                _viewModel.Settings.PairSettings.PropertyChanged += PairSettingsOnPropertyChanged;
                _viewModel.Settings.CardboardSettings.PropertyChanged += CardboardSettingsOnPropertyChanged;
                _viewModel.Settings.EditsSettings.PropertyChanged += EditsSettingsOnPropertyChanged;
            }
            else if (BindingContext == null && _viewModel != null)
            {
                _viewModel.Settings.PropertyChanged -= SettingsOnPropertyChanged;
                _viewModel.Settings.AlignmentSettings.PropertyChanged -= AlignmentSettingsOnPropertyChanged;
                _viewModel.Settings.PairSettings.PropertyChanged -= PairSettingsOnPropertyChanged;
                _viewModel.Settings.CardboardSettings.PropertyChanged -= CardboardSettingsOnPropertyChanged;
                _viewModel.Settings.EditsSettings.PropertyChanged -= EditsSettingsOnPropertyChanged;
            }
        }

        private void EditsSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
        }

        private async void CardboardSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await Task.Delay(1);
            if (e.PropertyName == nameof(CardboardSettings.AddBarrelDistortion) ||
                e.PropertyName == nameof(CardboardSettings.CardboardDownsize))
            {
                //_previewMethodExpander.ForceUpdateSize();  //TODO: needed?
            }
        }

        private async void PairSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await Task.Delay(1);
            if (e.PropertyName == nameof(PairSettings.IsPairedPrimary))
            {
                //_pairingExpander.ForceUpdateSize(); //TODO: needed?
            }
        }

        private async void AlignmentSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await Task.Delay(1);
            if (e.PropertyName == nameof(AlignmentSettings.ShowAdvancedAlignmentSettings) ||
                e.PropertyName == nameof(AlignmentSettings.IsAutomaticAlignmentOn))
            {
                //_alignmentExpander.ForceUpdateSize(); //TODO: needed?
            }
        }

        private async void SettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Expander expander = null;
            switch (e.PropertyName)
            {
                case nameof(Settings.Mode):
                    expander = _previewMethodExpander;
                    break;
                case nameof(Settings.AddBorder2):
                    expander = _borderExpander;
                    break;
                case nameof(Settings.AreGuideLinesVisible):
                case nameof(Settings.IsGuideDonutVisible):
                    expander = _guidesExpander;
                    break;
                case nameof(Settings.SavingDirectory):
                case nameof(Settings.SaveToExternal):
                    expander = _savingExpander;
                    break;
            }

            if (expander != null)
            {
                await Task.Delay(1);
                //expander.ForceUpdateSize(); //TODO: needed?
            }
        }
    }
}