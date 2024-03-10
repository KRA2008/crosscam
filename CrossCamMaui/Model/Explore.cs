namespace CrossCam.Model
{
    public class Explore
    {
        public float Vertical { get; set; }
        public float Horizontal { get; set; }
        public float VerticalBase { get; set; }
        public float HorizontalBase { get; set; }
        public float Zoom { get; set; }

        public void Clear()
        {
            Vertical = 0;
            Horizontal = 0;
            VerticalBase = 0;
            HorizontalBase = 0;
            Zoom = 0;
        }
    }
}
