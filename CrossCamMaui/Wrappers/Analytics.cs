using CrossCam.Model;
using Firebase.Analytics;

#if __IOS__
    using Foundation;
#elif __ANDROID__
    using Android.Content;
    using Android.OS;
#endif

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
            if (Settings?.IsAnalyticsEnabled == false) return;
#if __IOS__
            Firebase.Analytics.Analytics.LogEvent(name, (Dictionary<object, object>)null);
#elif __ANDROID__

            var firebaseAnalytics = FirebaseAnalytics.GetInstance(Platform.CurrentActivity);
            firebaseAnalytics.LogEvent(name, null);
#endif
        }

        public static void TrackEvent(string eventName, Dictionary<string, string> parameters)
        {
            if (Settings?.IsAnalyticsEnabled == false) return;
#if __IOS__
        if (parameters == null)
        {
            Firebase.Analytics.Analytics.LogEvent(eventName, (Dictionary<object, object>)null);
            return;
        }

        var keys = new List<NSString>();
        var values = new List<NSString>();
        foreach (var item in parameters)
        {
            keys.Add(new NSString(item.Key));
            values.Add(new NSString(item.Value));
        }

        var parametersDictionary =
            NSDictionary<NSString, NSObject>.FromObjectsAndKeys(values.ToArray(), keys.ToArray(), keys.Count);
        Firebase.Analytics.Analytics.LogEvent(eventName, parametersDictionary);
#elif __ANDROID__
            var firebaseAnalytics = FirebaseAnalytics.GetInstance(Platform.CurrentActivity);

            if (parameters == null)
            {
                firebaseAnalytics.LogEvent(eventName, null);
                return;
            }

            var bundle = new Bundle();
            foreach (var param in parameters)
            {
                bundle.PutString(param.Key, param.Value);
            }

            firebaseAnalytics.LogEvent(eventName, bundle);
#endif
        }

        public static class Crashes
        {
            public static void TrackError(Exception ex)
            {
                Analytics.TrackEvent(ex.ToString());
            }

            public static void TrackError(Exception ex, Dictionary<string, string> dict)
            {
                Analytics.TrackEvent(ex.ToString(), dict);
            }
        }
    }
}