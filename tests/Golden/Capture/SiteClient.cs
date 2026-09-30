#nullable disable

// The capture's browser: plain HTTP against the site, no redirects followed, cookies kept (as a browser keeps them).
// The same file drives the 2014 site (capture) and the new site (replay, through WebApplicationFactory's client), so
// every question is asked the same way. Form posts first read the form's page, as a browser has it, and send back its
// hidden inputs (the new site's antiforgery token among them); the ajax call sends the page's token as a header, as the
// new site's script does. The 2014 site has no token, so it receives exactly the fields a 2014 browser sent.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace GoldenCapture
{
    public sealed class Response
    {
        public int Status;
        public string ContentType;
        public string Location;
        public byte[] Body;

        public string Text => Encoding.UTF8.GetString(Body);
    }

    public sealed class SiteClient
    {
        private readonly HttpClient _http;

        public SiteClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<Response> Get(string path)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
            return await Send(request);
        }

        // Posts the fields as application/x-www-form-urlencoded, in the order given, with the hidden inputs of the form
        // on formPage added first when the page has them (the field list wins on a clash, so a case can post a bad Id).
        public async Task<Response> PostForm(string path, IList<KeyValuePair<string, string>> fields, string formPage = null, bool ajax = false)
        {
            var body = new List<KeyValuePair<string, string>>();
            string token = null;
            if (formPage != null)
            {
                var page = await Get(formPage);
                var hidden = Page.HiddenInputs(page.Text);
                token = hidden.Where(h => h.Key == "__RequestVerificationToken").Select(h => h.Value).FirstOrDefault()
                    ?? Page.MetaToken(page.Text);
                if (!ajax)
                {
                    foreach (var h in hidden)
                    {
                        if (!fields.Any(f => f.Key == h.Key))
                        {
                            body.Add(h);
                        }
                    }
                }
            }

            body.AddRange(fields);
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Content = new FormUrlEncodedContent(body);
            request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
            if (ajax)
            {
                request.Headers.Accept.ParseAdd("application/json, text/javascript, */*; q=0.01");
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                if (token != null)
                {
                    request.Headers.Add("RequestVerificationToken", token);
                }
            }
            else
            {
                request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            }

            return await Send(request);
        }

        private async Task<Response> Send(HttpRequestMessage request)
        {
            using var response = await _http.SendAsync(request);
            var r = new Response
            {
                Status = (int)response.StatusCode,
                ContentType = response.Content.Headers.ContentType?.MediaType,
                Location = response.Headers.Location?.OriginalString,
                Body = await response.Content.ReadAsByteArrayAsync(),
            };
            return r;
        }
    }
}
