namespace CrossCam.Wrappers
{
    public interface IDevice
    {
        public Task InvokeOnMainThreadAsync(Func<Task> funcTask);
    }

    public class CrossCamDevice : IDevice
    {
        public async Task InvokeOnMainThreadAsync(Func<Task> funcTask)
        {
            await Device.InvokeOnMainThreadAsync(funcTask);
        }
    }
}