using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using COMMON;
using Dapper;
using DBHelper;
using MODEL;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace KieliWeb.Setup;

/// <summary>
/// The automatic news of «Жаңалық көздері» (admin → Жаңалықтар): every hour the RSS/Atom feed of each source is read
/// and the items about migration become news cards. A card keeps only what a feed offers for syndication — the
/// headline, the feed's own short summary (cut to <see cref="ExcerptLength"/> characters), the source name and the
/// date — and opens the original article; no full text and no pictures are copied. Requests name the site in the
/// User-Agent, follow the publisher's robots.txt, ask only for what changed (ETag / Last-Modified) and never go to
/// private network addresses. A card deleted in the admin is never added again.
/// </summary>
public static class NewsCollector
{
	public const string UserAgent = "Mozilla/5.0 (compatible; KieliNewsBot/1.0; +https://kieli.kz)";

	/// <summary>The rules a new source starts with: Kazakh words that only migration news use.</summary>
	public const string DefaultKeywords = "көші-қон, қандас, ықтиярхат, ата жолы картас, тұрақты тұруға рұқсат, азаматтығын алу, азаматтыққа қабылда, " +
		"азаматтығына қабылда, оралман, этникалық қазақ, шетелдегі қазақ, отандастар қоры, дүниежүзі қазақтарының, " +
		"мигрант + қазақстан, миграция + қазақстан, визасыз + қазақстан";

	private const string BotToken = "kielinewsbot";

	private const int ExcerptLength = 280;

	private const int MaxFeedBytes = 5 * 1024 * 1024;

	private const int MaxAgeDays = 14;

	private static readonly HttpClient Http = CreateClient();

