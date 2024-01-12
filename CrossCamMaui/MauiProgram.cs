using CommunityToolkit.Maui;
using CrossCam.Page;
using CrossCam.CustomElement;
#if __ANDROID__
using CrossCam.Platforms.Android.CustomRenderer;
#elif __IOS__
using CrossCam.Platforms.iOS.CustomRenderer;
#endif
using CrossCam.ViewModel;
using CrossCam.Wrappers;
using FreshMvvm.Maui.Extensions;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace CrossCam;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiCommunityToolkit()
            .UseSkiaSharp()
            .UseMauiApp<App>();

        builder.ConfigureMauiHandlers(handlers =>
        {
#if __ANDROID__
            handlers.AddHandler<CameraModule, CameraModuleRenderer>();
            //handlers.AddHandler<Picker, HandsomePickerRenderer>();
#elif __IOS__
            handlers.AddHandler<CameraModule, CameraModuleRenderer>();
#endif
        });

        var services = builder.Services;
        services.Add(ServiceDescriptor.Singleton<CameraPage, CameraPage>());
        services.Add(ServiceDescriptor.Transient<ContactPage, ContactPage>());
        services.Add(ServiceDescriptor.Transient<DirectionsPage, DirectionsPage>());
        services.Add(ServiceDescriptor.Transient<FaqPage, FaqPage>());
        services.Add(ServiceDescriptor.Transient<HamburgerPage, HamburgerPage>());
        services.Add(ServiceDescriptor.Transient<HelpPage, HelpPage>());
        services.Add(ServiceDescriptor.Transient<PairingPage, PairingPage>());
        services.Add(ServiceDescriptor.Transient<SeeMorePicturesPage, SeeMorePicturesPage>());
        services.Add(ServiceDescriptor.Transient<SettingsPage, SettingsPage>());
        services.Add(ServiceDescriptor.Transient<TechniqueHelpPage, TechniqueHelpPage>());
        services.Add(ServiceDescriptor.Transient<TipMePage, TipMePage>());
        services.Add(ServiceDescriptor.Transient<TipsPage, TipsPage>());

        services.Add(ServiceDescriptor.Singleton<CameraViewModel,CameraViewModel>());
        services.Add(ServiceDescriptor.Transient<ContactViewModel, ContactViewModel>());
        services.Add(ServiceDescriptor.Transient<DirectionsViewModel, DirectionsViewModel>());
        services.Add(ServiceDescriptor.Transient<FaqViewModel, FaqViewModel>());
        services.Add(ServiceDescriptor.Transient<HamburgerViewModel, HamburgerViewModel>());
        services.Add(ServiceDescriptor.Transient<HelpViewModel, HelpViewModel>());
        services.Add(ServiceDescriptor.Transient<PairingViewModel, PairingViewModel>());
        services.Add(ServiceDescriptor.Transient<SeeMorePicturesViewModel, SeeMorePicturesViewModel>());
        services.Add(ServiceDescriptor.Transient<SettingsViewModel, SettingsViewModel>());
        services.Add(ServiceDescriptor.Transient<TechniqueHelpViewModel, TechniqueHelpViewModel>());
        services.Add(ServiceDescriptor.Transient<TipMeViewModel, TipMeViewModel>());
        services.Add(ServiceDescriptor.Transient<TipsViewModel, TipsViewModel>());

        DependencyService.Register<IPlatformPair, PlatformPair>();
        DependencyService.Register<IDirectorySelector, DirectorySelector>();
        DependencyService.Register<ILinkSharer, LinkSharer>();
        DependencyService.Register<IPhotoPicker, PhotoPicker>();
        DependencyService.Register<IScreenKeepAwaker, ScreenKeepAwaker>();
#if __ANDROID__
#elif __IOS__
        DependencyService.Register<INotchHeightProvider, NotchHeightProvider>();
#endif

        var app = builder.Build();
        app.UseFreshMvvm();
        return app;
    }
}
