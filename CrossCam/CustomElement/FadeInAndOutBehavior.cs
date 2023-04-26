using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AppCenter.Crashes;
using Xamarin.Forms;

namespace CrossCam.CustomElement
{
    // ReSharper disable once UnusedMember.Global
    public sealed class FadeInAndOutBehavior : Behavior<VisualElement>
    {
        private VisualElement _element;
        private Task _fadingTask;
        private CancellationTokenSource _cancellationTokenSource;

        public static readonly BindableProperty TriggerProperty = BindableProperty.Create(nameof(Trigger), typeof(bool),
            typeof(FadeInAndOutBehavior), false, propertyChanged: OnTriggerChanged,
            defaultBindingMode: BindingMode.TwoWay);

        public static readonly BindableProperty InTimeMsProperty = BindableProperty.Create(nameof(InTimeMs), typeof(uint),
            typeof(FadeInAndOutBehavior), (uint)1000, BindingMode.TwoWay);

        public static readonly BindableProperty OutTimeMsProperty = BindableProperty.Create(nameof(OutTimeMs), typeof(uint),
            typeof(FadeInAndOutBehavior), (uint)1000, BindingMode.TwoWay);

        public static readonly BindableProperty VisibleTimeMsProperty = BindableProperty.Create(nameof(VisibleTimeMs), typeof(int),
            typeof(FadeInAndOutBehavior), 2000, BindingMode.TwoWay);


        private static async void OnTriggerChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            await Device.InvokeOnMainThreadAsync(async () =>
            {
                var behavior = (FadeInAndOutBehavior) bindable;
                if (behavior._fadingTask?.IsCompleted == false)
                {
                    behavior._cancellationTokenSource?.Cancel();
                    behavior._element?.CancelAnimations();
                }

                behavior._cancellationTokenSource = new CancellationTokenSource();
                var token = behavior._cancellationTokenSource.Token;

                try
                {
                    behavior._fadingTask = Task.Run(async () =>
                    {
                        await Device.InvokeOnMainThreadAsync(async () =>
                        {
                            behavior._element.Opacity = 0;
                            behavior._element.IsVisible = true;
                            await behavior._element.FadeTo(1, behavior.InTimeMs, Easing.Linear);
                            await Task.Delay(behavior.VisibleTimeMs, token);
                            await behavior._element.FadeTo(0, behavior.OutTimeMs, Easing.Linear);
                            behavior._element.IsVisible = false;
                        });
                    }, token);
                    await behavior._fadingTask;
                }
                catch (Exception ex)
                {
                    if (ex is TaskCanceledException)
                    {
                        //Debug.WriteLine("### fade task cancelled: "+ex);
                    }
                    else
                    {
                        Crashes.TrackError(ex);
                    }
                }
            });
        }
        
        public bool Trigger
        {
            get => (bool)GetValue(TriggerProperty);
            set => SetValue(TriggerProperty, value);
        }

        public uint InTimeMs
        {
            get => (uint)GetValue(InTimeMsProperty);
            set => SetValue(InTimeMsProperty, value);
        }

        public uint OutTimeMs
        {
            get => (uint)GetValue(OutTimeMsProperty);
            set => SetValue(OutTimeMsProperty, value);
        }

        public int VisibleTimeMs
        {
            get => (int)GetValue(VisibleTimeMsProperty);
            set => SetValue(VisibleTimeMsProperty, value);
        }

        protected override void OnAttachedTo(VisualElement element)
        {
            base.OnAttachedTo(element);
            _element = element;
            Device.BeginInvokeOnMainThread(() =>
            {
                _element.Opacity = 0;
                _element.IsVisible = false;
            });
            _element.BindingContextChanged += SetBindingContext;
        }

        protected override void OnDetachingFrom(VisualElement element)
        {
            base.OnDetachingFrom(element);
            _element.BindingContextChanged -= SetBindingContext;
            _element = null;
            BindingContext = null;
        }

        private void SetBindingContext(object sender, EventArgs e)
        {
            BindingContext = _element.BindingContext;
        }
    }
}