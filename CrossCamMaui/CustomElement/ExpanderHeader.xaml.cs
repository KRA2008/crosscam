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

        public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title),
            typeof(string), typeof(ExpanderHeader));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
    }
}