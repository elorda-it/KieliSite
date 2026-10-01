using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace KieliWeb.Setup.Cms;

/// <summary>What the shared admin form (Views/Console/Cms/Edit.cshtml) shows: a content row or a page block.</summary>
public sealed class CmsEditModel
{
	public string Group { get; init; }

	public string ListTitle { get; init; }

	public string ListUrl { get; init; }

	public string Heading { get; init; }

	public string Help { get; init; }

	public string FormAction { get; init; }

	public int RowId { get; init; }

	/// <summary>Extra hidden form value (the block key).</summary>
	public string Key { get; init; }

	public IReadOnlyList<FieldDef> Fields { get; init; }

	public JObject Values { get; init; }

	/// <summary>Fields stored together as JSON under "data" (service pages).</summary>
	public IReadOnlyList<FieldDef> DataFields { get; init; }

	public JObject DataValues { get; init; }

	public string DataTitle { get; init; }

	public string PublicUrl { get; init; }

	/// <summary>Page blocks: POST address that puts the prototype texts back.</summary>
	public string ResetUrl { get; init; }

	/// <summary>The language being edited: kz (the content itself) or a translation language.</summary>
	public string Lang { get; init; } = SiteLanguages.Base;

	public bool Translate => !SiteLanguages.IsBase(Lang);

	/// <summary>The language tabs above the form (empty when the content is not translated).</summary>
	public IReadOnlyList<LanguageTab> Tabs { get; init; } = new List<LanguageTab>();

	/// <summary>Translation form: the Kazakh values, shown under each field.</summary>
	public JObject BaseValues { get; init; }

	public JObject BaseDataValues { get; init; }
}

public sealed record LanguageTab(string Culture, string Name, int Percent, string Url, bool Active);

/// <summary>
/// A set of fields with their values, rendered by Views/Console/Cms/_Fields.cshtml. In a translation form
/// only the translatable fields are shown, each with its Kazakh text, and list rows follow the Kazakh rows.
/// </summary>
public sealed record FieldsModel(IReadOnlyList<FieldDef> Fields, JObject Values, JObject BaseValues = null, bool Translate = false);
