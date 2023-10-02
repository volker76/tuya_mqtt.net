using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Diagnostics;
using tuya_mqtt.net.Data;

namespace tuya_mqtt.net.Services
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
    public interface IBrowserService
    {
        
        public Task InitAsync(IJSRuntime js);
        public event EventHandler<WindowSize>? Resize;
        public Task<BoundingClientRect> GetElementClientRectAsync(ElementReference element);
        public Task ScrollClassIntoView(string classSearchString, int index);
        public int BrowserWidth { get; }
        public int BrowserHeight { get; }
        public bool IsInitialized { get; }
    }
    public class BrowserService : IBrowserService
    {
        
        // ReSharper disable once InconsistentNaming
        // ReSharper disable once RedundantDefaultMemberInitializer
        private IJSRuntime? JS = null;
        private readonly ILogger _logger;
        private IJSObjectReference? _jsModule;
        public event EventHandler<WindowSize>? Resize;
        private int _browserWidth;
        private int _browserHeight;
        private readonly SemaphoreSlim _initLocker = new SemaphoreSlim(1,1);
        public BrowserService(ILogger<BrowserService> logger)
        {
            _logger = logger;
        }

        public bool IsInitialized => _jsModule != null;

        public async Task InitAsync(IJSRuntime js)
        {
            await _initLocker.WaitAsync().ConfigureAwait(false);
            try
            {
                // enforce single invocation            
                if (JS == null)
                {
                    this.JS = js;
                    try
                    {
                        _jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "/lib/BrowserService.js");

                        await _jsModule.InvokeAsync<string>("resizeListener", DotNetObjectReference.Create(this))
                            .ConfigureAwait(false);
                        _logger.LogDebug("BrowserService initialized");

                        await GetViewPortSize().ConfigureAwait(false);

                    }
                    catch (JSException e)
                    {
                        _logger.LogError(e,
                            "could not initialize BrowserService by calling 'resizeListener' in BrowserService.js");
                        if (Debugger.IsAttached)
                        {
                            Debugger.Break();
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e,
                            "general error initialize BrowserService");
                        if (Debugger.IsAttached)
                        {
                            Debugger.Break();
                        }
                    }
                }
            }
            finally
            {
                
                _initLocker.Release(); 

            }

        }

        private async Task GetViewPortSize()
        {
            //await _initLocker.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_jsModule == null)
                {
                    _logger.LogError("GetViewPortSize - no JSModule");
                    throw new InvalidOperationException("BrowserService is not initialized. run Init() before.");
                }

                var ret = await _jsModule.InvokeAsync<List<int>>("viewportSize").ConfigureAwait(false);
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
               // _initLocker.Release();

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
                this.Resize?.Invoke(this, new WindowSize() { Width = _browserWidth, Height = _browserHeight });
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
                    _logger.LogError( "browserWidth == 0");
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

        public async Task<Services.BoundingClientRect> GetElementClientRectAsync(ElementReference element)
        {
            if (_jsModule == null)
            {
                throw new InvalidOperationException("BrowserService is not initialized. run Init() before.");
            }
            var result = await _jsModule.InvokeAsync<Services.BoundingClientRect>("MyGetBoundingClientRect", element);
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
    }
}
