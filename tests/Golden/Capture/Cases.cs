#nullable disable

// Every question the golden capture asks the site, in order. The site starts from the committed DeveloperTest.sdf
// (four seeded rows) and the cases change its data as they go, so the order is part of the recording: never reorder,
// insert or remove a case after the Phase 0 commit. The replay runs this file unchanged against the new site, started
// from the same four rows.
//
// Inputs that depend on the day are written as placeholders ({TODAY-30y+1d}) and resolved against the recording's
// date, which the replay reads back and gives the new site as its clock. Dates are typed the way the 2014 page's
// jQuery UI datepicker wrote them (MM/dd/yyyy).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GoldenCapture
{
    public static class Cases
    {
        // The four rows of CRUDAjaxExampleWeb/App_Data/DeveloperTest.sdf at commit df9fb35, in the order the file returns them.
        public static readonly string[] SeedIds =
        {
            "ebe4c852-9b68-4fc6-9c0f-4bcf015cccb2",
            "93ffa596-5591-49f5-a003-c2841347511b",
            "83ceea2b-9e7f-4bb4-be6b-c33e634c0824",
            "115546fa-2e9b-4b20-b773-29c325f6d2b0",
        };

        // An id no row has; well formed, so it reaches the action.
        public const string UnknownId = "0badc0de-0000-4000-8000-000000000001";

        public static Func<string, string, string, string> SavePage = (group, name, html) => null;

        private static readonly List<JsonObject> Results = new List<JsonObject>();
        private static SiteClient _site;
        private static Ids _ids;
        private static DateTime _today;

        public static async Task<List<JsonObject>> Run(SiteClient site, DateTime today, Ids ids)
        {
            _site = site;
            _ids = ids;
            _today = today.Date;
            Results.Clear();
            foreach (var id in SeedIds)
            {
                ids.Keep(id);
            }

            ids.Keep(UnknownId);

            // Pages as they stand with the four seeded rows.
            await Get("pages", "index /", "/");
            await Get("pages", "index /Home", "/Home");
            await Get("pages", "index /Home/Index", "/Home/Index");
            await Get("pages", "create form", "/Home/Create");
            for (var i = 0; i < SeedIds.Length; i++)
            {
                await Get("pages", "details seed " + (i + 1), "/Home/Details/" + SeedIds[i]);
            }

            for (var i = 0; i < SeedIds.Length; i++)
            {
                await Get("pages", "edit form seed " + (i + 1), "/Home/Edit/" + SeedIds[i]);
            }

            // The one ajax call, as the details page's button makes it.
            for (var i = 0; i < SeedIds.Length; i++)
            {
                await Ajax("ajax", "age seed " + (i + 1), SeedIds[i]);
            }

            // Create: every way the form can fail. Nothing here may add a row; the index is checked after the group.
            await Post("create invalid", "all empty", "/Home/Create", Person("", "", "", "", ""), "/Home/Create");
            await Post("create invalid", "last name missing", "/Home/Create", Person("Ada", "", "", "12/10/1815", "ada@example.com"), "/Home/Create");
            await Post("create invalid", "last name whitespace", "/Home/Create", Person("Ada", "", "   ", "12/10/1815", ""), "/Home/Create");
            await Post("create invalid", "birth date missing", "/Home/Create", Person("Ada", "", "Lovelace", "", ""), "/Home/Create");
            await Post("create invalid", "birth date not a date", "/Home/Create", Person("Ada", "", "Lovelace", "notadate", ""), "/Home/Create");
            await Post("create invalid", "birth date day first", "/Home/Create", Person("Ada", "", "Lovelace", "29/04/2000", ""), "/Home/Create");
            await Post("create invalid", "birth date out of range", "/Home/Create", Person("Ada", "", "Lovelace", "13/45/2000", ""), "/Home/Create");
            await Post("create invalid", "email no at", "/Home/Create", Person("Ada", "", "Lovelace", "12/10/1815", "not-an-email"), "/Home/Create");
            await Post("create invalid", "email no dot", "/Home/Create", Person("Ada", "", "Lovelace", "12/10/1815", "ada@example"), "/Home/Create");
            await Post("create invalid", "email two ats", "/Home/Create", Person("Ada", "", "Lovelace", "12/10/1815", "ada@@example.com"), "/Home/Create");
            await Post("create invalid", "last name missing and email bad", "/Home/Create", Person("", "", "", "12/10/1815", "x"), "/Home/Create");
            await Get("create invalid", "index after invalid posts", "/", summaryOnly: true);

            // Create: accepted input, each followed by its details page and the ajax age.
            await Create("ada", Person("Ada", "", "Lovelace", "12/10/1815", "ada@example.com"));
            await Create("minimal", Person("", "", "Minimal", "01/01/1990", ""));
            await Create("iso date", Person("Iso", "", "Date", "2000-04-29", ""));
            await Create("date with time", Person("Time", "", "OfDay", "04/29/2000 13:45", ""));
            await Create("upper email", Person("Upper", "", "Email", "01/01/1990", "ADA@EXAMPLE.COM"));
            await Create("one letter tld", Person("One", "", "Letter", "01/01/1990", "a@b.c"));
            await Create("apostrophe ampersand", Person("Mary-Jane", "O'Neil", "O'Brien & Sons", "01/01/1990", "o'brien@example.com"));
            await Create("non-ascii", Person("Zo" + (char)0xEB, (char)0x00D1 + "u" + (char)0x00F1 + "ez", "M" + (char)0x00FC + "ller " + char.ConvertFromUtf32(0x1F600), "01/01/1990", ""));
            await Create("leap day", Person("Leap", "", "Day", "02/29/2000", ""));
            await Create("birthday today", Person("Birthday", "", "Today", "{TODAY-30y}", ""));
            await Create("birthday tomorrow", Person("Birthday", "", "Tomorrow", "{TODAY-30y+1d}", ""));
            await Create("birthday yesterday", Person("Birthday", "", "Yesterday", "{TODAY-30y-1d}", ""));
            await Create("age 3 birthday tomorrow", Person("Three", "", "Tomorrow", "{TODAY-3y+1d}", ""));
            await Create("born today", Person("Born", "", "Today", "{TODAY}", ""));
            await Create("born tomorrow", Person("Born", "", "Tomorrow", "{TODAY+1d}", ""));
            await Create("born 9999", Person("Far", "", "Future", "12/31/9999", ""));
            await Create("born 1753", Person("Old", "", "Est", "01/01/1753", ""));

            // Create: input the 2014 site refuses with an error page rather than a message.
            await CreateRefused("markup in a name", Person("<b>Bold</b>", "", "Markup", "01/01/1990", ""));
            await CreateRefused("script in a name", Person("", "", "<script>alert(1)</script>", "01/01/1990", ""));
            await CreateRefused("last name 101 characters", Person("", "", new string('L', 101), "01/01/1990", ""));
            await CreateRefused("born year 1", Person("Year", "", "One", "01/01/0001", ""));
            await CreateRefused("born 1752", Person("Before", "", "Gregorian", "12/31/1752", ""));
            await Get("create", "index after creates", "/", summaryOnly: true);

            // Edit: a real change, then the failures.
            await EditPost("edit", "change seed 2", SeedIds[1], new[] { Pair("FirstName", "Edited"), Pair("EmailAddress", "edited@example.com") });
            await Get("edit", "details seed 2 after edit", "/Home/Details/" + SeedIds[1]);
            await Ajax("edit", "age seed 2 after edit", SeedIds[1]);
            await EditPost("edit", "clear last name", SeedIds[1], new[] { Pair("LastName", "") });
            await EditPost("edit", "bad email", SeedIds[1], new[] { Pair("EmailAddress", "nope") });
            await EditPost("edit", "bad birth date", SeedIds[1], new[] { Pair("BirthDate", "notadate") });
            await EditPost("edit", "unknown id", SeedIds[1], new[] { Pair("Id", UnknownId) });
            await EditPost("edit", "empty id", SeedIds[1], new[] { Pair("Id", "") });
            await EditPost("edit", "malformed id", SeedIds[1], new[] { Pair("Id", "not-a-guid") });
            await Get("edit", "details seed 2 after failed edits", "/Home/Details/" + SeedIds[1]);

            // Ids that are unknown, malformed or missing; actions and pages that do not exist.
            await Get("errors", "details unknown id", "/Home/Details/" + UnknownId);
            await Get("errors", "details malformed id", "/Home/Details/not-a-guid");
            await Get("errors", "details no id", "/Home/Details");
            await Get("errors", "details id in query", "/Home/Details?id=" + SeedIds[0]);
            await Get("errors", "edit unknown id", "/Home/Edit/" + UnknownId);
            await Get("errors", "edit malformed id", "/Home/Edit/not-a-guid");
            await AjaxRaw("errors", "age unknown id", UnknownId);
            await AjaxRaw("errors", "age malformed id", "not-a-guid");
            await AjaxRaw("errors", "age no id", null);
            await Get("errors", "age by GET", "/Home/CalculateAge/" + SeedIds[0]);
            await Get("errors", "delete", "/Home/Delete/" + SeedIds[0]);
            await Get("errors", "unknown action", "/Home/Nope");
            await Get("errors", "unknown controller", "/Nope");
            await Get("errors", "unknown page", "/Nope/Nope/Nope/Nope");

            // Files the pages load, and a few a browser asks for.
            await Get("static", "site css", "/Content/Site.css");
            await Get("static", "person script", "/Scripts/personapp.js");
            await Get("static", "loading gif", "/Content/ajax-loader.gif");
            await Get("static", "favicon", "/favicon.ico");
            await Get("static", "robots", "/robots.txt");

            await Get("final", "index at the end", "/", summaryOnly: true);
            return Results;
        }

        private static KeyValuePair<string, string> Pair(string k, string v)
        {
            return new KeyValuePair<string, string>(k, v);
        }

        private static List<KeyValuePair<string, string>> Person(string first, string middle, string last, string birth, string email)
        {
            return new List<KeyValuePair<string, string>>
            {
                Pair("FirstName", first),
                Pair("MiddleName", middle),
                Pair("LastName", last),
                Pair("BirthDate", birth),
                Pair("EmailAddress", email),
            };
        }

        // {TODAY}, {TODAY-30y}, {TODAY-30y+1d}, {TODAY+1d}: the recording's date moved by whole years, then days.
        public static string Resolve(string value)
        {
            return Regex.Replace(value ?? "", @"\{TODAY([+-]\d+y)?([+-]\d+d)?\}", m =>
            {
                var d = _today;
                if (m.Groups[1].Success)
                {
                    d = d.AddYears(int.Parse(m.Groups[1].Value.TrimEnd('y'), CultureInfo.InvariantCulture));
                }

                if (m.Groups[2].Success)
                {
                    d = d.AddDays(int.Parse(m.Groups[2].Value.TrimEnd('d'), CultureInfo.InvariantCulture));
                }

                return d.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
            });
        }

        private static JsonObject Request(string method, string path, IEnumerable<KeyValuePair<string, string>> fields)
        {
            var o = new JsonObject();
            o.Add("method", method);
            o.Add("path", _ids.Normalise(path));
            if (fields != null)
            {
                o.Add("form", fields.Select(f => (object)(f.Key + "=" + _ids.Normalise(f.Value))).ToList());
                var resolved = fields.Where(f => Resolve(f.Value) != f.Value).Select(f => (object)(f.Key + "=" + Resolve(f.Value))).ToList();
                if (resolved.Count > 0)
                {
                    o.Add("resolved", resolved);
                }
            }

            return o;
        }

        private static void Add(string group, string name, JsonObject request, Response r, bool summaryOnly)
        {
            var result = new JsonObject();
            result.Add("status", r.Status);
            result.Add("contentType", r.ContentType);
            result.Add("location", _ids.Normalise(r.Location));
            var text = r.Text;
            if (r.ContentType == "text/html" && Page.IsErrorPage(text))
            {
                result.Add("error", Page.ErrorPage(text));
            }
            else if (r.ContentType == "text/html")
            {
                result.Add("summary", Page.Summary(text, _ids));
                if (!summaryOnly)
                {
                    result.Add("newline", text.Contains("\r\n", StringComparison.Ordinal) ? "CRLF" : "LF");
                    result.Add("html", _ids.Normalise(text).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(l => (object)l).ToList());
                    SavePage(group, name, text);
                }
            }
            else if (r.ContentType == "application/json")
            {
                result.Add("json", _ids.Normalise(text));
            }
            else
            {
                result.Add("length", r.Body.Length);
                result.Add("sha256", Convert.ToHexString(SHA256.HashData(r.Body)).ToLowerInvariant());
            }

            var o = new JsonObject();
            o.Add("group", group);
            o.Add("name", name);
            o.Add("request", request);
            o.Add("result", result);
            Results.Add(o);
        }

        private static async Task<Response> Get(string group, string name, string path, bool summaryOnly = false)
        {
            var r = await _site.Get(path);
            Add(group, name, Request("GET", path, null), r, summaryOnly);
            return r;
        }

        private static async Task<Response> Post(string group, string name, string path, List<KeyValuePair<string, string>> fields, string formPage)
        {
            var sent = fields.Select(f => Pair(f.Key, Resolve(f.Value))).ToList();
            var r = await _site.PostForm(path, sent, formPage);
            Add(group, name, Request("POST", path, fields), r, false);
            return r;
        }

        private static async Task Ajax(string group, string name, string id)
        {
            var fields = new List<KeyValuePair<string, string>> { Pair("id", id) };
            var r = await _site.PostForm("/Home/CalculateAge", fields, "/Home/Details/" + id, ajax: true);
            Add(group, name, Request("POST", "/Home/CalculateAge", fields), r, false);
        }

        // The ajax call without a details page to read a token from: the new site may refuse it for that reason alone.
        private static async Task AjaxRaw(string group, string name, string id)
        {
            var fields = id == null ? new List<KeyValuePair<string, string>>() : new List<KeyValuePair<string, string>> { Pair("id", id) };
            var r = await _site.PostForm("/Home/CalculateAge", fields, "/Home/Details/" + SeedIds[0], ajax: true);
            Add(group, name, Request("POST", "/Home/CalculateAge", fields), r, false);
        }

        private static async Task<List<string>> Listed()
        {
            return Page.ListedIds((await _site.Get("/")).Text);
        }

        private static async Task Create(string name, List<KeyValuePair<string, string>> fields)
        {
            var before = await Listed();
            await Post("create", name, "/Home/Create", fields, "/Home/Create");
            var added = (await Listed()).Where(id => !before.Contains(id, StringComparer.OrdinalIgnoreCase)).ToList();
            if (added.Count != 1)
            {
                Results.Add(Note("create", name + ": rows added", added.Count.ToString(CultureInfo.InvariantCulture)));
                return;
            }

            _ids.Name(added[0], name);
            await Get("create", name + ": details", "/Home/Details/" + added[0]);
            await Ajax("create", name + ": age", added[0]);
        }

        private static async Task CreateRefused(string name, List<KeyValuePair<string, string>> fields)
        {
            var before = await Listed();
            await Post("create refused", name, "/Home/Create", fields, "/Home/Create");
            var added = (await Listed()).Where(id => !before.Contains(id, StringComparer.OrdinalIgnoreCase)).ToList();
            Results.Add(Note("create refused", name + ": rows added", added.Count.ToString(CultureInfo.InvariantCulture)));
            if (added.Count == 1)
            {
                _ids.Name(added[0], name);
                await Get("create refused", name + ": details", "/Home/Details/" + added[0]);
                await Ajax("create refused", name + ": age", added[0]);
            }
        }

        // An edit as a browser makes it: the form's current values, with the changes typed over them.
        private static async Task EditPost(string group, string name, string id, KeyValuePair<string, string>[] changes)
        {
            var page = await _site.Get("/Home/Edit/" + id);
            var fields = Page.FormValues(page.Text);
            foreach (var c in changes)
            {
                var i = fields.FindIndex(f => f.Key == c.Key);
                if (i >= 0)
                {
                    fields[i] = c;
                }
                else
                {
                    fields.Add(c);
                }
            }

            var r = await _site.PostForm("/Home/Edit/" + id, fields);
            Add(group, name, Request("POST", "/Home/Edit/" + id, fields.Where(f => f.Key != "__RequestVerificationToken")), r, false);
        }

        private static JsonObject Note(string group, string name, string value)
        {
            var o = new JsonObject();
            o.Add("group", group);
            o.Add("name", name);
            o.Add("note", value);
            return o;
        }
    }
}
