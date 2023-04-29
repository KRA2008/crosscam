using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace CrossCam.CustomElement
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ExpanderHeader
    {
        public ExpanderHeader()
        {
            InitializeComponent();
            BindingContext = this;
        }

        public static readonly BindableProperty HeaderTitleProperty = BindableProperty.Create(nameof(HeaderTitle),
            typeof(string), typeof(ExpanderHeader));

        public string HeaderTitle
        {
            get => (string)GetValue(HeaderTitleProperty);
            set => SetValue(HeaderTitleProperty, value);
        }
    }
}