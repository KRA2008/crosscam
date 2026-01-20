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

        public static void TrackEvent(string eventName, Dictionary<string, object> parameters = null)
        {
            if (Settings?.IsAnalyticsEnabled == false) return;

            parameters ??= new Dictionary<string, object>();

            parameters = MakeSafeDict(parameters);

            CrossNewRelic.Current.RecordCustomEvent("user event", eventName, parameters);
        }

        public static void DropBreadcrumb(string crumbName, Dictionary<string, object> attributes = null)
        {
            if (Settings?.IsAnalyticsEnabled == false) return;

            attributes = MakeSafeDict(attributes);

            CrossNewRelic.Current.RecordBreadcrumb(crumbName, attributes);
        }

        private static Dictionary<string, object> MakeSafeDict(Dictionary<string, object> dict)
        {
            foreach (var parameter in dict)
            {
                dict[parameter.Key] = JsonConvert.SerializeObject(parameter.Value); //TODO: workaround for NewRelic's inability to log a null value
            }

            return dict;
        }

        public static class Crashes
        {
            public static void TrackError(Exception ex, Dictionary<string, object> dict = null)
            {
                if (Settings?.IsAnalyticsEnabled == false) return;

                if (dict != null)
                {
                    dict.Add("exception", ex.ToString());
                }
                else
                {
                    dict = new Dictionary<string, object>
                    {
                        {"exception", ex.ToString()}
                    };
                }

                dict = MakeSafeDict(dict);

                CrossNewRelic.Current.RecordException(ex,dict);
            }
        }
    }
}