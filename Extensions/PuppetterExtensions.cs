using DoMyThingWorker.Models;
using HtmlAgilityPack;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PuppeteerExtraSharp;
using PuppeteerExtraSharp.Plugins.Recaptcha;
using PuppeteerExtraSharp.Plugins.Recaptcha.Provider.AntiCaptcha;

namespace PuppeteerSharp
{
    public static class PuppeteerExtentions
    {
        public const string NoIdleWorkers = "No idle workers are available at the moment";
        public const string NoIdleWorkersMessage = "Unable to solve recaptcha. No idle workers are available at the moment.";
        public const string RecaptchaRendered = "reCAPTCHA has already been rendered in this element";
        public const string RecaptchaRenderedMessage = "Unable to solve recaptcha. reCAPTCHA has already been rendered in this element.";
        private const string RecaptchaSolvingInProcess = "Solving is in process";

        /// <summary>
        /// Creates local puppeteer instance. May require elevated users permission (VS ran as administrator)
        /// </summary>
        /// <returns></returns>
        public static Task<IBrowser> CreateLocalPuppeteerAsync(string userDataDir = null, RecaptchaPlugin recaptchaPlugin = null)
        {   
            var options = new LaunchOptions
            {
                Headless = false,
                // TODO : Dig down more
                IgnoredDefaultArgs = new[] { "--enable-automation" },
                Args = new string[] { "--no-sandbox" }

            };
            if (!string.IsNullOrEmpty(userDataDir))
            {
                // TODO : Dig down more
                options.UserDataDir = userDataDir;
            }

            return CreateLocalPuppeteer(options, recaptchaPlugin);
        }

        /// <summary>
        /// Creates local puppeteer instance. May require elevated users permission (VS ran as administrator)
        /// </summary>
        /// <param name="launchOptions">The launch options.</param>
        /// <returns></returns>
        private static async Task<IBrowser> CreateLocalPuppeteer(LaunchOptions launchOptions, RecaptchaPlugin? recaptchaPlugin = null)
        {
            var browserFetcher = new BrowserFetcher(new BrowserFetcherOptions
            {
                //Path = "./",
                // TODO : Change here to Linux on production
                Platform = Platform.MacOSArm64,
            });

            await browserFetcher.DownloadAsync();
            var puppeter = new PuppeteerExtra();
            if (recaptchaPlugin != null)
            {
                puppeter.Use(recaptchaPlugin);
            }

            return await puppeter.LaunchAsync(launchOptions);
        }

        /// <summary>
        /// Creates puppeter instance
        /// </summary>
        /// <returns></returns>
        public static Task<IBrowser> CreatePuppeteer(string apiKey, Dictionary<string, string> connStringParams = null)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new Exception("Apikey cannot be empty");
            }

            if (connStringParams == null)
            {
                connStringParams = new Dictionary<string, string>();
            }

            connStringParams["token"] = apiKey;

            var url = string.Format("wss://chrome-eu-uk.browserless.io?{0}", string.Join("&",
                connStringParams.Select(kvp => kvp.Value != null ? string.Format("{0}={1}", kvp.Key, kvp.Value) : string.Format("{0}", kvp.Key))));

            var options = new ConnectOptions()
            {
                IgnoreHTTPSErrors = true,
                BrowserWSEndpoint = url
            };

