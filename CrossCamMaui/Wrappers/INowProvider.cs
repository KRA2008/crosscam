namespace CrossCam.Wrappers
{
    public interface INowProvider
    {
        public DateTime UtcNow();
    }

    public class NowProvider : INowProvider {
        public DateTime UtcNow()
        {
            return DateTime.UtcNow;
        }
    }
}