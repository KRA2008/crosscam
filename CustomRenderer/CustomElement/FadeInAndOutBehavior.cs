using System;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace CustomRenderer.CustomElement
{
    // ReSharper disable once UnusedMember.Global
    public sealed class FadeInAndOutBehavior : Behavior<VisualElement>
    {
        private const uint EASING_IN = 250;
        private const uint EASING_OUT = 250;
        private const int VISIBLE_TIME = 2000;

        private VisualElement _element;
        // ReSharper disable once MemberCanBePrivate.Global
        public static readonly BindableProperty TriggerProperty = BindableProperty.Create(nameof(Trigger), typeof(bool),
            typeof(FadeInAndOutBehavior), false, propertyChanged: OnTriggerChanged,
            defaultBindingMode: BindingMode.TwoWay);

        private static async void OnTriggerChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            var behavior = (FadeInAndOutBehavior)bindable;
            behavior._element.Opacity = 0;
            behavior._element.IsVisible = true;
            await behavior._element.FadeTo(1, EASING_IN, Easing.Linear);
            await Task.Delay(VISIBLE_TIME);
            await behavior._element.FadeTo(0, EASING_OUT, Easing.Linear);
            behavior._element.IsVisible = false;
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public bool Trigger
        {
            get => (bool)GetValue(TriggerProperty);
            set => SetValue(TriggerProperty, value);
        }

        protected override void OnAttachedTo(VisualElement element)
        {
            base.OnAttachedTo(element);
            _element = element;
            _element.Opacity = 0;
            _element.IsVisible = false;
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