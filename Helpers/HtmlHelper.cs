using AngleSharp;
using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DoMyThingWorker.Helpers
{
    public static class HtmlHelper
    {
        public static async Task<IDocument> GetDocumentAsync(HttpResponseMessage response)
        {

            var stream = await response.Content.ReadAsStreamAsync();

            var browser = BrowsingContext.New();

            var document = await browser.OpenAsync(virtualResponse =>
            {
                virtualResponse.Content(stream, shouldDispose: true);
                virtualResponse.Address(response.RequestMessage.RequestUri);
            });

            return document;
        }
    }
}
