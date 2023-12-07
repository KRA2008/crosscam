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
        builder.Services.Add(ServiceDescriptor.Transient<ContactViewModel, ContactViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<DirectionsViewModel, DirectionsViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<FaqViewModel, FaqViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<HamburgerViewModel, HamburgerViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<HelpViewModel, HelpViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<MoreImagesViewModel, MoreImagesViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<PairingViewModel, PairingViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<SeeMorePicturesViewModel, SeeMorePicturesViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<SettingsViewModel, SettingsViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<TechniqueHelpViewModel, TechniqueHelpViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<TipMeViewModel, TipMeViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<TipsViewModel, TipsViewModel>());

        var app = builder.Build();
        app.UseFreshMvvm();
        return app;
    }
}
