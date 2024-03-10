using DoMyThingWorker.Models;
using PuppeteerSharp;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO.Compression;
using System.IO;
using Microsoft.Extensions.Hosting;
using PuppeteerExtraSharp.Plugins.Recaptcha;
using PuppeteerExtraSharp.Plugins.Recaptcha.Provider.AntiCaptcha;

namespace DoMyThingWorker.Processors
{
    public class FindVFSAppointmentSlotProcessor : IProcessor<FindVFSAppointmentModel, FindVFSAppointmentResponseModel>
    {
        private readonly string _baseUrl = "https://visa.vfsglobal.com/";
        private readonly HttpClient client;
        private readonly IConfiguration configuration;
        private readonly ILogger<FindVFSAppointmentSlotProcessor> logger;
        private readonly IHostEnvironment environment;
        
        private RecaptchaPlugin recaptchaPlugin;

        public FindVFSAppointmentSlotProcessor(IHttpClientFactory clientFactory, IConfiguration configuration,
            ILogger<FindVFSAppointmentSlotProcessor> logger, IHostEnvironment environment)
        {
            this.client = clientFactory.CreateClient();
            this.client.BaseAddress = new Uri(_baseUrl);
            this.configuration = configuration;
            this.logger = logger;
            this.environment = environment;
        }

        public async Task<FindVFSAppointmentResponseModel> ProcessAsync(FindVFSAppointmentModel request)
        {
            using IPage page = await OpenBrowserPage(isDevelopment: environment.IsDevelopment() || !configuration.GetValue<bool>("IsBrowserlessEnabled"));

            var (firstFileName, title1) = await FindAppointmentAsync("tur", "nld", page);

            return new FindVFSAppointmentResponseModel();
        }

        private async Task<(string fileNameInStorage, string title)> FindAppointmentAsync(string fromCode, string toCode,IPage page)
        {
            await Login(fromCode, toCode, page);

            // await PickFirstSubtitleAndNavigateTo(page);
            // try
            // {
            //     await TryToNavigateToDownloadPage(page);
            // }
            // catch
            // {
            //     // We're already in Download Page
            // }
            //
            // string href = await ExtractDownloadUrl(page);
            // string title = await ExtractMovieTitle(page);
            //
            // if (String.IsNullOrWhiteSpace(href))
            // {
            //     throw new Exception("Unable to find link to download subtitle!");
            // }
            //
            // var fileInBytes = await DownloadSubtitle(href);
            // var fileName = $"{title}-{languageCode}";
            //
            // var fileNameInStorage = await subtitleStorageService.UploadFileAsync(fileName, fileInBytes);
            // return (fileNameInStorage: fileNameInStorage, title: title);
            return default;
        }

        private async Task<byte[]> DownloadSubtitle(string href)
        {
            var zippedFileBytes = await client.GetByteArrayAsync(href);

            using (var archive = new ZipArchive(await client.GetStreamAsync(href), ZipArchiveMode.Read, true))
            {
                var subtitleFile = archive.Entries.FirstOrDefault(entry => entry.FullName.EndsWith(".srt"));
                if (subtitleFile == default)
                {
                    logger.LogError($"Subtitle not found in zipped file! download link : {href}");
                    throw new Exception($"Subtitle not found!");
                }

                using var stream = subtitleFile.Open();
                using var memoryStream = new MemoryStream();

                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        private static async Task<string> ExtractDownloadUrl(IPage page)
        {
            var doc = new HtmlDocument();
            var content = await page.GetContentAsync();
            doc.LoadHtml(content);
            var href = doc.GetElementbyId("bt-dwl-bt")
                .GetAttributes(new string[] { "href" })
                .SingleOrDefault()?.Value ?? String.Empty;
            return href;
        }

        private static async Task<string> ExtractMovieTitle(IPage page)
        {
            var doc = new HtmlDocument();
            var content = await page.GetContentAsync();
            doc.LoadHtml(content);
            var title = doc.GetElementbyId("bt-dwl-bt")
                .GetAttributes(new string[] { "data-product-title" })
                .SingleOrDefault()?.Value ?? String.Empty;
            return title;
        }


        private async Task TryToNavigateToDownloadPage(IPage page)
        {
            await page.ClickAsync("table#search_results tr:nth-child(2) .bnone");

            await page.WaitForSelectorAsync("#bt-dwl-bt", GetTimeoutOption(10));

            await page.WaitForTimeoutAsync(5);
        }

        private static async Task PickFirstSubtitleAndNavigateTo(IPage page)
        {
            await page.ClickAsync("table#search_results tr:nth-child(2) .bnone");

            await page.WaitForTimeoutAsync(5000);
        }

        private async Task Login(string fromCode, string toCode, IPage page)
        {
            var url = BuildSearchUrl(fromCode, toCode);

            await page.GoToAsync(url, new NavigationOptions() { WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded }, Timeout = 0 });
            await page.WaitForSelectorAsync("input[formControlName='username']", GetTimeoutOption(10));
            await page.WaitForTimeoutAsync(5000);
            // ResolveCloudFlare(page);
            
            await page.TypeAsync("input[formControlName='username']", "*****");
            await page.TypeAsync("input[formControlName='password']", "***");
            await page.ClickAsync("button.mat-btn-lg");
            await page.WaitForTimeoutAsync(5000);

        }
        private async Task ResolveCloudFlare(IPage page)
        {
            try
            {
                var result = await recaptchaPlugin.SolveCaptchaAsync(page);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
        }

        private async Task<IPage> OpenBrowserPage(bool isDevelopment = true)
        {
            IBrowser browser;
            if (isDevelopment)
            {
                recaptchaPlugin = new RecaptchaPlugin(new AntiCaptcha("*****"));
                browser = await PuppeteerExtentions.CreateLocalPuppeteerAsync();
            }
            else
            {
                var configurationString = configuration["BrowserlessApiKey"];
                if (configurationString == null)
                {
                    throw new ArgumentNullException("Browserless ApiKey cannot be null for deployed instance!");
                }

                var connectionStringParams = new Dictionary<string, string>()
                {
                };
                browser = await PuppeteerExtentions.CreatePuppeteer(configurationString, connectionStringParams);
            }

            var page = await browser.NewPageAsync();
            return page;
        }

        private string BuildSearchUrl(string fromCode, string toCode) => $"{_baseUrl}/{fromCode}/en/{toCode}/login";
        private string EncodeMovieName(string movieName) => String.Join("+", movieName.Split(" "));
        private WaitForSelectorOptions GetTimeoutOption(int sec) => new WaitForSelectorOptions() { Timeout = TimeSpan.FromSeconds(sec).Microseconds };
    }
}