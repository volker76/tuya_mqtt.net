using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Diagnostics;
using tuya_mqtt.net.Data;

namespace tuya_mqtt.net.Helper
{
    public class BrowserService 
    {
        public class BoundingClientRect
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public double Top { get; set; }
            public double Right { get; set; }
            public double Bottom { get; set; }
            public double Left { get; set; }
        }
        // ReSharper disable once InconsistentNaming
        // ReSharper disable once RedundantDefaultMemberInitializer
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger _logger;
        private IJSObjectReference? _jsModule;
        public event EventHandler<WindowSize>? Resize;
        private int _browserWidth;
        private int _browserHeight;

        public BrowserService(ILogger<BrowserService> logger, IJSRuntime jsRuntime)
        {
            _logger = logger;
            _jsRuntime = jsRuntime;

        }

        public bool IsInitialized => _jsModule != null;

        public async Task InitAsync()
        {
            try
            {
                _jsModule = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "/lib/BrowserService.js");

                await _jsModule.InvokeAsync<string>("resizeListener", DotNetObjectReference.Create(this));
                _logger.LogDebug("BrowserService initialized");

                await GetViewPortSize();
            }
            catch (JSException e)
            {
                _logger.LogError(e,
                    "could not initialize BrowserService by calling 'resizeListener' in BrowserService.js");
                if (Debugger.IsAttached)
                {
                    //Debugger.Break();
                    Console.WriteLine(e.Message);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    "general error initialize BrowserService");
                if (Debugger.IsAttached)
                {
                    //Debugger.Break();
                    Console.WriteLine(e.Message);
                }
            }
            //try
            //{
            //    // enforce single invocation            
            //    if (true)
            //    {

            //        try
            //        {
            //            _jsModule = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "/lib/BrowserService.js");

            //            await _jsModule.InvokeAsync<string>("resizeListener", DotNetObjectReference.Create(this))
            //                .ConfigureAwait(false);
            //            _logger.LogDebug("BrowserService initialized");

            //            await GetViewPortSize().ConfigureAwait(false);

            //        }
            //        catch (JSException e)
            //        {
            //            _logger.LogError(e,
            //                "could not initialize BrowserService by calling 'resizeListener' in BrowserService.js");
            //            if (Debugger.IsAttached)
            //            {
            //                Debugger.Break();
            //            }
            //        }
            //        catch (Exception e)
            //        {
            //            _logger.LogError(e,
            //                "general error initialize BrowserService");
            //            if (Debugger.IsAttached)
            //            {
            //                Debugger.Break();
            //            }
            //        }
            //    }
            //}
            //finally
            //{


            //}

        }

        public async Task<TimeSpan> GetTimeZoneOffset()
        {
            int i = await _jsModule.InvokeAsync<int>("timeZoneOffset");
            return TimeSpan.FromMinutes(-i);
            
        }

        public async Task<string> GetBrowserLanguage()
        {
            return await _jsModule.InvokeAsync<string>("getBrowserLanguage");
        }
        private async Task GetViewPortSize()
        {
           
            try
            {
                if (_jsModule == null)
                {
                    _logger.LogError("GetViewPortSize - no JSModule");
                    throw new InvalidOperationException("BrowserService is not initialized. run Init() before.");
                }

                var ret = await _jsModule.InvokeAsync<List<int>>("viewportSize");
                if (ret.Count < 2)
                    throw new InvalidOperationException(
                        $"javascript viewportSize did not return two elements {ret}");
                _browserWidth = ret[0];
                _browserHeight = ret[1];
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error Getting ViewPortSize");
            }
            // ReSharper disable once RedundantEmptyFinallyBlock
            finally
            {
              

            }
        }

        [JSInvokable]
        public void SetBrowserDimensions(int jsBrowserWidth, int jsBrowserHeight)
        {
            try
            {
                _browserWidth = jsBrowserWidth;
                _browserHeight = jsBrowserHeight;
                _logger.LogDebug($"BrowserService window new size ({_browserWidth},{_browserHeight})");
                Resize?.Invoke(this, new WindowSize() { Width = _browserWidth, Height = _browserHeight });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error set browser dimensions");
            }
        }

        public int BrowserWidth
        {
            get
            {
                if (_browserWidth == 0)
                {
                    _logger.LogError("browserWidth == 0");
                    throw new InvalidOperationException("InitAsync seems not called or not finished yet.");
                }
                return _browserWidth;
            }
        }

        public int BrowserHeight
        {
            get
            {
                if (_browserHeight == 0)
                {
                    _logger.LogError("browserHeight == 0");
                    throw new InvalidOperationException("InitAsync seems not called or not finished yet.");
                }
                return _browserHeight;
            }
        }

        public async Task<BoundingClientRect> GetElementClientRectAsync(ElementReference element)
        {
            if (_jsModule == null)
            {
                throw new InvalidOperationException("BrowserService is not initialized. run Init() before.");
            }

            BoundingClientRect result;
            result = new BoundingClientRect();
            try
            {
                result = await _jsModule.InvokeAsync<BoundingClientRect>("MyGetBoundingClientRect", element);
            }
            catch (Exception ex)
            {

            }

            _logger.LogDebug($"GetBoundingClientRect ({result.Width},{result.Height})");
            return result;
        }

        public async Task ScrollToEnd(ElementReference element)
        {
            if (_jsModule == null)
            {
                _logger.LogError("ScrollToEnd - no JSModule");
                throw new InvalidOperationException("BrowserService is not initialized. run Init() before.");
            }

            await _jsModule.InvokeVoidAsync("scrollToEnd", element);

        }

        public async Task ScrollClassIntoView(string classSearchString, int index)
        {
            if (_jsModule == null)
            {
                _logger.LogError("ScrollClassIntoView - no JSModule");
                throw new InvalidOperationException("BrowserService is not initialized. run Init() before.");
            }

            await _jsModule.InvokeVoidAsync("scrollToElement", new object[] { classSearchString, index });
        }


        private Stream GetFileStream()
        {
            var randomBinaryData = new byte[50 * 1024];
            var fileStream = new MemoryStream(randomBinaryData);

            return fileStream;
        }
        public async Task DownloadFileFromStream()
        {
            if (_jsModule == null)
            {
                throw new InvalidOperationException("BrowserService is not initialized. run Init() before.");
            }

            var fileStream = GetFileStream();
            var fileName = "log.bin";

            using var streamRef = new DotNetStreamReference(stream: fileStream);

            await _jsModule.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);
        }

        public async Task DisposeAsync()
        {
            if (_jsModule is not null)
                {
                    await _jsModule.DisposeAsync();
                }
        }
    }
}
