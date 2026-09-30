#nullable disable

// Golden capture of the 2014 site (package-modernize, repository variant for websites, Phase 0).
// Usage: dotnet run -c Release -- <base url> <output.json> <host notes file> [pages folder]
//   <base url>         the running 2014 site, for example http://localhost:61036
//   <host notes file>  "key: value" lines written by ../capture-old-site.ps1 (how the site was built and hosted)
//   [pages folder]     each recorded page's HTML as <group>/<name>/index.html, for the screenshots
// Writes UTF-8 without BOM, LF line endings, ASCII only. Never edit a recording; never regenerate it from new code.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GoldenCapture
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            if (args.Length < 3 || args.Length > 4)
            {
                Console.Error.WriteLine("usage: Capture <base url> <output.json> <host notes file> [pages folder]");
                return 2;
            }

            var pages = args.Length == 4 ? Path.GetFullPath(args[3]) : null;
            if (pages != null)
            {
                Cases.SavePage = (group, name, html) =>
                {
                    var dir = Path.Combine(pages, Slug(group), Slug(name));
                    Directory.CreateDirectory(dir);
                    var file = Path.Combine(dir, "index.html");
                    File.WriteAllText(file, html, new UTF8Encoding(false));
                    return file;
                };
            }

            var started = DateTime.Now;
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = true, CookieContainer = new CookieContainer() };
            using var http = new HttpClient(handler) { BaseAddress = new Uri(args[0]), Timeout = TimeSpan.FromSeconds(120) };
            var ids = new Ids();
            var cases = await Cases.Run(new SiteClient(http), started, ids);
            var finished = DateTime.Now;

            var root = new JsonObject();
            root.Add("source", "CRUDAjaxExampleWeb at commit df9fb35 (2014-04-26, tag v1.0.0), built and hosted as the host notes say");
            root.Add("data", "CRUDAjaxExampleWeb/App_Data/DeveloperTest.sdf at commit df9fb35, a fresh copy for this run");
            foreach (var line in File.ReadAllLines(args[2]))
            {
                var i = line.IndexOf(": ", StringComparison.Ordinal);
                if (i > 0)
                {
                    root.Add("host." + line.Substring(0, i).Trim(), line.Substring(i + 2).Trim());
                }
            }

            root.Add("client", "tests/Golden/Capture on " + RuntimeInformation.FrameworkDescription + ", " + RuntimeInformation.OSDescription.Trim());
            root.Add("clientCulture", CultureInfo.CurrentCulture.Name);
            root.Add("timeZone", TimeZoneInfo.Local.Id + " (UTC offset " + TimeZoneInfo.Local.GetUtcOffset(started).ToString() + ")");
            root.Add("today", started.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            root.Add("sameDay", started.Date == finished.Date);
            root.Add("normalised", new List<object>
            {
                "ids the site created are written {NEW:<case name>}; the seeded ids and the fixed unknown id stay as they are",
                "ASP.NET error pages are reduced to their title, description, exception type and message (the stack trace and version lines name the capture machine)",
                "an index page recorded after rows were added keeps only its summary: the 2014 site lists rows in its database's order of the new random ids",
                "html is split into lines at LF after CRLF became LF; the page's newline is in \"newline\"",
                "form inputs with {TODAY...} placeholders were sent resolved against \"today\"; the resolved values are in \"resolved\"",
            });
            root.Add("note", "Golden answers of the 2014 site, recorded by tests/Golden/Capture. Never edit; never regenerate from new code.");
            root.Add("unexpectedIds", ids.Unexpected.Distinct().Count());
            root.Add("caseCount", cases.Count);
            root.Add("cases", cases);

            File.WriteAllText(args[1], Json.Write(root), new UTF8Encoding(false));
            Console.Error.WriteLine("wrote " + cases.Count + " cases to " + args[1] + (ids.Unexpected.Count > 0 ? "; UNEXPECTED IDS: " + ids.Unexpected.Count : ""));
            return ids.Unexpected.Count > 0 || started.Date != finished.Date ? 1 : 0;
        }

        private static string Slug(string s)
        {
            return Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        }
    }
}
