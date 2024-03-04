using Microsoft.AppCenter.Crashes;
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
                    var tipData = client.GetAsync("https://kra2008.com/tips.json");
                    if (tipData.Result.IsSuccessStatusCode)
                    {
                        var body = await tipData.Result.Content.ReadAsStringAsync();
                        var tips = JsonConvert.DeserializeObject(body) as dynamic;
                        if (tips != null)
                        {
                            if (tips.version == 1)
                            {
                                TipsCount = tips.tipsCount;
                                TipsTotal = tips.tipsTotal;
                            }
                        }
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