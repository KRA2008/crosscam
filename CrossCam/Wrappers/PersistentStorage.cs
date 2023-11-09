using Newtonsoft.Json;
using Xamarin.Essentials;

namespace CrossCam.Wrappers
{
    public class PersistentStorage
    {
        public const string SETTINGS_KEY = "settings";
        public const string TOTAL_SAVES_KEY = "saves";

        public static T LoadOrDefault<T>(string key, T defaultValue)
        {
            if (Preferences.ContainsKey(key) &&
                Preferences.Get(key, null) is { } pref)
            {
                return JsonConvert.DeserializeObject<T>(pref);
            }
            return defaultValue;
        }

        public static void Save(string key, object data)
        {
            Preferences.Set(key, JsonConvert.SerializeObject(data));
        }
    }
}
