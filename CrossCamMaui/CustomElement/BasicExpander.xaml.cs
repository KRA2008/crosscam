namespace CrossCam.CustomElement
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class BasicExpander
    {
        public BasicExpander()
        {
            InitializeComponent();
            BindingContext = this;
        }

        public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title),
            typeof(string), typeof(BasicExpander));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text),
            typeof(string), typeof(BasicExpander));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }
    }
}