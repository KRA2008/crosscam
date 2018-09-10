using Xamarin.Forms;

namespace CustomRenderer.Page
{
    // ReSharper disable once UnusedMember.Global
	public partial class CameraPage
	{
	    public CameraPage()
		{
            InitializeComponent();
		    NavigationPage.SetHasNavigationBar(this, false);
        }

	    private bool _isInitialized;
	    private double _originalAbsoluteLeftX;
	    private double _originalAbsoluteRightX;
	    private double _originalAbsoluteY;
	    private double _originalProportionalWidth;

	    private void CrossHairPanned(object sender, PanUpdatedEventArgs e)
	    {
            //TODO: handle boundaries

	        if (!_isInitialized || e.StatusType == GestureStatus.Completed)
	        {
	            var originalBounds = AbsoluteLayout.GetLayoutBounds(_leftCrossHair);
	            _originalAbsoluteLeftX = _leftCrossHair.X;
	            _originalAbsoluteY = _leftCrossHair.Y;
	            _originalAbsoluteRightX = _rightCrossHair.X;
	            _originalProportionalWidth = originalBounds.Width;
	            _isInitialized = true;
	        }

            AbsoluteLayout.SetLayoutFlags(_leftCrossHair, AbsoluteLayoutFlags.SizeProportional);
            AbsoluteLayout.SetLayoutBounds(_leftCrossHair, new Rectangle(
                _originalAbsoluteLeftX + e.TotalX,
                _originalAbsoluteY + e.TotalY,
                _originalProportionalWidth,
                _originalProportionalWidth));

	        AbsoluteLayout.SetLayoutFlags(_rightCrossHair, AbsoluteLayoutFlags.SizeProportional);
            AbsoluteLayout.SetLayoutBounds(_rightCrossHair, new Rectangle(
                _originalAbsoluteRightX + e.TotalX,
                _originalAbsoluteY + e.TotalY,
                _originalProportionalWidth,
                _originalProportionalWidth));
        }
	}
}