using Microsoft.JSInterop;

namespace tuya_mqtt.net.Data
{
    internal interface IBrowserService
    {
        public Task<TimeSpan> GetTimeZoneOffset();
        public Task<string> GetBrowserLanguage();
        public Task ScrollClassIntoView(string classSearchString, int index);
        public Task DownloadStreamAsync(string fileName, Stream fileStream);

    }
}