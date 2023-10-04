using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Diagnostics;
using tuya_mqtt.net.Data;

namespace tuya_mqtt.net.Services
{
    public class BrowserService : IAsyncDisposable
    {

        private readonly ILogger _logger;
        private readonly Lazy<Task<IJSObjectReference>> moduleTask;

        public BrowserService(ILogger<BrowserService> logger, IJSRuntime jsRuntime)
        {
            _logger = logger;

            moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "/lib/BrowserService.js").AsTask());
        }

        public async Task<TimeSpan> GetTimeZoneOffset()
        {
            var module = await moduleTask.Value;
            int i = await module.InvokeAsync<int>("timeZoneOffset");
            return TimeSpan.FromMinutes(-i);

        }

        public async Task<string> GetBrowserLanguage()
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<string>("getBrowserLanguage");
        }
        
        public async Task ScrollClassIntoView(string classSearchString, int index)
        {
            var module = await moduleTask.Value;

            await module.InvokeVoidAsync("scrollToElement", new object[] { classSearchString, index });
        }

        public async Task DownloadStreamAsync(string fileName, Stream fileStream)
        {
            var module = await moduleTask.Value;

            using var streamRef = new DotNetStreamReference(stream: fileStream);

            await module.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);
        }


        private bool disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    //close any events ....
                }
                disposed = true;
            }
        }



        public async ValueTask DisposeAsync()
        {
            try
            {
                if (moduleTask.IsValueCreated)
                {
                    var module = await moduleTask.Value;
                    await module.DisposeAsync();
                }
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            catch (Exception)
            {
                // https://stackoverflow.com/questions/72488563/blazor-server-side-application-throwing-system-invalidoperationexception-javas
            }
        }

        
    }
}
