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
        
	    private double _reticleLeftX;
	    private double _reticleRightX;
	    private double _reticleY;
	    private double _reticleWidth;
        
	    private double _upperLineY;

	    private bool _isLowerLineInitialized;
	    private double _lowerLineY;

	    private void ReticlePanned(object sender, PanUpdatedEventArgs e)
	    {
	        if (e.StatusType == GestureStatus.Completed || e.StatusType == GestureStatus.Started)
	        {
	            var originalBounds = AbsoluteLayout.GetLayoutBounds(_leftReticle);
	            _reticleLeftX = _leftReticle.X;
	            _reticleY = _leftReticle.Y;
	            _reticleRightX = _rightReticle.X;
	            _reticleWidth = originalBounds.Width;
	            if (AbsoluteLayout.GetLayoutFlags(_leftReticle) != AbsoluteLayoutFlags.SizeProportional)
	            {
	                AbsoluteLayout.SetLayoutFlags(_leftReticle, AbsoluteLayoutFlags.SizeProportional);
	                AbsoluteLayout.SetLayoutFlags(_rightReticle, AbsoluteLayoutFlags.SizeProportional);
                }
            }

            AbsoluteLayout.SetLayoutBounds(_leftReticle, new Rectangle(
                _reticleLeftX + e.TotalX,
                _reticleY + e.TotalY,
                _reticleWidth,
                _reticleWidth));

            AbsoluteLayout.SetLayoutBounds(_rightReticle, new Rectangle(
                _reticleRightX + e.TotalX,
                _reticleY + e.TotalY,
                _reticleWidth,
                _reticleWidth));
        }

	    private void UpperLinePanned(object sender, PanUpdatedEventArgs e)
	    {
	        if (e.StatusType == GestureStatus.Completed || e.StatusType == GestureStatus.Started)
	        {
	            _upperLineY = _upperLine.Y;
	            if (AbsoluteLayout.GetLayoutFlags(_upperLine) != AbsoluteLayoutFlags.WidthProportional)
	            {
	                AbsoluteLayout.SetLayoutFlags(_upperLine, AbsoluteLayoutFlags.WidthProportional);
                }
            }

            AbsoluteLayout.SetLayoutBounds(_upperLine, new Rectangle(
                0,
                _upperLineY + e.TotalY,
                1,
                _upperLine.Height));
	    }

	    private void LowerLinePanned(object sender, PanUpdatedEventArgs e)
	    {
	        if (e.StatusType == GestureStatus.Completed || e.StatusType == GestureStatus.Started)
	        {
	            _lowerLineY = _lowerLine.Y;
	            if (AbsoluteLayout.GetLayoutFlags(_lowerLine) != AbsoluteLayoutFlags.WidthProportional)
	            {
	                AbsoluteLayout.SetLayoutFlags(_lowerLine, AbsoluteLayoutFlags.WidthProportional);
                }
	        }

	        AbsoluteLayout.SetLayoutBounds(_lowerLine, new Rectangle(
	            0,
	            _lowerLineY + e.TotalY,
	            1,
	            _lowerLine.Height));
        }
	}
}