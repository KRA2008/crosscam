using CommunityToolkit.Maui;
using CrossCam.ViewModel;
using FreshMvvm.Maui.Extensions;

namespace CrossCam;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiCommunityToolkit()
            .UseMauiApp<App>();

        builder.Services.Add(ServiceDescriptor.Singleton<CameraViewModel,CameraViewModel>());

        var app = builder.Build();
        app.UseFreshMvvm();
        return app;
    }
}
