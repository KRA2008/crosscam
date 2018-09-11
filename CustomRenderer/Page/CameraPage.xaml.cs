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

	    private void ReticlePanned(object sender, PanUpdatedEventArgs e)
	    {
            //TODO: handle boundaries

	        if (!_isInitialized || e.StatusType == GestureStatus.Completed)
	        {
	            var originalBounds = AbsoluteLayout.GetLayoutBounds(_leftReticle);
	            _originalAbsoluteLeftX = _leftReticle.X;
	            _originalAbsoluteY = _leftReticle.Y;
	            _originalAbsoluteRightX = _rightReticle.X;
	            _originalProportionalWidth = originalBounds.Width;
	            _isInitialized = true;
	        }

            AbsoluteLayout.SetLayoutFlags(_leftReticle, AbsoluteLayoutFlags.SizeProportional);
            AbsoluteLayout.SetLayoutBounds(_leftReticle, new Rectangle(
                _originalAbsoluteLeftX + e.TotalX,
                _originalAbsoluteY + e.TotalY,
                _originalProportionalWidth,
                _originalProportionalWidth));

	        AbsoluteLayout.SetLayoutFlags(_rightReticle, AbsoluteLayoutFlags.SizeProportional);
            AbsoluteLayout.SetLayoutBounds(_rightReticle, new Rectangle(
                _originalAbsoluteRightX + e.TotalX,
                _originalAbsoluteY + e.TotalY,
                _originalProportionalWidth,
                _originalProportionalWidth));
        }
	}
}