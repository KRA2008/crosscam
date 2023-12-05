namespace CrossCam.Model
{
    public enum TransformationFindingMethod
    {
        BinarySearch,
        FindHomography,
        EstimateRigidPartial,
        EstimateRigidFull,
        StereoRectifyUncalibrated
    }
}