	private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);

	private static readonly ConcurrentDictionary<string, (DateTime Until, List<(bool Allow, string Path)> Rules)> Robots = new ConcurrentDictionary<string, (DateTime, List<(bool, string)>)>();

	private sealed record Response(HttpStatusCode Status, byte[] Body, string ETag, string LastModified);

	public sealed record FeedItem(string Title, string Link, string Summary, DateTimeOffset? Date);

	/// <summary>What one run did, in words for the admin.</summary>
	public sealed class Result
	{
		public int Sources { get; set; }

		public int Added { get; set; }

		public List<string> Errors { get; } = new List<string>();

		public bool Busy { get; set; }

		public string Summary => Busy ? "Тексеріс қазір жүріп жатыр — сәлден соң қайталаңыз."
			: $"{Sources} көз тексерілді, {Added} жаңалық қосылды." + (Errors.Count > 0 ? " " + string.Join(" ", Errors) : string.Empty);
	}

	/// <summary>Checks every enabled source (or only <paramref name="sourceId"/>) and adds the new matching items.</summary>
	public static async Task<Result> RunAsync(IMemoryCache cache, int sourceId = 0)
	{
		Result result = new Result();
		if (!await Gate.WaitAsync(0))
		{
			result.Busy = true;
			return result;
		}
		try
		{
			using IDbConnection connection = Utilities.GetOpenConnection();
			List<Newssource> sources = connection.Query<Newssource>("select * from newssource where qStatus = 0 and (id = @sourceId or (@sourceId = 0 and isEnabled = 1)) order by displayOrder, id", new { sourceId }).ToList();
			int newsCategory = connection.ExecuteScalar<int?>("select id from articlecategory where qStatus = 0 and slug = 'news' order by id limit 1") ?? 0;
			foreach (Newssource source in sources)
			{
				result.Sources++;
				(string status, int added) = await CheckSource(connection, source, newsCategory);
				result.Added += added;
				if (status.StartsWith("Қате", StringComparison.Ordinal) || status.StartsWith("robots", StringComparison.Ordinal))
				{
					result.Errors.Add(source.Name + ": " + status);
				}
			}
			if (result.Added > 0)
			{
				ContentStore.Clear(cache);
			}
			Log.Information("News: {Sources} sources checked, {Added} items added", result.Sources, result.Added);
		}
		catch (Exception exception)
		{
			Log.Error(exception, "News collector");
			result.Errors.Add("Қате: " + exception.Message);
		}
		finally
		{
			Gate.Release();
		}
		return result;
	}

	private static async Task<(string Status, int Added)> CheckSource(IDbConnection connection, Newssource source, int newsCategory)
	{
		int now = UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
		string status;
		int added = 0;
		string etag = source.Etag, lastModified = source.LastModified;
		try
		{
			if (!Uri.TryCreate((source.FeedUrl ?? string.Empty).Trim(), UriKind.Absolute, out Uri feed) || (feed.Scheme != Uri.UriSchemeHttp && feed.Scheme != Uri.UriSchemeHttps))
			{
				status = "Қате: арна сілтемесі http(s):// деп басталуы керек.";
			}
			else if (!await IsPublic(feed))
			{
				status = "Қате: бұл ішкі желінің мекенжайы — сервер оған сұрау жібермейді.";
			}
			else if (!await RobotsAllow(feed))
			{
				status = "robots.txt бұл арнаны оқуға рұқсат бермейді (не оқылмады) — тексерілмеді.";
			}
			else
			{
				Response response = await Fetch(feed, etag, lastModified);
				if (response.Status == HttpStatusCode.NotModified)
				{
					status = "Арнада жаңа жазба жоқ.";
				}
				else if ((int)response.Status is < 200 or >= 300)
				{
					status = $"Қате: сайт {(int)response.Status} деп жауап берді.";
				}
				else
				{
					List<FeedItem> items = ParseFeed(response.Body, feed);
					List<string[]> rules = Rules(source.Keywords);
					List<string> excluded = Rules(source.ExcludeWords).SelectMany(r => r).ToList();
					DateTimeOffset oldest = DateTimeOffset.UtcNow.AddDays(-MaxAgeDays);
					List<FeedItem> matching = items.Where(i => (i.Date ?? DateTimeOffset.UtcNow) >= oldest && Matches(i, rules, excluded)).ToList();
					List<FeedItem> fresh = matching.Where(i => !Known(connection, source.Id, i)).OrderByDescending(i => i.Date ?? DateTimeOffset.UtcNow)
						.Take(Math.Clamp(source.MaxPerRun, 1, 20)).ToList();
					foreach (FeedItem item in fresh.OrderBy(i => i.Date ?? DateTimeOffset.UtcNow))
					{
						connection.Insert(ToArticle(item, source, source.CategoryId > 0 ? source.CategoryId : newsCategory, now));
						added++;
					}
					etag = response.ETag;
					lastModified = response.LastModified;
					status = $"{items.Count} жазба оқылды, {matching.Count} тақырыпқа сай, {added} жаңасы қосылды.";
				}
			}
		}
		catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or XmlException or InvalidOperationException or IOException or SocketException)
		{
			status = "Қате: " + (exception is TaskCanceledException ? "сайт уақытында жауап бермеді." : exception.Message);
			Log.Warning("News source {Name}: {Message}", source.Name, exception.Message);
		}
		connection.Execute("update newssource set lastCheckTime = @now, lastStatus = @status, etag = @etag, lastModified = @lastModified, addedCount = addedCount + @added where id = @id",
			new { now, status = status.Length > 500 ? status.Substring(0, 500) : status, etag = etag ?? string.Empty, lastModified = lastModified ?? string.Empty, added, id = source.Id });
		return (status, added);
	}

	/// <summary>Already on the site, or deleted there: the same address, or the same headline from this source.</summary>
	private static bool Known(IDbConnection connection, int sourceId, FeedItem item) =>
		connection.ExecuteScalar<int>("select count(1) from article where sourceUrl = @link or linkUrl = @link or (newsSourceId = @sourceId and title = @title)",
			new { link = item.Link, sourceId, title = Cut(item.Title, 250) }) > 0;

	private static Article ToArticle(FeedItem item, Newssource source, int categoryId, int now)
	{
		DateTimeOffset date = item.Date ?? DateTimeOffset.UtcNow;
		if (date > DateTimeOffset.UtcNow)
		{
			date = DateTimeOffset.UtcNow;
		}
		return new Article
		{
			CategoryId = categoryId,
			Slug = "news-" + source.Id + "-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.Link))).Substring(0, 10).ToLowerInvariant(),
			Title = Cut(item.Title, 250),
			Excerpt = Unfinished(Cut(item.Summary, ExcerptLength)),
			BodyHtml = string.Empty,
			LinkUrl = item.Link,
			CoverImageUrl = string.Empty,
			CoverTone = "sky",
			CoverIcon = "i-globe",
			// Kazakhstan has one time zone, UTC+5
			DateText = date.ToOffset(TimeSpan.FromHours(5)).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
			PublishTime = (int)date.ToUnixTimeSeconds(),
			ReadMinutes = 0,
			SourceName = Cut(source.Name, 128),
			SourceUrl = item.Link,
			NewsSourceId = source.Id,
			AuthorName = string.Empty,
			ServiceKey = string.Empty,
			IsImportant = 0,
			IsPublished = (byte)(source.AutoPublish == 1 ? 1 : 0),
			SeoDescription = string.Empty,
			ViewCount = 0,
			DisplayOrder = 0,
			AddTime = now,
			UpdateTime = now,
			QStatus = 0
		};
	}

	// ---- keywords ----------------------------------------------------------------------------

	/// <summary>"a, b + c" → [[a], [b, c]]: an item matches when every word of one rule is in its headline or summary.</summary>
	public static List<string[]> Rules(string text) => (text ?? string.Empty).Split(new[] { '\n', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
		.Select(r => r.Split('+').Select(Normalize).Where(w => w.Length > 0).ToArray()).Where(r => r.Length > 0).ToList();

	public static bool Matches(FeedItem item, List<string[]> rules, List<string> excluded)
	{
		string text = Normalize(item.Title + " " + item.Summary);
		return (rules.Count == 0 || rules.Any(r => r.All(text.Contains))) && !excluded.Any(text.Contains);
	}

	/// <summary>Lower case, no quotes, one kind of dash and space: «Ата жолы» картасы ~ "ата жолы" картасы.</summary>
	private static string Normalize(string s)
	{
		s = Regex.Replace((s ?? string.Empty).ToLowerInvariant(), "[«»“”„\"'’`]", string.Empty);
		s = Regex.Replace(s, "[‐-―−]", "-");
		s = Regex.Replace(s, @"\s*-\s*", "-");
		return Regex.Replace(s, @"\s+", " ").Trim();
	}

	// ---- feeds -------------------------------------------------------------------------------

	/// <summary>The items of an RSS (0.9x, 1.0, 2.0) or Atom feed; relative links are made absolute.</summary>
	public static List<FeedItem> ParseFeed(byte[] body, Uri feedUrl)
	{
		XmlReaderSettings settings = new XmlReaderSettings
		{
			DtdProcessing = DtdProcessing.Ignore,
			XmlResolver = null,
			MaxCharactersFromEntities = 1024,
			IgnoreComments = true,
			IgnoreProcessingInstructions = true
		};
		using MemoryStream stream = new MemoryStream(body);
		using XmlReader reader = XmlReader.Create(stream, settings);
		XDocument doc = XDocument.Load(reader);
		const string atom = "http://www.w3.org/2005/Atom";
		List<FeedItem> items = new List<FeedItem>();
		foreach (XElement e in doc.Descendants().Where(e => e.Name.LocalName == "item" || (e.Name.LocalName == "entry" && e.Name.NamespaceName == atom)).Take(500))
		{
			bool isAtom = e.Name.NamespaceName == atom;
			string Child(string name, string ns = null) => e.Elements().FirstOrDefault(c => c.Name.LocalName == name && (ns == null || c.Name.NamespaceName == ns))?.Value;
			string link = isAtom
				? e.Elements().Where(c => c.Name.LocalName == "link" && ((string)c.Attribute("rel") ?? "alternate") == "alternate").Select(c => (string)c.Attribute("href")).FirstOrDefault()
				: Child("link", string.Empty) ?? Child("link");
			if (string.IsNullOrWhiteSpace(link) && !isAtom)
			{
				XElement guid = e.Elements().FirstOrDefault(c => c.Name.LocalName == "guid");
				if (guid != null && (string)guid.Attribute("isPermaLink") != "false")
				{
					link = guid.Value;
				}
			}
			string title = Text(Child("title"));
			if (title.Length == 0 || string.IsNullOrWhiteSpace(link) || !Uri.TryCreate(feedUrl, link.Trim(), out Uri url) || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
			{
				continue;
			}
			// the feed's summary only: RSS «description», Atom «summary» (or its «content», cut short below)
			string summary = Text(isAtom ? Child("summary", atom) ?? Child("content", atom) : Child("description"));
			if (summary.StartsWith(title, StringComparison.OrdinalIgnoreCase))
			{
				summary = summary.Substring(title.Length).TrimStart(' ', '.', ':', '—', '-');
			}
			DateTimeOffset? date = ParseDate(Child("pubDate") ?? Child("published", atom) ?? Child("updated", atom) ?? Child("date"));
			items.Add(new FeedItem(title, Clean(url), summary, date));
		}
		return items;
	}

	/// <summary>One address per article: no #fragment and no utm_… tracking parameters.</summary>
	private static string Clean(Uri url)
	{
		UriBuilder b = new UriBuilder(url) { Fragment = string.Empty };
		if (b.Query.Length > 1)
		{
			string query = string.Join("&", b.Query.TrimStart('?').Split('&').Where(p => p.Length > 0 && !p.StartsWith("utm_", StringComparison.OrdinalIgnoreCase)));
			b.Query = query;
		}
		string s = b.Uri.AbsoluteUri;
		return s.Length > 255 ? url.GetLeftPart(UriPartial.Path) : s;
	}

	/// <summary>Plain text from a feed field: tags removed, entities decoded (twice, for HTML that was escaped once more).</summary>
	public static string Text(string html)
	{
		if (string.IsNullOrWhiteSpace(html))
		{
			return string.Empty;
		}
		static string Strip(string x) => Regex.Replace(Regex.Replace(x, @"<(script|style)\b.*?</\1\s*>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase), "<[^>]*>", " ");
		string s = html;
		for (int i = 0; i < 3; i++)
		{
			string next = WebUtility.HtmlDecode(Strip(s));
			if (next == s)
			{
				break;
			}
			s = next;
		}
		return Regex.Replace(Strip(s).Replace(' ', ' ').Replace("­", string.Empty), @"\s+", " ").Trim();
	}

	/// <summary>Feeds often cut their summary mid-sentence: show that it goes on at the source.</summary>
	private static string Unfinished(string s) => s.Length == 0 || ".!?…»\")".Contains(s[^1]) ? s : s.TrimEnd(' ', ',', ';', ':', '—', '-') + "…";

	private static string Cut(string s, int max)
	{
		s ??= string.Empty;
		if (s.Length <= max)
		{
			return s;
		}
		int space = s.LastIndexOf(' ', max - 1);
		return s.Substring(0, space > max / 2 ? space : max - 1).TrimEnd(' ', ',', ';', ':', '—', '-') + "…";
	}

	/// <summary>RSS dates ("Thu, 01 Oct 2026 13:05:20 +0600", "… GMT") and Atom dates (ISO 8601).</summary>
	public static DateTimeOffset? ParseDate(string s)
	{
		if (string.IsNullOrWhiteSpace(s))
		{
			return null;
		}
		s = s.Trim();
		string t = Regex.Replace(s, @"^[A-Za-z]{3,9},?\s*", string.Empty);
		t = Regex.Replace(t, @"\s(GMT|UTC|UT|Z)$", " +00:00", RegexOptions.IgnoreCase);
		t = Regex.Replace(t, @"\s([+-])(\d{2})(\d{2})$", " $1$2:$3");
		string[] formats = { "d MMM yyyy HH:mm:ss zzz", "d MMM yyyy HH:mm zzz", "d MMM yy HH:mm:ss zzz" };
		if (DateTimeOffset.TryParseExact(t, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTimeOffset rfc))
		{
			return rfc;
		}
		return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset other) ? other : null;
	}

	// ---- polite fetching ---------------------------------------------------------------------

	private static HttpClient CreateClient()
	{
		HttpClientHandler handler = new HttpClientHandler
		{
			// redirects are followed by hand so that every target address is checked (see Fetch)
			AllowAutoRedirect = false,
			AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
			UseCookies = false
		};
		HttpClient client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(25) };
		client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
		client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/rss+xml, application/atom+xml, application/xml;q=0.9, text/xml;q=0.8, */*;q=0.1");
		return client;
	}

	private static async Task<Response> Fetch(Uri url, string etag, string lastModified)
	{
		for (int hop = 0; hop < 5; hop++)
		{
			if (!await IsPublic(url))
			{
				throw new InvalidOperationException("ішкі желінің мекенжайына сұрау жіберілмейді (" + url.Host + ").");
			}
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
			if (!string.IsNullOrEmpty(etag))
			{
				request.Headers.TryAddWithoutValidation("If-None-Match", etag);
			}
			if (!string.IsNullOrEmpty(lastModified))
			{
				request.Headers.TryAddWithoutValidation("If-Modified-Since", lastModified);
			}
			using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
			int code = (int)response.StatusCode;
			if (code is >= 300 and < 400 && code != 304 && response.Headers.Location != null)
			{
				url = new Uri(url, response.Headers.Location);
				continue;
			}
			byte[] body = Array.Empty<byte>();
			if (response.IsSuccessStatusCode)
			{
				if (response.Content.Headers.ContentLength > MaxFeedBytes)
				{
					throw new InvalidOperationException("арна тым үлкен (5 МБ-тан асады).");
				}
				await using Stream stream = await response.Content.ReadAsStreamAsync();
				using MemoryStream copy = new MemoryStream();
				byte[] buffer = new byte[81920];
				int read;
				while ((read = await stream.ReadAsync(buffer)) > 0)
				{
					copy.Write(buffer, 0, read);
					if (copy.Length > MaxFeedBytes)
					{
						throw new InvalidOperationException("арна тым үлкен (5 МБ-тан асады).");
					}
				}
				body = copy.ToArray();
			}
			return new Response(response.StatusCode, body, response.Headers.ETag?.ToString() ?? string.Empty, response.Content.Headers.LastModified?.ToString("R") ?? string.Empty);
		}
		throw new InvalidOperationException("сайт тым көп рет басқа бетке жіберді.");
	}

	/// <summary>Only public internet addresses: a feed link can never make the server call itself or the local network.</summary>
	private static async Task<bool> IsPublic(Uri url)
	{
		if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
		{
			return false;
		}
		IPAddress[] addresses = IPAddress.TryParse(url.DnsSafeHost, out IPAddress ip) ? new[] { ip } : await Dns.GetHostAddressesAsync(url.DnsSafeHost);
		return addresses.Length > 0 && addresses.All(a => !IsPrivate(a));
	}

	private static bool IsPrivate(IPAddress a)
	{
		if (a.IsIPv4MappedToIPv6)
		{
			a = a.MapToIPv4();
		}
		if (IPAddress.IsLoopback(a))
		{
			return true;
		}
		byte[] b = a.GetAddressBytes();
		if (a.AddressFamily == AddressFamily.InterNetwork)
		{
			return b[0] is 0 or 10 or >= 224 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254) || (b[0] == 100 && b[1] is >= 64 and <= 127);
		}
		return a.IsIPv6LinkLocal || a.IsIPv6SiteLocal || a.IsIPv6Multicast || (b[0] & 0xFE) == 0xFC || a.Equals(IPAddress.IPv6Any);
	}

	/// <summary>robots.txt of the feed's site (kept 12 hours): the group naming KieliNewsBot, else «*».</summary>
	private static async Task<bool> RobotsAllow(Uri url)
	{
		string site = url.GetLeftPart(UriPartial.Authority);
		if (!Robots.TryGetValue(site, out var entry) || entry.Until < DateTime.UtcNow)
		{
			List<(bool, string)> rules;
			try
			{
				Response r = await Fetch(new Uri(site + "/robots.txt"), null, null);
				int code = (int)r.Status;
				// no robots.txt (4xx) allows everything; a server error means "not now"
				rules = code is >= 200 and < 300 ? ParseRobots(Encoding.UTF8.GetString(r.Body)) : code is >= 400 and < 500 && code != 429 ? new List<(bool, string)>() : null;
			}
			catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or IOException or SocketException)
			{
				rules = null;
			}
			if (rules == null)
			{
				return false;
			}
			entry = (DateTime.UtcNow.AddHours(12), rules);
			Robots[site] = entry;
		}
		return Allowed(entry.Rules, url.PathAndQuery);
	}

	public static List<(bool Allow, string Path)> ParseRobots(string text)
	{
		List<(List<string> Agents, List<(bool, string)> Rules)> groups = new List<(List<string>, List<(bool, string)>)>();
		List<string> agents = null;
		List<(bool, string)> rules = null;
		bool hadRule = false;
		foreach (string raw in (text ?? string.Empty).Split('\n'))
		{
			string line = raw.Split('#')[0];
			int colon = line.IndexOf(':');
			if (colon < 0)
			{
				continue;
			}
			string field = line.Substring(0, colon).Trim().ToLowerInvariant(), value = line.Substring(colon + 1).Trim();
			if (field == "user-agent")
			{
				if (agents == null || hadRule)
				{
					agents = new List<string>();
					rules = new List<(bool, string)>();
					groups.Add((agents, rules));
					hadRule = false;
				}
				agents.Add(value.Split('/')[0].Trim().ToLowerInvariant());
			}
			else if ((field == "allow" || field == "disallow") && agents != null)
			{
				hadRule = true;
				if (value.Length > 0)
				{
					rules.Add((field == "allow", value));
				}
			}
		}
		List<(List<string> Agents, List<(bool, string)> Rules)> mine = groups.Where(g => g.Agents.Contains(BotToken)).ToList();
		if (mine.Count == 0)
		{
			mine = groups.Where(g => g.Agents.Contains("*")).ToList();
		}
		return mine.SelectMany(g => g.Rules).ToList();
	}

	/// <summary>The longest matching rule decides (Allow wins a tie); «*» is any text, a final «$» the end of the address.</summary>
	public static bool Allowed(List<(bool Allow, string Path)> rules, string path)
	{
		(bool Allow, int Length) best = (true, -1);
		foreach ((bool allow, string pattern) in rules)
		{
			string regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*");
			if (regex.EndsWith("\\$", StringComparison.Ordinal))
			{
				regex = regex.Substring(0, regex.Length - 2) + "$";
			}
			if (Regex.IsMatch(path, regex) && (pattern.Length > best.Length || (pattern.Length == best.Length && allow)))
			{
				best = (allow, pattern.Length);
			}
		}
		return best.Allow;
	}
}
