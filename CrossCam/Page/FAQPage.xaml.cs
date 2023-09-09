﻿using CrossCam.Model;
using CrossCam.ViewModel;
using Xamarin.CommunityToolkit.UI.Views;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace CrossCam.Page
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class FaqPage
    {
        public FaqPage()
        {
            InitializeComponent();
        }

        private FaqViewModel _viewModel;

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is FaqViewModel viewModel)
            {
                _viewModel = viewModel;
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            HandleRequestedScrollOption();
        }

        private void HandleRequestedScrollOption()
        {
            if (_viewModel.RequestedScrollOption != 0)
            {
                BoxView scrollLine = null;
                Expander scrollExpander = null;
                switch (_viewModel.RequestedScrollOption)
                {
                    case FaqScrollOptions.CrossParallel:
                        scrollLine = _crossParallelLine;
                        scrollExpander = _crossParallelExpander;
                        break;
                    case FaqScrollOptions.Cardboard:
                        scrollLine = _cardboardLine;
                        scrollExpander = _cardboardExpander;
                        break;
                    case FaqScrollOptions.Mirror:
                        scrollLine = _mirrorLine;
                        scrollExpander = _mirrorExpander;
                        break;
                }

                if (scrollExpander != null &&
                    scrollLine != null)
                {
                    ExpandExpanderAndScrollToLine(scrollExpander, scrollLine);
                }
            }
        }

        private async void ExpandExpanderAndScrollToLine(Expander scrollTarget, BoxView line)
        {
            scrollTarget.IsExpanded = true;
            await _scrollView.ScrollToAsync(line, ScrollToPosition.Start, true);
        }
    }
}