using CrossCam.Model;

namespace CrossCam.Wrappers
{
    public static class Analytics
    {
        public static Settings Settings { get; private set; }

        public static void Initialize(Settings settings)
        {
            Settings = settings;
        }

        public static void TrackEvent(string name)
        {
            if (Settings?.IsAnalyticsEnabled == true)
            {
                SentrySdk.CaptureMessage(name);
                SentrySdk.AddBreadcrumb(name);
            }
        }

        public static void TrackEvent(string name, Dictionary<string,string> dict)
        {
            if (Settings?.IsAnalyticsEnabled == true)
            {
                SentrySdk.CaptureMessage(name);
                SentrySdk.AddBreadcrumb(name,data: dict);
            }
        }

    }

    public static class Crashes
    {
        public static void TrackError(Exception ex)
        {
            if (Analytics.Settings?.IsAnalyticsEnabled == true)
            {
                SentrySdk.CaptureException(ex);
            }
        }

        public static void TrackError(Exception ex, Dictionary<string, string> dict)
        {
            if (Analytics.Settings?.IsAnalyticsEnabled == true)
            {
                SentrySdk.AddBreadcrumb(ex.Message, data: dict);
                SentrySdk.CaptureException(ex);
            }
        }
    }
}