using System;
using System.Data;
using System.Linq;
using Dapper;
using DBHelper;
using MODEL;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using KieliWeb.Attributes;
using KieliWeb.Setup;

namespace KieliWeb.Controllers;

/// <summary>
/// The public site. Every text comes from the page blocks (admin: «Беттер мен мәтіндер») or the
/// content tables (services, news, places, ҚАЗТЕСТ); the markup is the approved prototype.
/// </summary>
[NoRole]
public class HomeController : QarBaseController
{
	public HomeController(IMemoryCache memoryCache, IWebHostEnvironment environment)
		: base(memoryCache, environment)
	{
	}

	private string Lang => string.IsNullOrEmpty(CurrentLanguage) ? SiteLanguages.Base : CurrentLanguage;

	public IActionResult Index() => Page("Index", "home", "home", null, "/", withArticles: true, overlayHeader: true);

	public IActionResult Author() => Page("Author", "author", "author", "author", "/kz/author");

	public IActionResult Services() => Page("Services", "services", "services", "services", "/kz/services");

	public IActionResult Service(string slug)
	{
		Servicepage page = ContentStore.ServicePage(_memoryCache, slug, Lang);
		if (page == null)
		{
			return NotFoundPage();
		}
		return Render("Service", "service", "services", page.Title, page.SeoDescription, ContentStore.Block(_memoryCache, "seo.services", Lang)["image"], "/kz/service/" + page.Slug, page);
	}

	public IActionResult Articles() => Page("Articles", "articles", "news", "articles", "/kz/news", withArticles: true);

	public IActionResult Article(string slug)
	{
		Article article = ContentStore.Articles(_memoryCache, Lang).FirstOrDefault(a => a.Slug.Equals(slug ?? string.Empty, StringComparison.OrdinalIgnoreCase));
		if (article == null)
		{
			return NotFoundPage();
		}
		if (string.IsNullOrWhiteSpace(article.BodyHtml))
		{
			// a news item that only points to its source
			return string.IsNullOrWhiteSpace(article.LinkUrl) ? NotFoundPage() : Redirect(article.LinkUrl);
		}
		CountView(article.Id);
		string description = string.IsNullOrWhiteSpace(article.SeoDescription) ? article.Excerpt : article.SeoDescription;
		string image = string.IsNullOrWhiteSpace(article.CoverImageUrl) ? ContentStore.Block(_memoryCache, "seo.news", Lang)["image"] : Absolute(article.CoverImageUrl);
		return Render("Article", "article", "articles", article.Title, description, image, "/kz/news/" + article.Slug, article);
	}

	public IActionResult Kazakhstan() => Page("Kazakhstan", "kazakhstan", "kazakhstan", "kazakhstan", "/kz/kazakhstan", withKieli: true);

	public IActionResult Place(string slug)
	{
		Place place = ContentStore.Places(_memoryCache, Lang).FirstOrDefault(p => p.Slug.Equals(slug ?? string.Empty, StringComparison.OrdinalIgnoreCase));
		if (place == null)
		{
			return NotFoundPage();
		}
		string description = string.IsNullOrWhiteSpace(place.Lead) ? place.Fact : place.Lead;
		if (description.Length > 200)
		{
			description = description.Substring(0, 197).TrimEnd() + "…";
		}
		return Render("Place", "place", "kazakhstan", place.Name, description, Absolute(ContentStore.ImageSize(place.ImageUrl, "big")), "/kz/place/" + place.Slug, place);
	}

	public IActionResult Kaztest() => Page("Kaztest", "kaztest", "kaztest", "kaztest", "/kz/kaztest");

	/// <summary>Old kieli.kz place address (/kz/attraction/view?id=128): the place with that legacy id, in the same language.</summary>
	public IActionResult OldPlace(string query)
	{
		int id = GetIntQueryParam("id");
		Place place = id > 0 ? ContentStore.Places(_memoryCache).FirstOrDefault(p => p.LegacyId == id) : null;
		return RedirectPermanent(SiteLanguages.Localize(place == null ? "/kz/kazakhstan" : "/kz/place/" + place.Slug, Lang));
	}

	/// <summary>Old kieli.kz traditions: now the heritage section of the Kazakhstan page.</summary>
	public IActionResult OldHeritage(string query) => RedirectPermanent(SiteLanguages.Localize("/kz/kazakhstan", Lang) + "#mura");

	public IActionResult OldAbout(string query) => RedirectPermanent(SiteLanguages.Localize("/kz/author", Lang));

	/// <summary>An address that no page answers (the fallback route in Program.cs).</summary>
	public IActionResult Missing() => NotFoundPage();

	private IActionResult NotFoundPage()
	{
		Response.StatusCode = 404;
		return Render("NotFound", "notfound", null, ContentStore.Ui(_memoryCache, "common.notFoundTitle", Lang), string.Empty, string.Empty, null, null);
	}

	/// <summary>A page whose title and description are its «seo.*» block.</summary>
	private IActionResult Page(string view, string page, string seoKey, string navKey, string path, bool withArticles = false, bool withKieli = false, bool overlayHeader = false)
	{
		BlockContent seo = ContentStore.Block(_memoryCache, "seo." + seoKey, Lang);
		ViewData["withArticles"] = withArticles;
		ViewData["withKieli"] = withKieli;
		ViewData["overlayHeader"] = overlayHeader;
		return Render(view, page, navKey, seo["title"], seo["description"], seo["image"], path, null, fullTitle: true);
	}

	private IActionResult Render(string view, string page, string navKey, string title, string description, string image, string path, object model, bool fullTitle = false)
	{
		string brand = ContentStore.Block(_memoryCache, "site.brand", Lang)["name"];
		ViewData["page"] = page;
		ViewData["navKey"] = navKey ?? string.Empty;
		ViewData["seoTitle"] = string.IsNullOrWhiteSpace(title) ? brand : fullTitle ? title : title + " · " + brand;
		ViewData["seoDescription"] = description ?? string.Empty;
		ViewData["seoImage"] = Absolute(image);
		// the page's address in its Kazakh form; the layout builds canonical and hreflang links from it
		ViewData["pagePath"] = path ?? string.Empty;
		// what the page is, for search engines and AI assistants (schema.org JSON-LD)
		ViewData["jsonLd"] = StructuredData.Build(_memoryCache, Lang, COMMON.QarSingleton.GetInstance().GetSiteUrl(), page, path, (string)ViewData["seoTitle"], description, (string)ViewData["seoImage"], model);
		ViewData["noindex"] = page == "notfound";
		if (model is Article article && article.PublishTime > 0)
		{
			ViewData["ogType"] = "article";
			ViewData["publishedTime"] = DateTimeOffset.FromUnixTimeSeconds(article.PublishTime).ToOffset(TimeSpan.FromHours(5)).ToString("yyyy-MM-dd'T'HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
		}
		return View($"~/Views/Themes/{CurrentTheme}/Home/{view}.cshtml", model);
	}

	private static string Absolute(string url)
	{
		if (string.IsNullOrWhiteSpace(url) || url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
		{
			return url ?? string.Empty;
		}
		return COMMON.QarSingleton.GetInstance().GetSiteUrl().TrimEnd('/') + url;
	}

	private static void CountView(int articleId)
	{
		try
		{
			using IDbConnection connection = Utilities.GetOpenConnection();
			connection.Execute("update article set viewCount = viewCount + 1 where id = @articleId", new { articleId });
		}
		catch (Exception)
		{
			// a view counter must never break the page
		}
	}
}
