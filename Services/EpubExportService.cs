using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace NileFusion.BookConverter.Services;

public class EpubExportService
{
    /// <summary>
    /// Generates an EPUB 3.0 file from the extracted Arabic text.
    /// Chapters are split on the Arabic page separator "--- الصفحة N ---".
    /// </summary>
    public async Task SaveEpubAsync(string epubPath, string allText, string pdfTitle, CancellationToken ct = default)
    {
        // Parse pages out of the combined text
        var pages = SplitIntoPages(allText);

        await Task.Run(() =>
        {
            if (File.Exists(epubPath)) File.Delete(epubPath);

            using var zip = ZipFile.Open(epubPath, ZipArchiveMode.Create);

            // ── mimetype (must be first and STORED, not deflated) ──────────────
            var mimeEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var w = new StreamWriter(mimeEntry.Open(), Encoding.ASCII))
                w.Write("application/epub+zip");

            // ── META-INF/container.xml ─────────────────────────────────────────
            AddEntry(zip, "META-INF/container.xml", @"<?xml version=""1.0"" encoding=""UTF-8""?>
<container version=""1.0"" xmlns=""urn:oasis:names:tc:opendocument:xmlns:container"">
  <rootfiles>
    <rootfile full-path=""EPUB/content.opf"" media-type=""application/oebps-package+xml""/>
  </rootfiles>
</container>");

            // ── EPUB/styles.css ────────────────────────────────────────────────
            AddEntry(zip, "EPUB/styles.css", @"
body {
  font-family: 'Traditional Arabic', 'Amiri', 'Arial', sans-serif;
  font-size: 1.1em;
  line-height: 1.9;
  direction: rtl;
  text-align: right;
  margin: 1.5em 2em;
  color: #1a1a1a;
  background: #fff;
}
h1 { font-size: 1.6em; text-align: center; margin-bottom: 1em; }
h2 { font-size: 1.2em; border-bottom: 1px solid #ccc; padding-bottom: .3em; margin-top: 1.5em; }
p  { margin: .6em 0; }
p.centered {
  text-align: center;
  direction: rtl;
  font-weight: 600;
  margin: 1em auto;
}
");

            // ── EPUB/toc.xhtml (Navigation document) ──────────────────────────
            var navItems = new StringBuilder();
            for (int i = 0; i < pages.Count; i++)
                navItems.AppendLine($@"      <li><a href=""chapter{i + 1}.xhtml"">{pages[i].title}</a></li>");

            AddEntry(zip, "EPUB/toc.xhtml", $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE html>
<html xmlns=""http://www.w3.org/1999/xhtml""
      xmlns:epub=""http://www.idpf.org/2007/ops"" xml:lang=""ar"" lang=""ar"" dir=""rtl"">
<head><meta charset=""UTF-8""/><title>فهرس المحتوى</title></head>
<body>
  <nav epub:type=""toc"" id=""toc"">
    <h1>فهرس المحتوى</h1>
    <ol>
{navItems}
    </ol>
  </nav>
</body>
</html>");

            // ── Chapter xhtml files ────────────────────────────────────────────
            foreach (var (idx, title, body) in pages.Select((p, i) => (i + 1, p.title, p.body)))
            {
                var paragraphs = BuildParagraphs(body);
                AddEntry(zip, $"EPUB/chapter{idx}.xhtml", $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE html>
<html xmlns=""http://www.w3.org/1999/xhtml"" xml:lang=""ar"" lang=""ar"" dir=""rtl"">
<head>
  <meta charset=""UTF-8""/>
  <title>{EscapeXml(title)}</title>
  <link rel=""stylesheet"" type=""text/css"" href=""styles.css""/>
</head>
<body>
  <h2>{EscapeXml(title)}</h2>
{paragraphs}
</body>
</html>");
            }

            // ── EPUB/content.opf ───────────────────────────────────────────────
            var uid = Guid.NewGuid().ToString();
            var manifestItems = new StringBuilder();
            var spineItems = new StringBuilder();

            // nav item
            manifestItems.AppendLine(@"    <item id=""toc"" href=""toc.xhtml"" media-type=""application/xhtml+xml"" properties=""nav""/>");
            manifestItems.AppendLine(@"    <item id=""css"" href=""styles.css"" media-type=""text/css""/>");

            for (int i = 1; i <= pages.Count; i++)
            {
                manifestItems.AppendLine($@"    <item id=""chapter{i}"" href=""chapter{i}.xhtml"" media-type=""application/xhtml+xml""/>");
                spineItems.AppendLine($@"    <itemref idref=""chapter{i}""/>");
            }

            AddEntry(zip, "EPUB/content.opf", $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<package xmlns=""http://www.idpf.org/2007/opf"" version=""3.0"" unique-identifier=""uid"" xml:lang=""ar"">
  <metadata xmlns:dc=""http://purl.org/dc/elements/1.1/"">
    <dc:identifier id=""uid"">{uid}</dc:identifier>
    <dc:title>{EscapeXml(pdfTitle)}</dc:title>
    <dc:language>ar</dc:language>
    <meta property=""dcterms:modified"">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</meta>
  </metadata>
  <manifest>
{manifestItems}
  </manifest>
  <spine page-progression-direction=""rtl"">
    <itemref idref=""toc""/>
{spineItems}
  </spine>
</package>");
        }, ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static List<(string title, string body)> SplitIntoPages(string allText)
    {
        var result = new List<(string, string)>();
        // Pattern: "--- الصفحة N ---"
        var pattern = new Regex(@"---\s*الصفحة\s+(\d+)\s*---", RegexOptions.Multiline);
        var matches = pattern.Matches(allText);

        if (matches.Count == 0)
        {
            // No separators → single chapter
            result.Add(("النص المستخرج", allText.Trim()));
            return result;
        }

        for (int i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            int start = m.Index + m.Length;
            int end = (i + 1 < matches.Count) ? matches[i + 1].Index : allText.Length;
            string body = allText[start..end].Trim();
            result.Add(($"الصفحة {m.Groups[1].Value}", body));
        }

        return result;
    }

    private static string BuildParagraphs(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "  <p></p>";

        var sb = new StringBuilder();
        foreach (var rawLine in body.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                sb.AppendLine("  <p>&#160;</p>");
                continue;
            }

            bool isCentered = line.StartsWith("\t");
            string text = line.Trim();

            if (isCentered)
            {
                sb.AppendLine($"  <p class=\"centered\">{EscapeXml(text)}</p>");
            }
            else
            {
                sb.AppendLine($"  <p>{EscapeXml(text)}</p>");
            }
        }
        return sb.ToString();
    }

    private static string EscapeXml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static void AddEntry(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    /// <summary>
    /// Reads and extracts plain text from an EPUB file.
    /// </summary>
    public static async Task<string> ReadEpubTextAsync(string epubPath)
    {
        return await Task.Run(() =>
        {
            using var zip = ZipFile.OpenRead(epubPath);
            var sb = new StringBuilder();
            var htmlEntries = zip.Entries
                .Where(e => e.FullName.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase) ||
                            e.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                            e.FullName.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
                .Where(e => !e.FullName.Contains("toc.", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FullName);

            int chapterNum = 1;
            foreach (var entry in htmlEntries)
            {
                using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                string html = reader.ReadToEnd();
                string text = StripHtml(html);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine($"--- الفصل {chapterNum++} ---");
                    sb.AppendLine(text.Trim());
                    sb.AppendLine();
                }
            }

            return sb.ToString().Trim();
        });
    }

    private static string StripHtml(string html)
    {
        string text = Regex.Replace(html, @"(?i)<(br|/p|/div|/h[1-6]|/li)\s*/?>", "\n");
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\r", "");
        text = Regex.Replace(text, @"\n\s*\n\s*\n+", "\n\n");
        return text.Trim();
    }
}

