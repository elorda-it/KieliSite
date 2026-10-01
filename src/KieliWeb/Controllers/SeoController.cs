using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using COMMON;
using MODEL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using KieliWeb.Setup;

namespace KieliWeb.Controllers;

/// <summary>
/// robots.txt, sitemap.xml and llms.txt / llms-full.txt (a plain summary of the site for AI assistants), all built
/// from the content in the database.
/// </summary>
[ApiController]
public class SeoController : ControllerBase
{
	private readonly IMemoryCache _memoryCache;

	public SeoController(IMemoryCache memoryCache)
	{
		_memoryCache = memoryCache;
	}

	private static string SiteUrl => QarSingleton.GetInstance().GetSiteUrl().TrimEnd('/');

	/// <summary>The public pages (Kazakh form of the address) and the blocks whose last change dates each one.</summary>
	private static readonly (string Path, string Blocks)[] StaticPages =
	{
		("/", "home."), ("/kz/author", "author."), ("/kz/services", "services."), ("/kz/news", "news."), ("/kz/kazakhstan", "kazakhstan."), ("/kz/kaztest", "kaztest.")
	};

	[HttpGet("/robots.txt")]
	public ContentResult Robots()
	{
		// the admin screens of every language (/kz/content/…, /ru/admin/…): they only redirect to the sign-in page
		IEnumerable<string> admin = AdminMenu.Groups.SelectMany(g => g.Items).Select(i => i.Url.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
			.Append("admin").Where(c => !string.IsNullOrEmpty(c)).Distinct();
		StringBuilder text = new StringBuilder();
		text.Append("# kieli.kz — search engines and AI assistants are welcome to read every public page.\n");
		text.Append("User-agent: *\nAllow: /\n");
		foreach (string c in admin)
		{
			text.Append("Disallow: /*/").Append(c).Append("/\n");
		}
		text.Append("Disallow: /api/\nDisallow: /hangfire\n\n");
		text.Append("Sitemap: ").Append(SiteUrl).Append("/sitemap.xml\n");
		text.Append("# A plain summary for AI assistants: ").Append(SiteUrl).Append("/llms.txt\n");
		return Content(text.ToString(), "text/plain", Encoding.UTF8);
	}

	[HttpGet("/sitemap.xml")]
	public ContentResult Sitemap()
	{
		Dictionary<string, Pageblock> blocks = ContentStore.BlockRows(_memoryCache);
		int site = blocks.Values.Where(b => b.BlockKey.StartsWith("site.") || b.BlockKey.StartsWith("seo.")).Select(b => b.UpdateTime).DefaultIfEmpty(0).Max();
		List<(string Path, int Time)> urls = StaticPages
			.Select(p => (p.Path, Math.Max(site, blocks.Values.Where(b => b.BlockKey.StartsWith(p.Blocks)).Select(b => b.UpdateTime).DefaultIfEmpty(0).Max())))
			.ToList();
		urls.AddRange(ContentStore.ServicePages(_memoryCache).Select(p => ("/kz/service/" + p.Slug, p.UpdateTime)));
		urls.AddRange(ContentStore.Articles(_memoryCache).Where(a => !string.IsNullOrWhiteSpace(a.BodyHtml)).Select(a => ("/kz/news/" + a.Slug, Math.Max(a.UpdateTime, a.PublishTime))));
		urls.AddRange(ContentStore.Places(_memoryCache).Select(p => ("/kz/place/" + p.Slug, p.UpdateTime)));
		// every page in every language the visitors see, each listing its other languages (hreflang)
		List<SiteLanguage> languages = SiteLanguages.Enabled(_memoryCache);
		StringBuilder xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">\n");
		foreach ((string path, int time) in urls)
		{
			foreach (SiteLanguage lang in languages)
			{
				xml.Append("  <url><loc>").Append(SecurityElement.Escape(SiteUrl + SiteLanguages.Localize(path, lang.Culture))).Append("</loc>");
				if (time > 0)
				{
					xml.Append("<lastmod>").Append(UnixTimeHelper.UnixTimeToDateTime(time).ToString("yyyy-MM-dd")).Append("</lastmod>");
				}
				if (languages.Count > 1)
				{
					foreach (SiteLanguage alt in languages)
					{
						xml.Append("<xhtml:link rel=\"alternate\" hreflang=\"").Append(alt.Hreflang).Append("\" href=\"")
							.Append(SecurityElement.Escape(SiteUrl + SiteLanguages.Localize(path, alt.Culture))).Append("\"/>");
					}
					xml.Append("<xhtml:link rel=\"alternate\" hreflang=\"x-default\" href=\"").Append(SecurityElement.Escape(SiteUrl + path)).Append("\"/>");
				}
				xml.Append("</url>\n");
			}
		}
		xml.Append("</urlset>\n");
		return Content(xml.ToString(), "application/xml", Encoding.UTF8);
	}

