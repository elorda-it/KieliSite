using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using MODEL;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KieliWeb.Setup;

/// <summary>
/// schema.org data (JSON-LD) for search engines and AI assistants: who runs the site (BASTAU LINE and its founder),
/// and what each page is — services, articles, places, the ҚАЗТЕСТ practice test, questions and answers. Built from
/// the same blocks and tables as the pages, in the page language, so editing the admin keeps it right.
/// </summary>
public static class StructuredData
{
	private static readonly string[] DayNames = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

	private static readonly Dictionary<string, int> KazakhDays = new Dictionary<string, int>
	{
		["Дс"] = 0, ["Сс"] = 1, ["Ср"] = 2, ["Бс"] = 3, ["Жм"] = 4, ["Сб"] = 5, ["Жс"] = 6
	};

	/// <summary>The JSON-LD of one page (a single @graph), or an empty string for pages that are not indexed.</summary>
	public static string Build(IMemoryCache cache, string lang, string siteUrl, string page, string path, string title, string description, string image, object model)
	{
		if (string.IsNullOrEmpty(page) || page == "notfound")
		{
			return string.Empty;
		}
		siteUrl = siteUrl.TrimEnd('/');
		SiteLanguage language = SiteLanguages.Get(lang);
		string Url(string p) => siteUrl + SiteLanguages.Localize(p, lang);
		string pageUrl = Url(string.IsNullOrEmpty(path) ? "/" : path);
		string org = siteUrl + "/#organization", site = siteUrl + "/#website", founder = siteUrl + "/#founder";

		JArray graph = new JArray { Organization(cache, lang, siteUrl, Url, org, founder), WebSite(cache, lang, siteUrl, org, site), Founder(cache, lang, siteUrl, Url, org, founder) };

		JObject webPage = new JObject
		{
			["@type"] = "WebPage",
			["@id"] = pageUrl + "#webpage",
			["url"] = pageUrl,
			["name"] = title,
			["description"] = Plain(description),
			["inLanguage"] = language.Html,
			["isPartOf"] = Ref(site),
			["publisher"] = Ref(org)
		};
		if (!string.IsNullOrWhiteSpace(image))
		{
			webPage["primaryImageOfPage"] = new JObject { ["@type"] = "ImageObject", ["url"] = image };
		}
		graph.Add(webPage);

		List<(string Name, string Url)> crumbs = new List<(string, string)> { (ContentStore.Ui(cache, "common.home", lang), Url("/")) };
		string NavLabel(string navPath) => ContentStore.Block(cache, "site.nav", lang).List("items")
			.FirstOrDefault(i => SiteLanguages.Localize(i["url"], lang) == SiteLanguages.Localize(navPath, lang))?["label"] ?? title;

		switch (page)
		{
		case "home":
			webPage["@type"] = new JArray("WebPage", "FAQPage");
			webPage["about"] = Ref(org);
			webPage["mainEntity"] = Questions(ContentStore.Block(cache, "home.faq", lang));
			break;
		case "author":
			webPage["@type"] = "ProfilePage";
			webPage["mainEntity"] = Ref(founder);
			crumbs.Add((title, pageUrl));
			break;
		case "services":
		{
			webPage["@type"] = new JArray("CollectionPage", "FAQPage");
			webPage["mainEntity"] = Questions(ContentStore.Block(cache, "services.faq", lang));
			JArray services = new JArray();
			int position = 1;
			foreach (ContentStore.TabWithItems tab in ContentStore.ServiceTabs(cache, lang))
			{
				foreach (Serviceitem item in tab.Items)
				{
					services.Add(new JObject
					{
						["@type"] = "ListItem",
						["position"] = position++,
						["item"] = new JObject
						{
							["@type"] = "Service",
							["name"] = Plain(item.Title),
							["description"] = Plain(item.Dir),
							["serviceType"] = Plain(tab.Tab.Name),
							["url"] = pageUrl + "#s-" + tab.Tab.Slug + "-" + item.Slug,
							["provider"] = Ref(org),
							["areaServed"] = new JObject { ["@type"] = "Country", ["name"] = "Kazakhstan" }
						}
					});
				}
			}
			graph.Add(new JObject { ["@type"] = "ItemList", ["@id"] = pageUrl + "#services", ["name"] = title, ["itemListElement"] = services });
			webPage["hasPart"] = Ref(pageUrl + "#services");
			crumbs.Add((NavLabel("/kz/services"), pageUrl));
			break;
		}
		case "service" when model is Servicepage servicePage:
		{
			JObject data = ContentStore.Parse(servicePage.DataJson);
			JArray questions = Questions(new BlockContent(data));
			if (questions.Count > 0)
			{
				webPage["@type"] = new JArray("WebPage", "FAQPage");
				webPage["mainEntity"] = questions;
			}
			graph.Add(new JObject
			{
				["@type"] = "Service",
				["@id"] = pageUrl + "#service",
				["name"] = Plain(servicePage.Title),
				["description"] = Plain(servicePage.SeoDescription),
				["url"] = pageUrl,
				["provider"] = Ref(org),
				["areaServed"] = new JObject { ["@type"] = "Country", ["name"] = "Kazakhstan" },
				["availableLanguage"] = new JArray("kk", "zh", "ru")
			});
			webPage["about"] = Ref(pageUrl + "#service");
			crumbs.Add((NavLabel("/kz/services"), Url("/kz/services")));
			crumbs.Add((Plain(servicePage.Title), pageUrl));
			break;
		}
		case "articles":
		{
			webPage["@type"] = "CollectionPage";
			JArray items = new JArray();
			int position = 1;
			foreach (Article a in ContentStore.Articles(cache, lang).Where(a => !string.IsNullOrWhiteSpace(a.BodyHtml)).Take(50))
			{
				items.Add(new JObject { ["@type"] = "ListItem", ["position"] = position++, ["url"] = siteUrl + ContentStore.ArticleUrl(a, lang), ["name"] = Plain(a.Title) });
			}
			graph.Add(new JObject { ["@type"] = "ItemList", ["@id"] = pageUrl + "#articles", ["name"] = title, ["itemListElement"] = items });
			crumbs.Add((NavLabel("/kz/news"), pageUrl));
			break;
		}
		case "article" when model is Article article:
		{
			JObject node = new JObject
			{
				["@type"] = "Article",
				["@id"] = pageUrl + "#article",
				["headline"] = Plain(article.Title),
				["description"] = Plain(description),
				["inLanguage"] = language.Html,
				["mainEntityOfPage"] = Ref(pageUrl + "#webpage"),
				["publisher"] = Ref(org),
				["author"] = string.IsNullOrWhiteSpace(article.AuthorName) ? Ref(org)
					: Plain(article.AuthorName) == FounderName(cache, lang) ? Ref(founder)
					: new JObject { ["@type"] = "Person", ["name"] = Plain(article.AuthorName) }
			};
			if (article.PublishTime > 0)
			{
				node["datePublished"] = IsoTime(article.PublishTime);
			}
			if (article.UpdateTime > 0)
			{
				node["dateModified"] = IsoTime(Math.Max(article.UpdateTime, article.PublishTime));
			}
			if (!string.IsNullOrWhiteSpace(image))
			{
				node["image"] = image;
			}
			if (!string.IsNullOrWhiteSpace(article.SourceUrl))
			{
				node["isBasedOn"] = article.SourceUrl;
			}
			graph.Add(node);
			crumbs.Add((NavLabel("/kz/news"), Url("/kz/news")));
			crumbs.Add((Plain(article.Title), pageUrl));
			break;
		}
		case "kazakhstan":
		{
			webPage["@type"] = "CollectionPage";
			JArray items = new JArray();
			int position = 1;
			foreach (Place p in ContentStore.Places(cache, lang))
			{
				items.Add(new JObject { ["@type"] = "ListItem", ["position"] = position++, ["url"] = Url("/kz/place/" + p.Slug), ["name"] = Plain(p.Name) });
			}
			graph.Add(new JObject { ["@type"] = "ItemList", ["@id"] = pageUrl + "#places", ["name"] = title, ["itemListElement"] = items });
			crumbs.Add((NavLabel("/kz/kazakhstan"), pageUrl));
			break;
		}
		case "place" when model is Place place:
		{
			JObject node = new JObject
			{
				["@type"] = "TouristAttraction",
				["@id"] = pageUrl + "#place",
				["name"] = Plain(place.Name),
				["description"] = Plain(description),
				["url"] = pageUrl
			};
			if (!string.IsNullOrWhiteSpace(image))
			{
				node["image"] = image;
			}
			if (place.Lat != 0 && place.Lon != 0)
			{
				node["geo"] = new JObject { ["@type"] = "GeoCoordinates", ["latitude"] = place.Lat, ["longitude"] = place.Lon };
			}
			Region region = ContentStore.Regions(cache, lang).FirstOrDefault(r => r.Id == place.RegionId);
			node["containedInPlace"] = region == null
				? new JObject { ["@type"] = "Country", ["name"] = "Kazakhstan" }
				: new JObject { ["@type"] = "AdministrativeArea", ["name"] = Plain(region.Name), ["containedInPlace"] = new JObject { ["@type"] = "Country", ["name"] = "Kazakhstan" } };
			graph.Add(node);
			webPage["about"] = Ref(pageUrl + "#place");
			crumbs.Add((NavLabel("/kz/kazakhstan"), Url("/kz/kazakhstan")));
			crumbs.Add((Plain(place.Name), pageUrl));
			break;
		}
		case "kaztest":
		{
			webPage["@type"] = new JArray("WebPage", "FAQPage");
			webPage["mainEntity"] = Questions(ContentStore.Block(cache, "kaztest.faq", lang));
			BlockContent settings = ContentStore.Block(cache, "kaztest.list", lang);
			graph.Add(new JObject
			{
				["@type"] = "LearningResource",
				["@id"] = pageUrl + "#test",
				["name"] = title,
				["description"] = Plain(description),
				["url"] = pageUrl,
				["learningResourceType"] = "Practice test",
				["educationalLevel"] = "A1–B1",
				["inLanguage"] = "kk",
				["isAccessibleForFree"] = true,
				["timeRequired"] = "PT" + settings.Int("minutes", 70) + "M",
				["about"] = new JObject { ["@type"] = "Thing", ["name"] = "ҚАЗТЕСТ (KAZTEST) — Kazakh language proficiency test" },
				["provider"] = Ref(org)
			});
			webPage["about"] = Ref(pageUrl + "#test");
			crumbs.Add((NavLabel("/kz/kaztest"), pageUrl));
			break;
		}
		}

		if (crumbs.Count > 1)
		{
			graph.Add(new JObject
			{
				["@type"] = "BreadcrumbList",
				["@id"] = pageUrl + "#breadcrumb",
				["itemListElement"] = new JArray(crumbs.Select((c, i) => new JObject { ["@type"] = "ListItem", ["position"] = i + 1, ["name"] = Plain(c.Name), ["item"] = c.Url }))
			});
			webPage["breadcrumb"] = Ref(pageUrl + "#breadcrumb");
		}
		// a page without questions is not a FAQ page
		if (webPage["@type"] is JArray types && webPage["mainEntity"] is JArray questionList && questionList.Count == 0)
		{
			webPage["@type"] = types.First;
			webPage.Remove("mainEntity");
		}

		JObject document = new JObject { ["@context"] = "https://schema.org", ["@graph"] = graph };
		// no "</script>" can end the block early: the serializer escapes <, > and &
		return JsonConvert.SerializeObject(document, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeHtml, NullValueHandling = NullValueHandling.Ignore });
	}

	/// <summary>BASTAU LINE: the company behind kieli.kz, with the office address, contacts and opening hours of «Байланыс».</summary>
	private static JObject Organization(IMemoryCache cache, string lang, string siteUrl, Func<string, string> url, string id, string founder)
	{
		BlockContent contact = ContentStore.Block(cache, "site.contact", lang);
		BlockContent brand = ContentStore.Block(cache, "site.brand", lang);
		JObject baseContact = ContentStore.BaseBlock(cache, "site.contact");
		JObject node = new JObject
		{
			["@type"] = "LocalBusiness",
			["@id"] = id,
			["name"] = "BASTAU LINE",
			["legalName"] = Plain(contact["legalName"]),
			["alternateName"] = new JArray(new[] { brand["name"], "«BASTAU LINE» ЖШС", "BASTAU LINE LLP" }.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct()),
			["url"] = siteUrl + "/",
			["logo"] = new JObject { ["@type"] = "ImageObject", ["url"] = siteUrl + "/kieli/img/icon-512.png", ["width"] = 512, ["height"] = 512 },
			["image"] = siteUrl + "/kieli/img/og-kieli.jpg",
			["description"] = Plain(ContentStore.Block(cache, "seo.home", lang)["description"]),
			["founder"] = Ref(founder),
			["areaServed"] = new JArray(new JObject { ["@type"] = "Country", ["name"] = "Kazakhstan" }, new JObject { ["@type"] = "Country", ["name"] = "China" }),
			["knowsLanguage"] = new JArray("kk", "zh", "ru"),
			["address"] = Address((string)baseContact["address"])
		};
		if (contact.Has("email"))
		{
			node["email"] = contact["email"];
		}
		if (contact.Has("phone"))
		{
			node["telephone"] = contact["phone"];
		}
		JArray hours = OpeningHours((string)baseContact["hours"]);
		if (hours.Count > 0)
		{
			node["openingHoursSpecification"] = hours;
		}
		JArray sameAs = new JArray(ContentStore.Block(cache, "site.social", lang).List("items").Select(s => s["url"]).Where(u => u.StartsWith("http", StringComparison.OrdinalIgnoreCase)).Distinct());
		if (sameAs.Count > 0)
		{
			node["sameAs"] = sameAs;
		}
		JObject point = new JObject
		{
			["@type"] = "ContactPoint",
			["contactType"] = "customer service",
			["availableLanguage"] = new JArray("Kazakh", "Chinese", "Russian"),
			["url"] = url("/kz/services")
		};
		if (contact.Has("email"))
		{
			point["email"] = contact["email"];
		}
		if (contact.Has("phone"))
		{
			point["telephone"] = contact["phone"];
		}
		node["contactPoint"] = point;
		return node;
	}

	private static JObject WebSite(IMemoryCache cache, string lang, string siteUrl, string org, string id) => new JObject
	{
		["@type"] = "WebSite",
		["@id"] = id,
		["url"] = siteUrl + "/",
		["name"] = ContentStore.Block(cache, "site.brand", lang)["name"],
		["alternateName"] = new JArray("Киелі", "Kieli"),
		["inLanguage"] = new JArray(SiteLanguages.Enabled(cache).Select(l => l.Html)),
		["publisher"] = Ref(org)
	};

	/// <summary>Омар Бекмұрат, the founder and the author of the site, with his name as it is written in every site language.</summary>
	private static JObject Founder(IMemoryCache cache, string lang, string siteUrl, Func<string, string> url, string org, string id)
	{
		BlockContent hero = ContentStore.Block(cache, "author.hero", lang);
		string name = FounderName(cache, lang);
		JObject node = new JObject
		{
			["@type"] = "Person",
			["@id"] = id,
			["name"] = name,
			["alternateName"] = new JArray(SiteLanguages.All.Select(l => FounderName(cache, l.Culture)).Append("Omar Bekmurat").Where(n => n.Length > 0 && n != name).Distinct()),
			["jobTitle"] = Plain(hero["role"]),
			["description"] = Plain(ContentStore.Block(cache, "seo.author", lang)["description"]),
			["url"] = url("/kz/author"),
			["worksFor"] = Ref(org),
			["knowsLanguage"] = new JArray("kk", "zh", "ru")
		};
		if (hero.Has("photo"))
		{
			node["image"] = hero["photo"].StartsWith("http", StringComparison.OrdinalIgnoreCase) ? hero["photo"] : siteUrl + hero["photo"];
		}
		return node;
	}

	private static string FounderName(IMemoryCache cache, string lang) => Plain(ContentStore.Block(cache, "author.hero", lang)["name"]);

	/// <summary>The questions and answers of a block's «items» (or «faq») list.</summary>
	private static JArray Questions(BlockContent block)
	{
		IEnumerable<BlockContent> items = block.List("items").Concat(block.List("faq"));
		return new JArray(items.Where(i => i.Has("q") && i.Has("a")).Select(i => new JObject
		{
			["@type"] = "Question",
			["name"] = Plain(i["q"]),
			["acceptedAnswer"] = new JObject { ["@type"] = "Answer", ["text"] = Plain(i["a"]) }
		}));
	}

	/// <summary>"Астана қ., Айнакөл көшесі, 66" → city and street.</summary>
	private static JObject Address(string address)
	{
		address = Plain(address);
		JObject node = new JObject { ["@type"] = "PostalAddress", ["addressCountry"] = "KZ" };
		Match m = Regex.Match(address, @"^\s*([^,]+?)\s+қ\.?\s*,\s*(.+)$");
		if (m.Success)
		{
			node["addressLocality"] = m.Groups[1].Value.Trim();
			node["streetAddress"] = m.Groups[2].Value.Trim();
		}
		else if (address.Length > 0)
		{
			node["streetAddress"] = address;
		}
		return node;
	}

	/// <summary>"Дс–Жм 10:00–19:00, Сб 10:00–15:00" → opening hours (nothing when the text has another form).</summary>
	private static JArray OpeningHours(string text)
	{
		JArray result = new JArray();
		foreach (Match m in Regex.Matches(text ?? string.Empty, @"(Дс|Сс|Ср|Бс|Жм|Сб|Жс)(?:\s*[–-]\s*(Дс|Сс|Ср|Бс|Жм|Сб|Жс))?\s+(\d{1,2}:\d{2})\s*[–-]\s*(\d{1,2}:\d{2})"))
		{
			int from = KazakhDays[m.Groups[1].Value], to = m.Groups[2].Success ? KazakhDays[m.Groups[2].Value] : from;
			if (to < from)
			{
				continue;
			}
			result.Add(new JObject
			{
				["@type"] = "OpeningHoursSpecification",
				["dayOfWeek"] = new JArray(Enumerable.Range(from, to - from + 1).Select(d => DayNames[d])),
				["opens"] = m.Groups[3].Value.PadLeft(5, '0'),
				["closes"] = m.Groups[4].Value.PadLeft(5, '0')
			});
		}
		return result;
	}

	private static JObject Ref(string id) => new JObject { ["@id"] = id };

	/// <summary>Kazakhstan has one time zone, UTC+5, since 2024.</summary>
	private static string IsoTime(int unix) =>
		DateTimeOffset.FromUnixTimeSeconds(unix).ToOffset(TimeSpan.FromHours(5)).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

	/// <summary>Text without tags, entities decoded, white space collapsed.</summary>
	public static string Plain(string html)
	{
		if (string.IsNullOrEmpty(html))
		{
			return string.Empty;
		}
		string text = Regex.Replace(html, @"<(br|/p|/li|/h\d)\s*/?>", " ", RegexOptions.IgnoreCase);
		text = Regex.Replace(text, "<[^>]+>", string.Empty);
		return Regex.Replace(WebUtility.HtmlDecode(text), @"\s+", " ").Trim();
	}
}
