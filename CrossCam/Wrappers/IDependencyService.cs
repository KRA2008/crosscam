namespace CrossCam.Wrappers
{
    public interface IDependencyService
    {
        public T Get<T>() where T : class;
    }

    public class CrossCamDependencyService : IDependencyService
    {
        public T Get<T>() where T : class
        {
            return Xamarin.Forms.DependencyService.Get<T>();
        }
    }
}