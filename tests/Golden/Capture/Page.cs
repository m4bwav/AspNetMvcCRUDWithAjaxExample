#nullable disable

// Reading a page the way the golden comparison needs it. A website's contract is not its bytes: a new stack writes
// different markup for the same page. So each page is recorded twice: its HTML (normalised, as history and for the
// screenshots) and a summary of what a visitor and a browser script depend on (title, headings, visible text in order,
// links, forms with every field and its validation attributes, labels, validation messages, table cells, element ids,
// scripts and stylesheets). The replay compares summaries; the maintainer's rulings name every difference allowed.
// ASP.NET's error page (the "yellow screen") is reduced to its title and exception, since its stack trace and version
// lines name the capture machine.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace GoldenCapture
{
    // Guids in answers: the seeded rows' ids and the fixed "unknown" ids are data and stay as they are; an id the site
    // made (a new person) becomes {NEW:name}, named by the case that created it, so two runs record the same text.
    public sealed class Ids
    {
        private static readonly Regex GuidPattern = new Regex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.CultureInvariant);
        private readonly HashSet<string> _kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Unexpected = new List<string>();

        public void Keep(string id)
        {
            _kept.Add(id);
        }

        public void Name(string id, string name)
        {
            _named[id] = "{NEW:" + name + "}";
        }

        public bool Known(string id)
        {
            return _kept.Contains(id) || _named.ContainsKey(id);
        }

        public static IEnumerable<string> Find(string text)
        {
            return GuidPattern.Matches(text).Select(m => m.Value);
        }

        public string Normalise(string text)
        {
            if (text == null)
            {
                return null;
            }

            return GuidPattern.Replace(text, m =>
            {
                if (_kept.Contains(m.Value))
                {
                    return m.Value;
                }

                if (_named.TryGetValue(m.Value, out var name))
                {
                    return name;
                }

                Unexpected.Add(m.Value);
                return "{UNKNOWN-ID}";
            });
        }
    }

    public static class Page
    {
        private static readonly HtmlParser Parser = new HtmlParser();

        public static IDocument Parse(string html)
        {
            return Parser.ParseDocument(html);
        }

        public static List<KeyValuePair<string, string>> HiddenInputs(string html)
        {
            var doc = Parse(html);
            return doc.QuerySelectorAll("form input[type=hidden]")
                .Select(e => new KeyValuePair<string, string>(e.GetAttribute("name") ?? "", e.GetAttribute("value") ?? ""))
                .Where(kv => kv.Key.Length > 0)
                .ToList();
        }

        // Every named field of the page's first form with the value the page gives it, as a browser would submit it.
        public static List<KeyValuePair<string, string>> FormValues(string html)
        {
            var doc = Parse(html);
            var form = doc.QuerySelector("form");
            if (form == null)
            {
                return new List<KeyValuePair<string, string>>();
            }

            return form.QuerySelectorAll("input,select,textarea")
                .Where(e => !string.IsNullOrEmpty(e.GetAttribute("name")))
                .Where(e => e.GetAttribute("type") != "submit" && e.GetAttribute("type") != "button")
                .Select(e => new KeyValuePair<string, string>(e.GetAttribute("name"), e.LocalName == "textarea" ? e.TextContent : e.GetAttribute("value") ?? ""))
                .ToList();
        }

        public static string MetaToken(string html)
        {
            var doc = Parse(html);
            return doc.QuerySelector("meta[name=RequestVerificationToken]")?.GetAttribute("content")
                ?? doc.QuerySelector("input[name=__RequestVerificationToken]")?.GetAttribute("value");
        }

        // The ids of the rows the index page lists, in page order.
        public static List<string> ListedIds(string html)
        {
            var doc = Parse(html);
            var ids = new List<string>();
            foreach (var a in doc.QuerySelectorAll("table a[href]"))
            {
                foreach (var id in Ids.Find(a.GetAttribute("href")))
                {
                    if (!ids.Contains(id, StringComparer.OrdinalIgnoreCase))
                    {
                        ids.Add(id);
                    }
                }
            }

            return ids;
        }

        public static bool IsErrorPage(string html)
        {
            return html.Contains("Server Error in '", StringComparison.Ordinal);
        }

        public static JsonObject ErrorPage(string html)
        {
            var doc = Parse(html);
            var text = Collapse(doc.Body?.TextContent ?? "");
            var o = new JsonObject();
            o.Add("kind", "ASP.NET error page");
            o.Add("title", Collapse(doc.Title ?? ""));
            var m = Regex.Match(text, @"Exception Details:\s*([A-Za-z0-9_.]+):\s*(.*?)\s*(Source Error:|Stack Trace:|Requested URL:|Version Information:|$)", RegexOptions.CultureInvariant);
            o.Add("exception", m.Success ? m.Groups[1].Value : null);
            o.Add("message", m.Success ? m.Groups[2].Value : null);
            var d = Regex.Match(text, @"Description:\s*(.*?)\s*(Exception Details:|Requested URL:|Source Error:|Version Information:|$)", RegexOptions.CultureInvariant);
            o.Add("description", d.Success ? d.Groups[1].Value : null);
            var u = Regex.Match(text, @"Requested URL:\s*(\S+)", RegexOptions.CultureInvariant);
            o.Add("requestedUrl", u.Success ? u.Groups[1].Value : null);
            return o;
        }

        public static JsonObject Summary(string html, Ids ids)
        {
            var doc = Parse(html);
            var o = new JsonObject();
            o.Add("title", ids.Normalise(Collapse(doc.Title ?? "")));
            o.Add("headings", doc.QuerySelectorAll("h1,h2,h3,legend").Select(e => (object)(e.LocalName + ": " + ids.Normalise(Collapse(e.TextContent)))).ToList());
            o.Add("text", VisibleText(doc.Body, ids));
            o.Add("links", doc.QuerySelectorAll("a[href]").Select(e => (object)(ids.Normalise(Collapse(e.TextContent)) + " -> " + ids.Normalise(e.GetAttribute("href")))).ToList());
            o.Add("buttons", doc.QuerySelectorAll("input[type=submit],input[type=button],button,[id$=-btn]").Select(e => (object)(e.LocalName == "input" ? e.GetAttribute("value") : Collapse(e.TextContent))).ToList());
            o.Add("forms", doc.QuerySelectorAll("form").Select(f => (object)Form(f, ids)).ToList());
            o.Add("labels", doc.QuerySelectorAll("label").Select(e => (object)((e.GetAttribute("for") ?? "") + ": " + Collapse(e.TextContent))).ToList());
            o.Add("validation", Validation(doc, ids));
            o.Add("tables", doc.QuerySelectorAll("table").Select(t => (object)t.QuerySelectorAll("tr").Select(r => (object)r.QuerySelectorAll("th,td").Select(c => (object)ids.Normalise(Collapse(c.TextContent))).ToList()).ToList()).ToList());
            o.Add("elementIds", doc.QuerySelectorAll("body [id]").Select(e => (object)(e.LocalName + "#" + e.Id)).ToList());
            o.Add("scripts", doc.QuerySelectorAll("script[src]").Select(e => (object)e.GetAttribute("src")).ToList());
            o.Add("stylesheets", doc.QuerySelectorAll("link[rel=stylesheet]").Select(e => (object)e.GetAttribute("href")).ToList());
            return o;
        }

        private static JsonObject Form(IElement form, Ids ids)
        {
            var o = new JsonObject();
            o.Add("action", ids.Normalise(form.GetAttribute("action")));
            o.Add("method", (form.GetAttribute("method") ?? "get").ToLowerInvariant());
            var fields = new List<object>();
            foreach (var e in form.QuerySelectorAll("input,select,textarea"))
            {
                var f = new JsonObject();
                f.Add("tag", e.LocalName);
                f.Add("type", e.GetAttribute("type"));
                f.Add("name", e.GetAttribute("name"));
                f.Add("id", e.Id);
                var name = e.GetAttribute("name") ?? "";
                f.Add("value", name == "__RequestVerificationToken" ? "{TOKEN}" : ids.Normalise(e.LocalName == "textarea" ? e.TextContent : e.GetAttribute("value")));
                f.Add("validation", e.Attributes.Where(a => a.Name.StartsWith("data-val", StringComparison.Ordinal)).OrderBy(a => a.Name, StringComparer.Ordinal).Select(a => (object)(a.Name + "=" + a.Value)).ToList());
                fields.Add(f);
            }

            o.Add("fields", fields);
            return o;
        }

        private static List<object> Validation(IDocument doc, Ids ids)
        {
            var list = new List<object>();
            foreach (var e in doc.QuerySelectorAll("[data-valmsg-for]"))
            {
                var text = Collapse(e.TextContent);
                if (text.Length > 0)
                {
                    list.Add(e.GetAttribute("data-valmsg-for") + ": " + ids.Normalise(text));
                }
            }

            foreach (var e in doc.QuerySelectorAll(".validation-summary-errors li"))
            {
                var text = Collapse(e.TextContent);
                if (text.Length > 0)
                {
                    list.Add("summary: " + ids.Normalise(text));
                }
            }

            return list;
        }

        // Every text node outside script and style, whitespace collapsed, in document order.
        private static List<object> VisibleText(IElement root, Ids ids)
        {
            var list = new List<object>();
            if (root == null)
            {
                return list;
            }

            foreach (var node in root.Descendants<IText>())
            {
                var parent = node.ParentElement?.LocalName;
                if (parent == "script" || parent == "style" || parent == "noscript")
                {
                    continue;
                }

                var text = Collapse(node.Data);
                if (text.Length > 0)
                {
                    list.Add(ids.Normalise(text));
                }
            }

            return list;
        }

        public static string Collapse(string s)
        {
            return Regex.Replace(s ?? "", @"\s+", " ", RegexOptions.CultureInvariant).Trim();
        }
    }
}
