using System;
using System.Collections.Generic;
using System.Linq;
using MODEL;
using Microsoft.Extensions.Caching.Memory;
using KieliWeb.Caches;

namespace KieliWeb.Setup;

/// <summary>A language of the public site. Kazakh (kz) is the base: every other language is a translation of it.</summary>
public sealed record SiteLanguage(string Culture, string Short, string Name, string Html, string Hreflang, string OgLocale, byte Order);

public static class SiteLanguages
{
	public const string Base = "kz";

	/// <summary>The languages the site knows. Which of them visitors see is the «language» table (FrontendDisplay).</summary>
	public static readonly SiteLanguage[] All =
	{
		new SiteLanguage("kz", "Қаз", "Қазақша", "kk", "kk", "kk_KZ", 1),
		new SiteLanguage("ru", "Рус", "Русский", "ru", "ru", "ru_RU", 2),
		new SiteLanguage("zh-cn", "中文", "简体中文", "zh-CN", "zh-Hans", "zh_CN", 3),
		new SiteLanguage("en", "Eng", "English", "en", "en", "en_US", 4),
		new SiteLanguage("tr", "Tür", "Türkçe", "tr", "tr", "tr_TR", 5)
	};

	public static SiteLanguage Get(string culture) =>
		All.FirstOrDefault(l => l.Culture.Equals(culture ?? string.Empty, StringComparison.OrdinalIgnoreCase)) ?? All[0];

	public static bool IsBase(string culture) => string.IsNullOrEmpty(culture) || culture.Equals(Base, StringComparison.OrdinalIgnoreCase);

	/// <summary>Translation languages (all but Kazakh), for the admin tabs.</summary>
	public static IEnumerable<SiteLanguage> Translations => All.Where(l => !IsBase(l.Culture));

	/// <summary>Languages shown to visitors (switched on in the «language» table), in site order.</summary>
	public static List<SiteLanguage> Enabled(IMemoryCache cache)
	{
		HashSet<string> on = QarCache.GetLanguageList(cache).Where(l => l.FrontendDisplay == 1)
			.Select(l => (l.LanguageCulture ?? string.Empty).ToLowerInvariant()).ToHashSet();
		return All.Where(l => IsBase(l.Culture) || on.Contains(l.Culture)).ToList();
	}

	/// <summary>A site address in another language: /kz/services → /ru/services, / → /ru.</summary>
	public static string Localize(string url, string culture)
	{
		if (string.IsNullOrEmpty(url) || IsBase(culture))
		{
			return url;
		}
		culture = culture.ToLowerInvariant();
		if (url == "/")
		{
			return "/" + culture;
		}
		if (url.StartsWith("/#") || url.StartsWith("/?"))
		{
			return "/" + culture + url.Substring(1);
		}
		if (url.StartsWith("/kz/", StringComparison.OrdinalIgnoreCase))
		{
			return "/" + culture + url.Substring(3);
		}
		return url.Equals("/kz", StringComparison.OrdinalIgnoreCase) ? "/" + culture : url;
	}

	private static readonly System.Text.RegularExpressions.Regex Href = new System.Text.RegularExpressions.Regex(
		"(href=\")(/[^\"]*)(\")", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

	/// <summary>Site links inside rich text follow the page language.</summary>
	public static string LocalizeHtml(string html, string culture)
	{
		if (string.IsNullOrEmpty(html) || IsBase(culture) || html.IndexOf("href=\"/", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return html;
		}
		return Href.Replace(html, m => m.Groups[1].Value + Localize(m.Groups[2].Value, culture) + m.Groups[3].Value);
	}

	/// <summary>Language rows the site needs (bootstrap adds the missing ones).</summary>
	public static Language Row(SiteLanguage l) => new Language
	{
		ShortName = l.Short,
		FullName = l.Name,
		LanguageCulture = l.Culture,
		UniqueSeoCode = l.Html.Substring(0, 2).ToLowerInvariant(),
		ISOCode = l.Html.Substring(0, 2).ToLowerInvariant(),
		LanguageFlagImageUrl = string.Empty,
		DisplayOrder = l.Order,
		IsSubLanguage = 0,
		IsDefault = (byte)(IsBase(l.Culture) ? 1 : 0),
		FrontendDisplay = 1,
		BackendDisplay = (byte)(IsBase(l.Culture) ? 1 : 0),
		QStatus = 0
	};
}