	/// <summary>llms.txt (llmstxt.org): what kieli.kz is and where things are, in English with links to the English pages.</summary>
	[HttpGet("/llms.txt")]
	public ContentResult Llms() => Content(LlmsText(full: false), "text/plain", Encoding.UTF8);

	/// <summary>The same with the descriptions of every service, the questions and answers, articles and places.</summary>
	[HttpGet("/llms-full.txt")]
	public ContentResult LlmsFull() => Content(LlmsText(full: true), "text/plain", Encoding.UTF8);

	private string LlmsText(bool full)
	{
		const string en = "en";
		string Url(string path, string lang = en) => SiteUrl + SiteLanguages.Localize(path, lang);
		string Title(string seoKey) => Clean(ContentStore.Block(_memoryCache, "seo." + seoKey, en)["title"]).Replace(" · kieli.kz", string.Empty);
		string Seo(string seoKey) => Clean(ContentStore.Block(_memoryCache, "seo." + seoKey, en)["description"]);
		BlockContent contact = ContentStore.Block(_memoryCache, "site.contact", en);
		StringBuilder t = new StringBuilder();

		t.Append("# ").Append(ContentStore.Block(_memoryCache, "site.brand", en)["name"]).Append("\n\n");
		t.Append("> ").Append(Seo("home")).Append("\n\n");
		t.Append("kieli.kz is the website of BASTAU LINE LLP («BASTAU LINE» ЖШС), a translation, document and migration consultancy in Astana, Kazakhstan, ")
			.Append("founded by Omar Bekmurat (Омар Бекмұрат). It helps people — above all ethnic Kazakhs (kandas) from China — move to Kazakhstan, and works in Kazakh, Chinese and Russian. ")
			.Append("The site also runs a free KAZTEST practice test and «Киелі» (Kieli), a guide to the sacred places of Kazakhstan.\n\n");
		foreach (SiteLanguage l in SiteLanguages.All.Where(l => l.Culture != en))
		{
			t.Append("- ").Append(l.Name).Append(": ").Append(Clean(ContentStore.Block(_memoryCache, "seo.home", l.Culture)["description"])).Append(' ').Append(Url("/", l.Culture)).Append('\n');
		}

		t.Append("\n## Contact\n\n");
		t.Append("- Company: ").Append(Clean(contact["legalName"])).Append('\n');
		t.Append("- Office: ").Append(Clean(contact["address"])).Append(", Kazakhstan\n");
		foreach ((string label, string field) in new[] { ("Phone", "phone"), ("WhatsApp", "whatsapp"), ("Telegram", "telegram"), ("WeChat", "wechat"), ("Email", "email"), ("Hours (Astana time, UTC+5)", "hours") })
		{
			if (contact.Has(field))
			{
				t.Append("- ").Append(label).Append(": ").Append(Clean(contact[field])).Append('\n');
			}
		}
		t.Append("- Requests: the «").Append(Clean(ContentStore.Block(_memoryCache, "site.consult", en)["title"])).Append("» form on every page of the site\n");

		t.Append("\n## Main pages\n\n");
		foreach ((string path, string seoKey) in new[] { ("/kz/services", "services"), ("/kz/kaztest", "kaztest"), ("/kz/news", "news"), ("/kz/kazakhstan", "kazakhstan"), ("/kz/author", "author") })
		{
			t.Append("- [").Append(Title(seoKey)).Append("](").Append(Url(path)).Append("): ").Append(Seo(seoKey)).Append('\n');
		}

		t.Append("\n## Services\n\n");
		foreach (ContentStore.TabWithItems tab in ContentStore.ServiceTabs(_memoryCache, en))
		{
			t.Append("### ").Append(Clean(tab.Tab.Name)).Append("\n\n");
			foreach (Serviceitem item in tab.Items)
			{
				t.Append("- [").Append(Clean(item.Title)).Append("](").Append(Url("/kz/services")).Append("#s-").Append(tab.Tab.Slug).Append('-').Append(item.Slug).Append(")");
				if (full && !string.IsNullOrWhiteSpace(item.Dir))
				{
					t.Append(": ").Append(Clean(item.Dir));
					if (!string.IsNullOrWhiteSpace(item.Who))
					{
						t.Append(" For: ").Append(Clean(item.Who));
					}
				}
				t.Append('\n');
			}
			t.Append('\n');
		}
		List<Servicepage> pages = ContentStore.ServicePages(_memoryCache, en);
		if (pages.Count > 0)
		{
			t.Append("Detailed guides:\n\n");
			foreach (Servicepage p in pages)
			{
				t.Append("- [").Append(Clean(p.Title)).Append("](").Append(Url("/kz/service/" + p.Slug)).Append("): ").Append(Clean(p.SeoDescription)).Append('\n');
			}
		}

		ContentStore.KaztestSet test = ContentStore.Kaztest(_memoryCache, en);
		BlockContent settings = ContentStore.Block(_memoryCache, "kaztest.list", en);
		List<Kaztestvariant> variants = test.Variants.Where(v => test.Questions.Any(q => q.VariantId == v.Id)).ToList();
		t.Append("\n## KAZTEST practice test\n\n");
		t.Append("- [").Append(Title("kaztest")).Append("](").Append(Url("/kz/kaztest")).Append("): ").Append(Seo("kaztest")).Append('\n');
		if (variants.Count > 0)
		{
			// the sections of one variant: listening questions have a recording
			string sections = string.Join(" and ", test.Questions.Where(q => q.VariantId == variants[0].Id).GroupBy(q => q.SectionNo).OrderBy(g => g.Key)
				.Select(g => g.Any(q => q.AudioNo > 0) ? "listening " + g.Count() + " questions (with audio)" : "reading " + g.Count() + " questions"));
			t.Append("- ").Append(variants.Count).Append(" practice variants written by BASTAU LINE in the official format (not National Testing Center material): ")
				.Append(sections).Append(", ").Append(settings.Int("minutes", 70)).Append(" minutes, ")
				.Append("at least ").Append(settings.Int("passPercent", 70)).Append("% in each section to pass. Free, no registration; answers and the recording scripts are shown after the test.\n");
		}
		string law = Clean(ContentStore.Block(_memoryCache, "kaztest.law", en)["text"]);
		if (law.Length > 0)
		{
			t.Append("- ").Append(law).Append('\n');
		}

		List<Article> articles = ContentStore.Articles(_memoryCache, en).Where(a => !string.IsNullOrWhiteSpace(a.BodyHtml)).Take(full ? 100 : 20).ToList();
		if (articles.Count > 0)
		{
			t.Append("\n## Articles and news\n\n");
			foreach (Article a in articles)
			{
				t.Append("- [").Append(Clean(a.Title)).Append("](").Append(SiteUrl).Append(ContentStore.ArticleUrl(a, en)).Append(")");
				if (full && !string.IsNullOrWhiteSpace(a.Excerpt))
				{
					t.Append(": ").Append(Clean(a.Excerpt));
				}
				t.Append('\n');
			}
		}

		List<Place> places = ContentStore.Places(_memoryCache, en);
		t.Append("\n## Kieli: sacred places of Kazakhstan\n\n");
		t.Append("- [").Append(Title("kazakhstan")).Append("](").Append(Url("/kz/kazakhstan")).Append("): ").Append(Seo("kazakhstan")).Append(' ').Append(places.Count).Append(" places so far.\n");
		if (full)
		{
			foreach (Place p in places)
			{
				t.Append("- [").Append(Clean(p.Name)).Append("](").Append(Url("/kz/place/" + p.Slug)).Append("): ").Append(Clean(string.IsNullOrWhiteSpace(p.Lead) ? p.Fact : p.Lead)).Append('\n');
			}
		}

		if (full)
		{
			t.Append("\n## Questions and answers\n\n");
			foreach (string key in new[] { "home.faq", "services.faq", "kaztest.faq" })
			{
				foreach (BlockContent qa in ContentStore.Block(_memoryCache, key, en).List("items").Where(i => i.Has("q") && i.Has("a")))
				{
					t.Append("**").Append(Clean(qa["q"])).Append("**\n").Append(Clean(qa["a"])).Append("\n\n");
				}
			}
		}

		t.Append("\n## Languages\n\n");
		foreach (SiteLanguage l in SiteLanguages.All)
		{
			t.Append("- ").Append(l.Name).Append(": ").Append(Url("/", l.Culture)).Append('\n');
		}
		t.Append("\nThe Kazakh pages are the original; the other languages are translations. ").Append(full ? string.Empty : "More detail: " + SiteUrl + "/llms-full.txt\n");
		return t.ToString();
	}

	private static string Clean(string text) => StructuredData.Plain(text).Replace("\n", " ");
}
