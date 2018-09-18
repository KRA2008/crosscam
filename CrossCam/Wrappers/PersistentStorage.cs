using Newtonsoft.Json;
using Xamarin.Forms;

namespace CrossCam.Wrappers
{
    public class PersistentStorage
    {
        public const string SETTINGS_KEY = "settings";

        public static T LoadOrDefault<T>(string key, T defaultValue)
        {
            if (Application.Current.Properties.ContainsKey(key) &&
                Application.Current.Properties[key] != null)
            {
                return JsonConvert.DeserializeObject<T>(Application.Current.Properties[key] as string);
            }
            return defaultValue;
        }

        public static void Save(string key, object data)
        {
            Application.Current.Properties[key] = JsonConvert.SerializeObject(data);
        }
    }
}
