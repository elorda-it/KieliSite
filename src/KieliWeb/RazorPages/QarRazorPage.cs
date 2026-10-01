using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using COMMON;
using MODEL;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using KieliWeb.Caches;
using KieliWeb.Setup;

namespace KieliWeb.RazorPages;

public abstract class QarRazorPage<TModel> : RazorPage<TModel>
{
	private IWebHostEnvironment _environment;

	protected string CurrentLanguage => QarString("language");

	protected string CurrentTheme => QarSingleton.GetInstance().GetSiteTheme();

	protected string SiteUrl => QarSingleton.GetInstance().GetSiteUrl();

	protected string ControllerName => QarString("controllerName");

	protected string ActionName => QarString("actionName");

	protected string SkinName => QarString("skinName");

	protected string Title => QarString("title");

	protected string Query => QarString("query");

	protected List<Language> LanguageList => QarList<Language>("languageList");

	protected List<Multilanguage> MultiLanguageList => (base.ViewData["multiLanguageList"] ?? new List<Multilanguage>()) as List<Multilanguage>;

	public Sitesetting SiteSetting => QarModel<Sitesetting>("siteSetting");

	protected bool CanView => Convert.ToBoolean(base.ViewData["canView"] ?? ((object)false));

	protected bool CanCreate => Convert.ToBoolean(base.ViewData["canCreate"] ?? ((object)false));

	protected bool CanEdit => Convert.ToBoolean(base.ViewData["canEdit"] ?? ((object)false));

	protected bool CanDelete => Convert.ToBoolean(base.ViewData["canDelete"] ?? ((object)false));

	protected bool IsMobile => Convert.ToBoolean(base.ViewData["isMobile"] ?? ((object)false));

	/// <summary>
	/// Canonical homepage URL for a culture: "/" for the default language,
	/// "/{culture}" for the others. The middleware folds "/home/index" and the
	/// default culture out of the address bar, so links should use this instead of
	/// spelling those defaults out — otherwise every click costs a 301.
	/// </summary>
	public string HomeUrl(string culture = null)
	{
		culture = (string.IsNullOrWhiteSpace(culture) ? CurrentLanguage : culture).Trim().ToLower();
		string defaultCulture = LanguageList.FirstOrDefault((Language x) => x.IsDefault == 1)?.LanguageCulture ?? "kz";
		if (culture.Equals(defaultCulture, StringComparison.OrdinalIgnoreCase))
		{
			return "/";
		}
		return "/" + culture;
	}

	protected string T(string localKey)
	{
		if (string.IsNullOrWhiteSpace(localKey))
		{
			return localKey;
		}
		IMemoryCache memoryCache = ViewContext.HttpContext.RequestServices.GetService<IMemoryCache>();
		return QarCache.GetLanguageValue(memoryCache, localKey, CurrentLanguage);
	}

	protected List<T> QarList<T>(string vdName) where T : new()
	{
		if (base.ViewData[vdName] is List<T> value)
		{
			return value;
		}
		return new List<T>();
	}

	protected T QarModel<T>(string vdName)
	{
		object obj = base.ViewData[vdName];
		if (obj is T)
		{
			return (T)obj;
		}
		return default(T);
	}

	protected string QarString(string vdName)
	{
		if (base.ViewData[vdName] is string value)
		{
			return value;
		}
		return string.Empty;
	}

	protected int QarInt(string vdName)
	{
		object obj = base.ViewData[vdName];
		if (obj is int)
		{
			return (int)obj;
		}
		return 0;
	}

	protected bool QarBool(string vdName)
	{
		object obj = base.ViewData[vdName];
		if (obj is bool)
		{
			return (bool)obj;
		}
		return false;
	}

	// ---- public theme (Views/Themes/Kieli) ---------------------------------------------

	protected IMemoryCache Cache => ViewContext.HttpContext.RequestServices.GetService<IMemoryCache>();

	/// <summary>The page language (kz, ru, zh-cn, en, tr); Kazakh is the base of every translation.</summary>
	protected string Lang => string.IsNullOrEmpty(CurrentLanguage) ? SiteLanguages.Base : CurrentLanguage;

	protected SiteLanguage LangInfo => SiteLanguages.Get(Lang);

	protected bool IsKazakh => SiteLanguages.IsBase(Lang);

	/// <summary>An editable page block (admin: «Беттер мен мәтіндер») in the page language.</summary>
	protected BlockContent Block(string key) => ContentStore.Block(Cache, key, Lang);

	/// <summary>An interface text in the page language: <c>U("form.nameLabel")</c>, <c>U("places.count", ("n", 5))</c>.</summary>
	protected string U(string path, params (string Name, object Value)[] values) => ContentStore.Ui(Cache, path, Lang, values);

	/// <summary>An interface text with one placeholder filled by ready HTML (the text itself is encoded).</summary>
	protected IHtmlContent UHtml(string path, string name, string html) =>
		new HtmlString(WebUtility.HtmlEncode(ContentStore.Ui(Cache, path, Lang)).Replace("{" + name + "}", html));

	/// <summary>A site address in the page language: /kz/services → /ru/services.</summary>
	protected string L(string url) => SiteLanguages.Localize(url, Lang);

	/// <summary>A file under wwwroot with a content hash, so browsers pick up changes despite the year-long cache.</summary>
	protected string Asset(string path)
	{
		IFileVersionProvider provider = ViewContext.HttpContext.RequestServices.GetService<IFileVersionProvider>();
		return provider == null ? path : provider.AddFileVersionToPath(ViewContext.HttpContext.Request.PathBase, path);
	}

	/// <summary>An image address from a block: the site's own files get the content hash (see <see cref="Asset"/>), uploads and links stay as they are.</summary>
	protected string Media(string url) => !string.IsNullOrWhiteSpace(url) && url.StartsWith("/kieli/", StringComparison.Ordinal) ? Asset(url) : url ?? string.Empty;

	/// <summary>The site name with its domain ending set apart: «kieli.kz» → kieli&lt;span class="brand__tld"&gt;.kz&lt;/span&gt;.</summary>
	protected IHtmlContent BrandName(string name)
	{
		name ??= string.Empty;
		int dot = name.LastIndexOf('.');
		if (dot <= 0 || dot == name.Length - 1)
		{
			return new HtmlString(WebUtility.HtmlEncode(name));
		}
		return new HtmlString(WebUtility.HtmlEncode(name.Substring(0, dot)) + "<span class=\"brand__tld\">" + WebUtility.HtmlEncode(name.Substring(dot)) + "</span>");
	}

	/// <summary>A symbol of the site sprite: &lt;svg&gt;&lt;use href="#i-doc"/&gt;&lt;/svg&gt;.</summary>
	protected IHtmlContent Icon(string id, string cssClass = null)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			return HtmlString.Empty;
		}
		string cls = string.IsNullOrEmpty(cssClass) ? string.Empty : " class=\"" + WebUtility.HtmlEncode(cssClass) + "\"";
		return new HtmlString("<svg" + cls + " aria-hidden=\"true\"><use href=\"#" + WebUtility.HtmlEncode(id.Trim()) + "\"/></svg>");
	}

	protected static string Digits(string value) => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

	protected static string ImageSize(string url, string size) => ContentStore.ImageSize(url, size);

	protected string GetUrl(string url)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			return string.Empty;
		}
		if (_environment == null)
		{
			_environment = ViewContext.HttpContext.RequestServices.GetService<IWebHostEnvironment>();
		}
		if (_environment == null || _environment.IsDevelopment())
		{
			if (!url.StartsWith("http"))
			{
				return SiteUrl + url;
			}
			return url;
		}
		return url;
	}
}
