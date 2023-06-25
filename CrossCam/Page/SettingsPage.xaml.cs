using System;
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
            if (BindingContext is SettingsViewModel viewModel)
            {
                _viewModel = viewModel;
                _viewModel.Settings.PropertyChanged += SettingsOnPropertyChanged;
                _viewModel.Settings.AlignmentSettings.PropertyChanged += AlignmentSettingsOnPropertyChanged;
                _viewModel.Settings.PairSettings.PropertyChanged += PairSettingsOnPropertyChanged;
                _viewModel.Settings.CardboardSettings.PropertyChanged += CardboardSettingsOnPropertyChanged;
                _viewModel.Settings.EditsSettings.PropertyChanged += EditsSettingsOnPropertyChanged;
                _alignmentExpander.Tapped += OnExpanderOnTapped;
                _borderExpander.Tapped += OnExpanderOnTapped;
                _cameraExpander.Tapped += OnExpanderOnTapped;
                _editingExpander.Tapped += OnExpanderOnTapped;
                _guidesExpander.Tapped += OnExpanderOnTapped;
                _pairingExpander.Tapped += OnExpanderOnTapped;
                _savingExpander.Tapped += OnExpanderOnTapped;
            }
            else if (BindingContext == null && _viewModel != null)
            {
                _viewModel.Settings.PropertyChanged -= SettingsOnPropertyChanged;
                _viewModel.Settings.AlignmentSettings.PropertyChanged -= AlignmentSettingsOnPropertyChanged;
                _viewModel.Settings.PairSettings.PropertyChanged -= PairSettingsOnPropertyChanged;
                _viewModel.Settings.CardboardSettings.PropertyChanged -= CardboardSettingsOnPropertyChanged;
                _viewModel.Settings.EditsSettings.PropertyChanged -= EditsSettingsOnPropertyChanged;
                _alignmentExpander.Tapped -= OnExpanderOnTapped;
                _borderExpander.Tapped -= OnExpanderOnTapped;
                _cameraExpander.Tapped -= OnExpanderOnTapped;
                _editingExpander.Tapped -= OnExpanderOnTapped;
                _guidesExpander.Tapped -= OnExpanderOnTapped;
                _pairingExpander.Tapped -= OnExpanderOnTapped;
                _savingExpander.Tapped -= OnExpanderOnTapped;
            }
        }

        private void OnExpanderOnTapped(object sender, EventArgs e)
        {
            if (sender != _alignmentExpander)
            {
                _alignmentExpander.IsExpanded = false;
            }

            if (sender != _borderExpander)
            {
                _borderExpander.IsExpanded = false;
            }

            if (sender != _cameraExpander)
            {
                _cameraExpander.IsExpanded = false;
            }

            if (sender != _editingExpander)
            {
                _editingExpander.IsExpanded = false;
            }

            if (sender != _guidesExpander)
            {
                _guidesExpander.IsExpanded = false;
            }

            if (sender != _pairingExpander)
            {
                _pairingExpander.IsExpanded = false;
            }

            if (sender != _savingExpander)
            {
                _savingExpander.IsExpanded = false;
            }
        }

        private void EditsSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
        }

        private void CardboardSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CardboardSettings.AddBarrelDistortion) ||
                e.PropertyName == nameof(CardboardSettings.CardboardDownsize))
            {
                //
            }
        }

        private void PairSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PairSettings.IsPairedPrimary))
            {
                _pairingExpander.ForceUpdateSize();
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
            if (e.PropertyName == nameof(Settings.Mode))
            {
                //_viewModeExpander.ForceUpdateSize();
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