            return Puppeteer.ConnectAsync(options);
        }

        /// <summary>
        /// Create new page with Anti-Captcha resolver
        /// </summary>
        /// <exception cref="CaptchaException">Unable to solve recaptcha. Need to try again</exception>
        public static async Task<IPage> NewPageWithAntiCaptchaAsync(this Browser browser, string url, string anticaptchaKey, Func<Task> antiCaptchaStartsCallback,
            ViewPortOptions viewPortOptions = null)
        {
            var page = await browser.NewPageAsync();
            if (viewPortOptions != null)
            {
                await page.SetViewportAsync(viewPortOptions);
            }
            await page.GoToAsync(url, new NavigationOptions() { WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded }, Timeout = 0 });

            // TODO : Implement here
            //await page.UseAntiCaptchaAsync(anticaptchaKey, antiCaptchaStartsCallback);
            return page;
        }

        /// <summary>
        /// Uses the anti captcha asynchronous.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="anticaptchaKey">The API key.</param>
        public static async Task UseAntiCaptchaAsync(this Page page, string anticaptchaKey, Func<Task> antiCaptchaStartsCallback, CaptchaType captchaType = CaptchaType.ReCaptcha)
        {
            await StartCaptchaSolvingProcess(page, anticaptchaKey, captchaType);

            await page.WaitForTimeoutAsync(1000);
            page.PageError += (_, args) =>
            {
                CheckError(args.Message);
            };
            page.Error += (_, args) =>
            {
                CheckError(args.Error);
            };
            page.Console += (_, args) =>
            {
                if (args.Message.Type == ConsoleType.Error)
                {
                    CheckError(args.Message.Text);
                }
                else if (args.Message.Text.Contains(RecaptchaSolvingInProcess))
                {
                    antiCaptchaStartsCallback();
                }
            };

        }

        /// <summary>
        /// Calls anticaptcha service for captcha resolving and waits until it will be resolved or timed out
        /// Default timeout for resolving 3 min.
        /// </summary>
        /// <returns>true if recaptcha was solved</returns>
        public static async Task<bool> SolveCaptchaAsync(this Page page, string anticaptchaKey, int timeoutInMinutes = 3, bool throwIfNotResolved = true, CaptchaType captchaType = CaptchaType.ReCaptcha)
        {
            await StartCaptchaSolvingProcess(page, anticaptchaKey, captchaType);

            var opts = new WaitForSelectorOptions() { Timeout = (int)TimeSpan.FromMinutes(timeoutInMinutes).TotalMilliseconds };

            try
            {
                return await page.WaitForSelectorAsync("div[class='antigate_solver solved']", opts) != null;
            }
            catch (Exception ex)
            {
                if (throwIfNotResolved)
                {
                    throw;
                }
            }

            return false;
        }

        private static async Task StartCaptchaSolvingProcess(Page page, string anticaptchaKey, CaptchaType captchaType = CaptchaType.ReCaptcha)
        {
            await page.EvaluateExpressionAsync(
                @"var d = document.getElementById(""anticaptcha-imacros-account-key"");
                if (!d)
                {
                    d = document.createElement(""div"");" +
                    $"d.innerHTML = \"{anticaptchaKey}\";" +
                    @"d.style.display = ""none"";
                    d.id = ""anticaptcha-imacros-account-key"";
                    document.getElementsByTagName('head')[0].appendChild(d);
                }"
            );

            string scriptSolvingCaptcha = "";
            switch (captchaType)
            {
                case CaptchaType.ReCaptcha:
                    scriptSolvingCaptcha = "https://cdn.antcpt.com/imacros_inclusion/recaptcha.js?";
                    break;
                default:
                    break;
            }

            await page.EvaluateExpressionAsync(
                $@"var s = document.createElement(""script"");
                s.src = ""{scriptSolvingCaptcha}"" + Math.random();
                document.getElementsByTagName('head')[0].appendChild(s);"
            );
        }

        /// <summary>
        /// Tries to find recaptcha div element at provided page
        /// </summary>
        /// <returns>true if re-captcha was found</returns>
        public static bool IsReCaptchaExists(this HtmlDocument doc) =>
            doc.DocumentNode.SelectSingleNode("//div[@class='g-recaptcha']") != null;

        /// <summary>
        /// Convert html id to valid selector, that can be used in QuerySelector
        /// </summary>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static string IdToSelector(this string selector) =>
            selector.Replace("\\", "\\\\").Replace(":", "\\:");

        /// <summary>
        /// Clicks the after appear asynchronous.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="selector">The selector.</param>
        public static async Task ClickAfterAppearAsync(this Page page, string selector)
        {
            await page.WaitForSelectorAsync(selector);
            await page.ClickAsync(selector);
        }

        /// <summary>
        /// Wait while element will appear on the page
        /// </summary>
        /// <param name="page"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static async Task WaitWhenDisAppearAsync(this Page page, string selector)
        {
            IElementHandle value;
            do
            {
                await page.WaitForTimeoutAsync(500);
                value = await page.QuerySelectorAsync(selector);
            }
            while (value != null);
        }

        /// <summary>
        /// Wait when element will appear on the page
        /// </summary>
        /// <param name="page"></param>
        /// <param name="selector"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public static async Task<IElementHandle> QuerySelectorWhenAppearAsync(this Page page, string selector, int? timeout = null)
        {
            IElementHandle value;
            const int repeatTime = 500;
            do
            {

                await page.WaitForTimeoutAsync(repeatTime);
                try
                {
                    value = await page.QuerySelectorAsync(selector);
                }
                catch (Exception ex)
                {
                    if ((ex.InnerException == null) || (ex.InnerException.Message != "Execution context was destroyed, most likely because of a navigation."))
                    {
                        throw;
                    }
                    value = null;
                    //ignore navigation exeptions
                }
                if (timeout.HasValue)
                {
                    timeout -= repeatTime;
                    if (timeout < 0)
                    {
                        return null;
                    }
                }
            }
            while (value == null);
            return value;
        }

        /// <summary>
        /// Waits for visible selector asyc.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="selector">The selector.</param>
        /// <param name="timeout">The timeout.</param>
        /// <returns></returns>
        public static Task WaitForVisibleSelectorAsyc(this Page page, string selector, int timeout = 0) =>
            page.WaitForSelectorAsync(selector, new WaitForSelectorOptions() { Visible = true, Timeout = timeout });

        /// <summary>
        /// Get inner text by path
        /// </summary>
        /// <param name="page"></param>
        /// <param name="xPath"></param>
        /// <returns></returns>
        public async static Task<string> GetXPathTextValueAsync(this Page page, string xPath)
        {
            var node = await page.XPathAsync(xPath);

            if (node?.Any() != true)
            {
                return null;
            }

            return await node.First().GetElementTextAsync();
        }

        /// <summary>
        /// Waits for visible selector asynchronous.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <param name="selector">The selector.</param>
        /// <param name="timeout">The timeout.</param>
        /// <returns></returns>
        public static Task WaitForVisibleSelectorAsync(this Frame frame, string selector, int timeout = 60000) =>
            frame.WaitForSelectorAsync(selector, new WaitForSelectorOptions() { Visible = true, Timeout = timeout });

        /// <summary>
        ///     Get the property value of given element handler.
        /// </summary>
        /// <param name="elementHandle">ElementHandle.</param>
        /// <param name="propertyName">String.</param>
        /// <returns>Property value.</returns>
        public static async Task<string> GetElementPropertyValueAsync(this IElementHandle elementHandle, string propertyName)
        {
            var property = await elementHandle.GetPropertyAsync(propertyName);
            var propertyValue = await property.JsonValueAsync<string>();
            return propertyValue?.Trim();
        }

        /// <summary>
        /// Get the inner text of given element handler.
        /// </summary>
        /// <param name="elementHandle">ElementHandle.</param>
        /// <returns>Property value.</returns>
        public static Task<string> GetElementTextAsync(this IElementHandle elementHandle)
        {
            return elementHandle.GetElementPropertyValueAsync("textContent");
        }

        /// <summary>
        /// Click by element even if it's not visible.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="selector">Jquery selector.</param>
        /// <returns></returns>
        public static async Task ClickByElementEvenIfInvisible(this Page page, string selector)
        {
            try
            {
                await page.ClickAsync(selector);
            }
            catch (PuppeteerException ex) when (ex.Message.Contains("not visible or not an"))
            {
                await page.EvaluateExpressionAsync($"$('{selector}').click()");
            }
        }

        public static Task JavascriptClickByElement(this ElementHandle elementHandle)
        {
            return elementHandle.EvaluateFunctionAsync("el => el.click()");
        }

        public static Task JavascriptClickBySelector(this Page page, string selector)
        {
            return page.EvaluateExpressionAsync($"document.querySelector(\"{selector}\").click()");
        }


        static void CheckError(string error)
        {
            if (error?.Contains(RecaptchaRendered) == true)
                throw new Exception(RecaptchaRenderedMessage);
            if (error?.Contains(NoIdleWorkers) == true)
                throw new Exception(NoIdleWorkersMessage);
        }

    }
}
