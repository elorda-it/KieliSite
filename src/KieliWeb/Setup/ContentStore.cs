using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Dapper;
using DBHelper;
using MODEL;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using KieliWeb.Setup.Cms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KieliWeb.Setup;

/// <summary>
/// Everything the public pages read: page blocks and the content tables, cached in memory per language.
/// Kazakh is the base; another language is the Kazakh content with its translations (multilanguage
/// table) put over it, so anything not translated yet shows in Kazakh. Any save in the admin calls
/// <see cref="Clear"/>, so the site shows the change at once.
/// </summary>
public static class ContentStore
{
	private static Dictionary<string, JObject> _defaults = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);

	private static CancellationTokenSource _changes = new CancellationTokenSource();

	private static readonly MethodInfo CloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

	public static void Init(string dbDirectory)
	{
		_defaults = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
		foreach (string name in new[] { "blocks.json", "ui.json" })
		{
			string path = Path.Combine(dbDirectory, "seed", name);
			if (!File.Exists(path))
			{
				continue;
			}
			JObject all = JObject.Parse(File.ReadAllText(path));
			foreach (JProperty p in all.Properties().Where(p => p.Value is JObject))
			{
				_defaults[p.Name] = (JObject)p.Value;
			}
			if (name == "ui.json")
			{
				BlockRegistry.LoadUi(all);
			}
		}
	}

	public static JObject DefaultBlock(string key) => _defaults.TryGetValue(key, out JObject o) ? (JObject)o.DeepClone() : new JObject();

	public static void Clear(IMemoryCache cache)
	{
		CancellationTokenSource old = Interlocked.Exchange(ref _changes, new CancellationTokenSource());
		old.Cancel();
		old.Dispose();
	}

	private static MemoryCacheEntryOptions Options() => new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6) }
		.AddExpirationToken(new CancellationChangeToken(_changes.Token));

	private static T Cached<T>(IMemoryCache cache, string key, Func<IDbConnection, T> load)
	{
		return Memo(cache, key, () =>
		{
			using IDbConnection connection = Utilities.GetOpenConnection();
			return load(connection);
		});
	}

	private static T Memo<T>(IMemoryCache cache, string key, Func<T> load)
	{
		if (cache.TryGetValue("kieli_" + key, out T value))
		{
			return value;
		}
		value = load();
		cache.Set("kieli_" + key, value, Options());
		return value;
	}

	public static JObject Parse(string json)
	{
		try
		{
			return string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);
		}
		catch (JsonException)
		{
			return new JObject();
		}
	}

	public static JArray ParseArray(string json)
	{
		try
		{
			return string.IsNullOrWhiteSpace(json) ? new JArray() : JArray.Parse(json);
		}
		catch (JsonException)
		{
			return new JArray();
		}
	}

	// ---- translations ---------------------------------------------------------------

	/// <summary>All translations of a table into a language: (row id, field) → value.</summary>
	public static Dictionary<(int Id, string Column), string> Translations(IMemoryCache cache, string table, string lang) =>
		Cached(cache, "tr_" + table + "_" + lang, c => c
			.Query<(int Id, string Column, string Value)>("select columnId, columnName, columnValue from multilanguage where qStatus = 0 and tableName = @table and language = @lang order by id",
				new { table, lang })
			.GroupBy(x => (x.Id, x.Column.ToLowerInvariant()))
			.ToDictionary(g => g.Key, g => g.Last().Value));

	private static object Clone(object o) => CloneMethod.Invoke(o, null);

	/// <summary>A copy of each row in another language: translated texts, localized site links.</summary>
	private static List<T> Translate<T>(IMemoryCache cache, string key, Func<List<T>> source, EntityDef def, string lang)
	{
		if (SiteLanguages.IsBase(lang))
		{
			return source();
		}
		return Cached(cache, key + "_" + lang, c =>
		{
			FieldDef[] fields = def.Fields(c);
			Dictionary<(int, string), string> tr = Translations(cache, def.Table, lang);
			return source().Select(o => (T)TranslateRow(o, def, fields, tr, lang)).ToList();
		});
	}

	private static object TranslateRow(object row, EntityDef def, FieldDef[] fields, Dictionary<(int, string), string> tr, string lang)
	{
		object copy = Clone(row);
		int id = (int)def.Model.GetProperty("Id").GetValue(row);
		foreach (FieldDef f in fields)
		{
			PropertyInfo p = def.Model.GetProperty(f.Column ?? f.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
			if (p == null || p.PropertyType != typeof(string))
			{
				continue;
			}
			string value = (string)p.GetValue(copy) ?? string.Empty;
			if (f.Translatable && tr.TryGetValue((id, f.Name.ToLowerInvariant()), out string t) && !string.IsNullOrWhiteSpace(t))
			{
				if (f.Kind == FieldKind.List)
				{
					JObject wrap = new JObject { [f.Name] = ParseArray(value) };
					FieldValues.ApplyTranslation(wrap, new[] { f }, new JObject { [f.Name] = ParseArray(t) });
					value = wrap[f.Name].ToString(Formatting.None);
				}
				else
				{
					value = t;
				}
			}
			if (f.Kind == FieldKind.Url)
			{
				value = SiteLanguages.Localize(value, lang);
			}
			else if (f.Kind == FieldKind.Html)
			{
				value = SiteLanguages.LocalizeHtml(value, lang);
			}
			p.SetValue(copy, value);
		}
		if (def.DataFields != null)
		{
			PropertyInfo dp = def.Model.GetProperty("DataJson");
			JObject data = Parse((string)dp.GetValue(copy));
			if (tr.TryGetValue((id, "data"), out string t))
			{
				FieldValues.ApplyTranslation(data, def.DataFields, Parse(t));
			}
			FieldValues.LocalizeLinks(data, def.DataFields, lang);
			dp.SetValue(copy, data.ToString(Formatting.None));
		}
		return copy;
	}

	// ---- blocks -------------------------------------------------------------------

	public static Dictionary<string, Pageblock> BlockRows(IMemoryCache cache) => Cached(cache, "blocks", c =>
		c.GetList<Pageblock>("where qStatus = 0").GroupBy(b => b.BlockKey).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase));

	/// <summary>The block as shown on the site: saved values over the first (seed) values, then the translation.</summary>
	public static BlockContent Block(IMemoryCache cache, string key, string lang = SiteLanguages.Base)
	{
		lang = (lang ?? SiteLanguages.Base).ToLowerInvariant();
		return Memo(cache, "block_" + key + "_" + lang, () =>
		{
			JObject merged = BaseBlock(cache, key);
			BlockDef def = BlockRegistry.Find(key);
			if (def != null && !SiteLanguages.IsBase(lang))
			{
				FieldValues.ApplyTranslation(merged, def.Fields, BlockTranslation(cache, key, lang));
				FieldValues.LocalizeLinks(merged, def.Fields, lang);
			}
			return new BlockContent(merged);
		});
	}

	/// <summary>The Kazakh values of a block (defaults + what was saved in the admin).</summary>
	public static JObject BaseBlock(IMemoryCache cache, string key)
	{
		JObject merged = DefaultBlock(key);
		if (BlockRows(cache).TryGetValue(key, out Pageblock row))
		{
			merged.Merge(Parse(row.DataJson), new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace, MergeNullValueHandling = MergeNullValueHandling.Ignore });
		}
		return merged;
	}

	/// <summary>The saved translation of a block (only its translated texts), or null.</summary>
	public static JObject BlockTranslation(IMemoryCache cache, string key, string lang)
	{
		if (!BlockRows(cache).TryGetValue(key, out Pageblock row))
		{
			return null;
		}
		return Translations(cache, "pageblock", lang).TryGetValue((row.Id, "datajson"), out string json) ? Parse(json) : null;
	}

	/// <summary>An interface text (ui.* blocks): <c>Ui(cache, "form.nameLabel", "ru")</c>, with {name} placeholders filled.</summary>
	public static string Ui(IMemoryCache cache, string path, string lang, params (string Name, object Value)[] values)
	{
		int dot = path.IndexOf('.');
		string text = Block(cache, "ui." + path.Substring(0, dot), lang)[path.Substring(dot + 1)];
		foreach ((string name, object value) in values)
		{
			text = text.Replace("{" + name + "}", Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
		}
		return text;
	}

	// ---- services -------------------------------------------------------------------

	public sealed class TabWithItems
	{
		public Servicetab Tab { get; set; }

		public List<Serviceitem> Items { get; set; }
	}

	private static List<Servicetab> BaseTabs(IMemoryCache cache) => Cached(cache, "servicetabs", c =>
		c.GetList<Servicetab>("where qStatus = 0 order by displayOrder, id").ToList());

	private static List<Serviceitem> BaseItems(IMemoryCache cache) => Cached(cache, "serviceitems", c =>
		c.GetList<Serviceitem>("where qStatus = 0 order by displayOrder, id").ToList());

	public static List<TabWithItems> ServiceTabs(IMemoryCache cache, string lang = SiteLanguages.Base)
	{
		lang = (lang ?? SiteLanguages.Base).ToLowerInvariant();
		return Memo(cache, "tabswithitems_" + lang, () =>
		{
			List<Servicetab> tabs = Translate(cache, "servicetabs", () => BaseTabs(cache), Entities.ServiceTab, lang);
			List<Serviceitem> items = Translate(cache, "serviceitems", () => BaseItems(cache), Entities.ServiceItem, lang);
			return tabs.Select(t => new TabWithItems { Tab = t, Items = items.Where(i => i.TabId == t.Id).ToList() }).ToList();
		});
	}

	public static List<Servicepage> ServicePages(IMemoryCache cache, string lang = SiteLanguages.Base) =>
		Translate(cache, "servicepages", () => Cached(cache, "servicepages", c => c.GetList<Servicepage>("where qStatus = 0 order by displayOrder, id").ToList()), Entities.ServicePage, lang);

	public static Servicepage ServicePage(IMemoryCache cache, string slug, string lang = SiteLanguages.Base) =>
		ServicePages(cache, lang).FirstOrDefault(p => p.Slug.Equals(slug ?? string.Empty, StringComparison.OrdinalIgnoreCase));

	// ---- news -------------------------------------------------------------------------

	public static List<Articlecategory> ArticleCategories(IMemoryCache cache, string lang = SiteLanguages.Base) =>
		Translate(cache, "articlecats", () => Cached(cache, "articlecats", c => c.GetList<Articlecategory>("where qStatus = 0 order by displayOrder, id").ToList()), Entities.ArticleCategory, lang);

	/// <summary>Published posts: «Маңызды» first, then newest first.</summary>
	public static List<Article> Articles(IMemoryCache cache, string lang = SiteLanguages.Base) =>
		Translate(cache, "articles", () => Cached(cache, "articles", c => c.GetList<Article>("where qStatus = 0 and isPublished = 1 order by isImportant desc, publishTime desc, id desc").ToList()), Entities.Article, lang);

	/// <summary>A post opens its own page; a short news item without text goes to its link.</summary>
	public static string ArticleUrl(Article a, string lang = SiteLanguages.Base)
	{
		if (string.IsNullOrWhiteSpace(a.BodyHtml) && !string.IsNullOrWhiteSpace(a.LinkUrl))
		{
			return a.LinkUrl;
		}
		return SiteLanguages.Localize("/kz/news/" + a.Slug, lang);
	}

	// ---- Kazakhstan -------------------------------------------------------------------

	public static List<Region> Regions(IMemoryCache cache, string lang = SiteLanguages.Base) =>
		Translate(cache, "regions", () => Cached(cache, "regions", c => c.GetList<Region>("where qStatus = 0 order by displayOrder, id").ToList()), Entities.Region, lang);

	public static List<Placecategory> PlaceCategories(IMemoryCache cache, string lang = SiteLanguages.Base) =>
		Translate(cache, "placecats", () => Cached(cache, "placecats", c => c.GetList<Placecategory>("where qStatus = 0 order by displayOrder, id").ToList()), Entities.PlaceCategory, lang);

	public static List<Place> Places(IMemoryCache cache, string lang = SiteLanguages.Base) =>
		Translate(cache, "places", () => Cached(cache, "places", c => c.GetList<Place>("where qStatus = 0 order by displayOrder, id").ToList()), Entities.Place, lang);

	public static List<Heritage> HeritageItems(IMemoryCache cache, string lang = SiteLanguages.Base) =>
		Translate(cache, "heritage", () => Cached(cache, "heritage", c => c.GetList<Heritage>("where qStatus = 0 order by displayOrder, id").ToList()), Entities.Heritage, lang);

	/// <summary>Places of a region: the count typed in the admin, or the places added, whichever is larger.</summary>
	public static int RegionCount(IMemoryCache cache, Region region)
	{
		return Math.Max(region.PlaceCount, Places(cache).Count(p => p.RegionId == region.Id));
	}

	/// <summary>kieli.kz thumbnails come in _big and _middle sizes; other images have one size.</summary>
	public static string ImageSize(string url, string size)
	{
		if (string.IsNullOrEmpty(url))
		{
			return url;
		}
		return Regex.Replace(url, @"_(big|middle)\.jpg$", "_" + size + ".jpg");
	}

	// ---- ҚАЗТЕСТ ------------------------------------------------------------------------

	public sealed class KaztestSet
	{
		public List<Kaztestvariant> Variants { get; set; }

		public List<Kaztestpassage> Passages { get; set; }

		public List<Kaztestaudio> Recordings { get; set; }

		public List<Kaztestquestion> Questions { get; set; }
	}

	/// <summary>The practice tests. Questions and passages stay Kazakh in every language; only the variant names are translated.</summary>
	public static KaztestSet Kaztest(IMemoryCache cache, string lang = SiteLanguages.Base)
	{
		KaztestSet set = Cached(cache, "kaztest", c => new KaztestSet
		{
			Variants = c.GetList<Kaztestvariant>("where qStatus = 0 and isPublished = 1 order by displayOrder, id").ToList(),
			Passages = c.GetList<Kaztestpassage>("where qStatus = 0 order by variantId, passageNo").ToList(),
			Recordings = c.GetList<Kaztestaudio>("where qStatus = 0 order by variantId, audioNo").ToList(),
			Questions = c.GetList<Kaztestquestion>("where qStatus = 0 order by variantId, sectionNo, questionNo").ToList()
		});
		if (SiteLanguages.IsBase(lang))
		{
			return set;
		}
		return new KaztestSet { Variants = Translate(cache, "kaztestvariants", () => set.Variants, Entities.KaztestVariant, lang), Passages = set.Passages, Recordings = set.Recordings, Questions = set.Questions };
	}

	// ---- data for the page scripts (same shapes as the prototype's data files) ----------

	/// <summary>JSON placed inside &lt;script&gt;: &lt; &gt; &amp; and quotes are escaped, so no text can close the tag.</summary>
	public static readonly JsonSerializerSettings ScriptJson = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeHtml };

	private static List<string> Texts(string json, string field = "text") =>
		ParseArray(json).Select(s => s is JObject o ? (string)o[field] : (string)s).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

	public static string ServicesJson(IMemoryCache cache, bool withArticles, string lang = SiteLanguages.Base)
	{
		var tabs = ServiceTabs(cache, lang).Select(t => new
		{
			id = t.Tab.Slug,
			code = t.Tab.Code,
			name = t.Tab.Name,
			icon = t.Tab.Icon,
			cta = t.Tab.Cta,
			visual = t.Tab.Visual,
			steps = Texts(t.Tab.StepsJson),
			footnote = t.Tab.Footnote,
			items = t.Items.Select(i => new
			{
				id = i.Slug,
				code = i.Code,
				name = i.Name,
				icon = i.Icon,
				title = i.Title,
				dir = i.Dir,
				who = i.Who,
				bring = Texts(i.BringJson),
				note = i.Note,
				link = string.IsNullOrWhiteSpace(i.LinkUrl) ? null : new[] { i.LinkUrl, i.LinkText },
				cta = string.IsNullOrWhiteSpace(i.Cta) ? null : i.Cta
			})
		});
		object articleCats = null;
		object articles = null;
		if (withArticles)
		{
			List<Articlecategory> cats = ArticleCategories(cache, lang);
			articleCats = cats.ToDictionary(c => c.Slug, c => c.Name);
			articles = Articles(cache, lang).Select(a => new
			{
				id = a.Slug,
				cat = cats.FirstOrDefault(c => c.Id == a.CategoryId)?.Slug ?? string.Empty,
				important = a.IsImportant == 1,
				title = a.Title,
				excerpt = a.Excerpt,
				date = a.DateText,
				sort = a.PublishTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(a.PublishTime).ToString("yyyy-MM-dd") : "0000",
				read = a.ReadMinutes > 0 ? Ui(cache, "common.readTime", lang, ("n", a.ReadMinutes)) : string.Empty,
				source = string.IsNullOrWhiteSpace(a.SourceName) ? null : a.SourceName,
				href = ArticleUrl(a, lang),
				cover = string.IsNullOrWhiteSpace(a.CoverImageUrl) ? (object)new { tone = a.CoverTone, icon = a.CoverIcon } : new { img = a.CoverImageUrl }
			});
		}
		return JsonConvert.SerializeObject(new { tabs, articleCats, articles }, ScriptJson);
	}

	public static string KieliJson(IMemoryCache cache, string lang = SiteLanguages.Base)
	{
		List<Region> regions = Regions(cache, lang);
		List<Placecategory> cats = PlaceCategories(cache, lang);
		return JsonConvert.SerializeObject(new
		{
			regions = regions.Select(r => new { id = r.MapId, name = r.Name, shortName = string.IsNullOrWhiteSpace(r.ShortName) ? r.Name : r.ShortName, count = RegionCount(cache, r), city = r.IsCity == 1 }),
			cats = cats.ToDictionary(c => c.Slug, c => c.Name),
			catsPlural = cats.ToDictionary(c => c.Slug, c => string.IsNullOrWhiteSpace(c.PluralName) ? c.Name : c.PluralName),
			places = Places(cache, lang).Select(p => new
			{
				id = p.Slug,
				dbId = p.LegacyId,
				region = regions.FirstOrDefault(r => r.Id == p.RegionId)?.MapId ?? 0,
				cat = cats.FirstOrDefault(c => c.Id == p.CategoryId)?.Slug ?? string.Empty,
				img = p.ImageUrl,
				pano = ParseArray(p.PanoJson).Select(x => x is JObject o ? (string)o["url"] : (string)x).Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
				name = p.Name,
				lat = p.Lat,
				lon = p.Lon,
				fact = p.Fact,
				lead = p.Lead,
				facts = ParseArray(p.FactsJson).Select(x => x is JObject o ? new[] { (string)o["label"], (string)o["value"] } : x.ToObject<string[]>()).ToList()
			}),
			heritage = HeritageItems(cache, lang).Select(h => new { name = h.Name, kind = h.Kind, year = h.Year, img = h.ImageUrl })
		}, ScriptJson);
	}

	public static string KaztestJson(IMemoryCache cache, BlockContent settings, string lang = SiteLanguages.Base)
	{
		KaztestSet set = Kaztest(cache, lang);
		// a variant appears on the site once it has questions
		var tests = set.Variants.Where(v => set.Questions.Any(q => q.VariantId == v.Id)).Select(v =>
		{
			List<Kaztestpassage> passages = set.Passages.Where(p => p.VariantId == v.Id).OrderBy(p => p.PassageNo).ToList();
			List<Kaztestaudio> clips = set.Recordings.Where(a => a.VariantId == v.Id).OrderBy(a => a.AudioNo).ToList();
			List<Kaztestquestion> questions = set.Questions.Where(q => q.VariantId == v.Id).ToList();
			return new
			{
				id = v.Id,
				// changes when the questions are created anew, so answers saved in a browser for older questions are not reused
				rev = questions.Max(q => q.AddTime),
				title = v.Title,
				passages = passages.Select(p => p.BodyText.Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList()),
				// the recordings' scripts are shown once the test is over (titles stay in the admin: they would give answers away)
				clips = clips.Select(a => new
				{
					audio = string.IsNullOrWhiteSpace(a.AudioUrl) ? null : a.AudioUrl,
					credit = string.IsNullOrWhiteSpace(a.Credit) ? null : a.Credit,
					creditUrl = string.IsNullOrWhiteSpace(a.CreditUrl) ? null : a.CreditUrl,
					lines = ScriptLines(a.Script)
				}),
				sections = questions.GroupBy(q => new { q.SectionNo, q.SectionName }).OrderBy(g => g.Key.SectionNo).Select(g => new
				{
					name = g.Key.SectionName,
					listen = g.Key.SectionNo == 1,
					questions = g.OrderBy(q => q.QuestionNo).Select(q =>
					{
						int ci = q.SectionNo == 1 ? clips.FindIndex(x => x.AudioNo == q.AudioNo) : -1;
						string audio = ci >= 0 && !string.IsNullOrWhiteSpace(clips[ci].AudioUrl) ? clips[ci].AudioUrl : q.AudioUrl;
						return new
						{
							n = q.QuestionNo,
							q = q.QuestionText,
							sub = string.IsNullOrWhiteSpace(q.SubText) ? null : q.SubText,
							opts = new[] { q.OptionA, q.OptionB, q.OptionC, q.OptionD }.Where(o => !string.IsNullOrWhiteSpace(o)).ToList(),
							a = ci >= 0 ? ci : (int?)null,
							audio = string.IsNullOrWhiteSpace(audio) ? null : audio,
							p = passages.FindIndex(x => x.PassageNo == q.PassageNo) is int pi && pi >= 0 && q.PassageNo > 0 ? pi : (int?)null,
							ans = string.IsNullOrWhiteSpace(q.Answer) ? null : q.Answer.Trim().ToUpperInvariant()
						};
					})
				})
			};
		});
		return JsonConvert.SerializeObject(new
		{
			minutes = settings.Int("minutes", 70),
			pass = settings.Int("passPercent", 70) / 100.0,
			tests
		}, ScriptJson);
	}

	/// <summary>A recording's script as turns: "Ерлан: Сәлем!" → { who = "Ерлан", text = "Сәлем!" }; a line without a name keeps the previous speaker.</summary>
	private static List<object> ScriptLines(string script)
	{
		List<object> lines = new List<object>();
		string who = string.Empty;
		foreach (string raw in (script ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
		{
			string line = raw.Trim();
			if (line.Length == 0)
			{
				continue;
			}
			Match m = Regex.Match(line, @"^([^:]{1,40}):\s+(.+)$");
			if (m.Success)
			{
				who = m.Groups[1].Value.Trim();
				line = m.Groups[2].Value.Trim();
			}
			lines.Add(new { who, text = line });
		}
		return lines;
	}

	/// <summary>Interface texts for the page scripts (kieli.js, kaztest.js): { form: {...}, places: {...}, ... }.</summary>
	public static string UiJson(IMemoryCache cache, string lang, params string[] groups)
	{
		JObject o = new JObject();
		foreach (string g in groups)
		{
			o[g] = Block(cache, "ui." + g, lang).Data;
		}
		return JsonConvert.SerializeObject(o, ScriptJson);
	}
}
