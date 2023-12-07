using CommunityToolkit.Maui;
using CrossCam.CustomElement;
using CrossCam.Page;
#if __ANDROID__
using CrossCam.Platforms.Android.CustomRenderer;
#elif __IOS__
using CrossCam.iOS.CustomRenderer;
#endif
using CrossCam.ViewModel;
using CrossCam.Wrappers;
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

        builder.Services.Add(ServiceDescriptor.Singleton<CameraPage, CameraPage>());
        builder.Services.Add(ServiceDescriptor.Transient<ContactPage, ContactPage>());
        builder.Services.Add(ServiceDescriptor.Transient<DirectionsPage, DirectionsPage>());
        builder.Services.Add(ServiceDescriptor.Transient<FaqPage, FaqPage>());
        builder.Services.Add(ServiceDescriptor.Transient<HamburgerPage, HamburgerPage>());
        builder.Services.Add(ServiceDescriptor.Transient<HelpPage, HelpPage>());
        builder.Services.Add(ServiceDescriptor.Transient<PairingPage, PairingPage>());
        builder.Services.Add(ServiceDescriptor.Transient<SeeMorePicturesPage, SeeMorePicturesPage>());
        builder.Services.Add(ServiceDescriptor.Transient<SettingsPage, SettingsPage>());
        builder.Services.Add(ServiceDescriptor.Transient<TechniqueHelpPage, TechniqueHelpPage>());
        builder.Services.Add(ServiceDescriptor.Transient<TipMePage, TipMePage>());
        builder.Services.Add(ServiceDescriptor.Transient<TipsPage, TipsPage>());

        builder.Services.Add(ServiceDescriptor.Singleton<CameraViewModel,CameraViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<ContactViewModel, ContactViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<DirectionsViewModel, DirectionsViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<FaqViewModel, FaqViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<HamburgerViewModel, HamburgerViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<HelpViewModel, HelpViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<PairingViewModel, PairingViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<SeeMorePicturesViewModel, SeeMorePicturesViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<SettingsViewModel, SettingsViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<TechniqueHelpViewModel, TechniqueHelpViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<TipMeViewModel, TipMeViewModel>());
        builder.Services.Add(ServiceDescriptor.Transient<TipsViewModel, TipsViewModel>());
#if __ANDROID__
        DependencyService.Register<IPlatformPair, PlatformPair>();
        DependencyService.Register<IDirectorySelector, DirectorySelector>();
        DependencyService.Register<ILinkSharer, LinkSharer>();
        DependencyService.Register<IPhotoPicker, PhotoPicker>();
        DependencyService.Register<IScreenKeepAwaker, ScreenKeepAwaker>();
#elif __IOS__
        DependencyService.Register<IPlatformPair, PlatformPair>();
        DependencyService.Register<IDirectorySelector, DirectorySelector>();
        DependencyService.Register<ILinkSharer, LinkSharer>();
        DependencyService.Register<IPhotoPicker, PhotoPicker>();
        DependencyService.Register<IScreenKeepAwaker, ScreenKeepAwaker>();
        DependencyService.Register<INotchHeightProvider, NotchHeightProvider>();
#endif
        var app = builder.Build();
        app.UseFreshMvvm();
        return app;
    }
}
