using CrossCam.Model;
using CrossCam.Wrappers;
using Newtonsoft.Json;

namespace CrossCam.ViewModel
{
    public class TipMeViewModel : BaseViewModel
    {
        public string TipsCount { get; set; }
        public string TipsTotal { get; set; }

        public override void Init(object initData)
        {
            base.Init(initData);
            GetTipData();
        }

        private async void GetTipData()
        {
            await Task.Run(async () =>
            {
                try
                {
                    var client = new HttpClient();
                    var tipData = await client.GetAsync("https://kra2008.com/tips.json");
                    tipData.EnsureSuccessStatusCode();

                    var body = await tipData.Content.ReadAsStringAsync();
                    var tips = JsonConvert.DeserializeObject<TipData>(body);
                    if (tips is {Version: "1"})
                    {
                        TipsCount = tips.TipsCount;
                        TipsTotal = tips.TipsTotal;
                    }
                }
                catch (Exception ex)
                {
                    Crashes.TrackError(ex);
                }
            });
        }
    }
}