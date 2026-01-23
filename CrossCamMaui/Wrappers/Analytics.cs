using System.Diagnostics;
using CrossCam.Model;
using NewRelic.MAUI.Plugin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


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

        public static void TrackEvent(string eventName, Dictionary<string, object> attributes = null)
        {
            if (Settings?.IsAnalyticsEnabled == false) return;

            attributes ??= new Dictionary<string, object>();

            try
            {
                CrossNewRelic.Current.RecordCustomEvent("TrackedEvent", eventName, attributes);
            }
            catch (NullReferenceException) // New Relic doesn't do nulls in dicts
            {
                foreach (var attribute in attributes)
                {
                    attributes[attribute.Key] = JsonConvert.SerializeObject(attribute.Value);
                }
                CrossNewRelic.Current.RecordCustomEvent("TrackedEvent", eventName, attributes);
            }
        }

        public static void DropBreadcrumb(string crumbName, Dictionary<string, object> attributes = null)
        {
            if (Settings?.IsAnalyticsEnabled == false) return;

            attributes ??= new Dictionary<string, object>();

            try
            {
                CrossNewRelic.Current.RecordBreadcrumb(crumbName, attributes);
            }
            catch (NullReferenceException) // New Relic doesn't do nulls in dicts
            {
                foreach (var attribute in attributes)
                {
                    attributes[attribute.Key] = JsonConvert.SerializeObject(attribute.Value);
                }
                CrossNewRelic.Current.RecordBreadcrumb(crumbName, attributes);
            }
        }

        public static class Crashes
        {
            public static void TrackError(Exception ex, Dictionary<string, object> attributes = null)
            {
                if (Settings?.IsAnalyticsEnabled == false) return;

                if (attributes != null)
                {
                    attributes.Add("exception", ex.ToString());
                }
                else
                {
                    attributes = new Dictionary<string, object>
                    {
                        {"exception", ex.ToString()}
                    };
                }

                try
                {
                    CrossNewRelic.Current.RecordException(ex, attributes);
                }
                catch (NullReferenceException) // New Relic doesn't do nulls in dicts
                {
                    foreach (var attribute in attributes)
                    {
                        attributes[attribute.Key] = JsonConvert.SerializeObject(attribute.Value);
                    }
                    CrossNewRelic.Current.RecordException(ex, attributes);
                }
            }
        }
    }
}