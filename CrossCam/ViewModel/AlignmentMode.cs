namespace CrossCam.ViewModel
{
    public enum AlignmentMode
    {
        None,
        EccDiscardX,
        EccKeepX,
        KeypointDiscardX,
        KeypointKeepX,
        // Other? triggers a redo when settings are switched... but are these settings
    }
}