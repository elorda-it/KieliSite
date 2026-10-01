using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using COMMON;
using Dapper;

namespace KieliWeb.Setup.Cms;

/// <summary>A column of an admin list. <see cref="Html"/> returns ready (encoded) HTML for the cell.</summary>
public sealed class ColumnDef
{
	public ColumnDef(string title, Func<object, CmsLookups, string> html, string sortSql = null)
	{
		Title = title;
		Html = html;
		SortSql = sortSql;
	}

	public string Title { get; }

	public Func<object, CmsLookups, string> Html { get; }

	/// <summary>SQL for ordering by this column; null = not sortable.</summary>
	public string SortSql { get; }
}

/// <summary>Names of related rows, loaded once per list request.</summary>
public sealed class CmsLookups
{
	public Dictionary<int, string> ServiceTabs { get; init; }

	public Dictionary<int, string> ServiceTabSlugs { get; init; }

	public Dictionary<int, string> ArticleCategories { get; init; }

	public Dictionary<int, string> Regions { get; init; }

	public Dictionary<int, string> PlaceCategories { get; init; }

	public Dictionary<int, string> Variants { get; init; }

	public Dictionary<int, int> Counts { get; init; } = new Dictionary<int, int>();

	public static CmsLookups Load(IDbConnection c)
	{
		Dictionary<int, string> Names(string sql) => c.Query<(int, string)>(sql).ToDictionary(x => x.Item1, x => x.Item2);
		return new CmsLookups
		{
			ServiceTabs = Names("select id, name from servicetab where qStatus = 0"),
			ServiceTabSlugs = Names("select id, slug from servicetab where qStatus = 0"),
			ArticleCategories = Names("select id, name from articlecategory where qStatus = 0"),
			Regions = Names("select id, name from region where qStatus = 0"),
			PlaceCategories = Names("select id, name from placecategory where qStatus = 0"),
			Variants = Names("select id, title from kaztestvariant where qStatus = 0")
		};
	}

	public static string Name(Dictionary<int, string> map, int id) => map != null && map.TryGetValue(id, out string n) ? n : "—";
}

/// <summary>A filter select above an admin list (e.g. by service tab), also settable from ?key=id.</summary>
public sealed class FilterDef
{
	public FilterDef(string column, string label, string queryKey, Func<IDbConnection, (string Value, string Label)[]> options)
	{
		Column = column;
		Label = label;
		QueryKey = queryKey;
		Options = options;
	}

	public string Column { get; }

	public string Label { get; }

	public string QueryKey { get; }

	public Func<IDbConnection, (string Value, string Label)[]> Options { get; }
}

/// <summary>
/// One content table in the admin: its menu address (/{Controller}/{Action}/list), form fields,
/// list columns, and the rules applied when saving or deleting.
/// </summary>
public sealed class EntityDef
{
	public string Controller { get; init; }

	public string Action { get; init; }

	public string Group { get; init; }

	public string Title { get; init; }

	public string Singular { get; init; }

	public string Help { get; init; }

	public Type Model { get; init; }

	public string Table => Model.Name.ToLowerInvariant();

	/// <summary>Form fields; a function so selects can list related rows.</summary>
	public Func<IDbConnection, FieldDef[]> Fields { get; init; }

	/// <summary>Further fields kept together as JSON in the row's DataJson (service pages).</summary>
	public FieldDef[] DataFields { get; init; }

	public ColumnDef[] Columns { get; init; }

	public string[] Search { get; init; } = Array.Empty<string>();

	public string DefaultOrder { get; init; } = "displayOrder, id";

	public FilterDef Filter { get; init; }

	/// <summary>Before saving (entity, isNew): fill defaults and check; returns an error text or null.</summary>
	public Func<IDbConnection, object, bool, string> Prepare { get; init; }

	/// <summary>Before deleting: returns an error text (e.g. the row is still in use) or null.</summary>
	public Func<IDbConnection, List<int>, string> DeleteGuard { get; init; }

	/// <summary>Where the row is seen on the site (for «Сайтта көру»).</summary>
	public Func<IDbConnection, object, string> PublicUrl { get; init; }

	public bool CanCreate { get; init; } = true;

	/// <summary>A button above the list that posts run=1 to the save address and shows the answer (e.g. «Қазір тексеру»).</summary>
	public (string Label, string Icon)? ListCommand { get; init; }

	/// <summary>Has translations into the other site languages (ҚАЗТЕСТ questions stay Kazakh).</summary>
	public bool Translatable { get; init; } = true;

	public string ListUrl(string culture) => $"/{culture}/{Controller.ToLowerInvariant()}/{Action.ToLowerInvariant()}/list";

	public string EditUrl(string culture, int id) => $"/{culture}/{Controller.ToLowerInvariant()}/{Action.ToLowerInvariant()}/edit?id={id}";

	public string CreateUrl(string culture) => $"/{culture}/{Controller.ToLowerInvariant()}/{Action.ToLowerInvariant()}/create";
}

/// <summary>Small helpers shared by the entity definitions.</summary>
public static class CmsText
{
	public static string E(string s) => WebUtility.HtmlEncode(s ?? string.Empty);

	public static string Short(string s, int max = 90)
	{
		s = Regex.Replace(s ?? string.Empty, @"\s+", " ").Trim();
		return E(s.Length > max ? s.Substring(0, max - 1).TrimEnd() + "…" : s);
	}

	public static string Badge(string text, string tone = "secondary") => $"<span class=\"badge bg-light-{tone} text-{tone} me-1\">{E(text)}</span>";

	public static string Date(int unix) => unix > 0 ? UnixTimeHelper.UnixTimeToDateTime(unix).ToString("dd.MM.yyyy") : "—";

	public static string Thumb(string url) => string.IsNullOrWhiteSpace(url) ? string.Empty
		: $"<img src=\"{E(ContentStore.ImageSize(url, "middle"))}\" alt=\"\" class=\"kf-thumb\" loading=\"lazy\">";

	/// <summary>Latin address part from a Kazakh/Russian title: «Шарын шатқалы» → sharyn-shatqaly.</summary>
	public static string Slugify(string text)
	{
		// Kazakh Latin has letters with no ASCII base (ı from і); the rest lose their marks below
		string latin = Cyrl2LatynHelper.Cyrl2Latyn(text ?? string.Empty).Replace('ı', 'i').Replace('İ', 'i').Normalize(NormalizationForm.FormD);
		StringBuilder sb = new StringBuilder();
		foreach (char ch in latin)
		{
			if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
			{
				continue;
			}
			char c = char.ToLowerInvariant(ch);
			sb.Append(c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '-');
		}
		string slug = Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
		return slug.Length > 60 ? slug.Substring(0, 60).TrimEnd('-') : slug;
	}

	public static bool IsSlug(string s) => Regex.IsMatch(s ?? string.Empty, "^[a-z0-9]+(-[a-z0-9]+)*$");
}
