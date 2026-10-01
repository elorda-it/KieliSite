using System;
using System.Collections.Generic;
using System.Linq;

namespace KieliWeb.Setup;

public enum FieldKind
{
	Text,
	/// <summary>Several paragraphs of plain text.</summary>
	Textarea,
	/// <summary>Short text whose line breaks are shown (e.g. a name over two lines).</summary>
	Lines,
	/// <summary>Rich text edited with TinyMCE.</summary>
	Html,
	Image,
	Url,
	Number,
	Bool,
	/// <summary>Id of a symbol in the site's SVG sprite (i-doc, i-yurt, ...).</summary>
	Icon,
	Select,
	/// <summary>A service of the «Кеңес алу» list: "tab:item", or "tab:" for a general question.</summary>
	Service,
	/// <summary>A calendar date (entities only; stored as unix time).</summary>
	Date,
	/// <summary>An audio file (MP3, M4A…): uploaded to /uploads/audio/ or linked.</summary>
	Audio,
	/// <summary>Repeated rows; the row fields are in <see cref="FieldDef.Fields"/>.</summary>
	List
}

public sealed class FieldDef
{
	public FieldDef(string name, string label, FieldKind kind)
	{
		Name = name;
		Label = label;
		Kind = kind;
	}

	public string Name { get; }

	public string Label { get; }

	public FieldKind Kind { get; }

	public FieldDef[] Fields { get; init; } = Array.Empty<FieldDef>();

	public (string Value, string Label)[] Options { get; init; } = Array.Empty<(string, string)>();

	/// <summary>Must not be empty when saving.</summary>
	public bool Required { get; init; }

	/// <summary>A hint under the field in the admin.</summary>
	public string Help { get; init; }

	/// <summary>Entity field stored under another property name (e.g. "steps" in StepsJson).</summary>
	public string Column { get; init; }

	/// <summary>Html fields: a tall editor for whole articles.</summary>
	public bool Large { get; init; }

	/// <summary>A text field that is the same in every language (addresses, codes).</summary>
	public bool NoTranslate { get; init; }

	/// <summary>Content tables: the value a new row starts with ("true" for a Bool field).</summary>
	public string Default { get; init; }

	/// <summary>Has its own value in each site language: text fields, and lists with text in their rows.</summary>
	public bool Translatable => !NoTranslate && (Kind is FieldKind.Text or FieldKind.Textarea or FieldKind.Lines or FieldKind.Html
		|| (Kind == FieldKind.List && Fields.Any(f => f.Translatable)));
}

public sealed class BlockDef
{
	public BlockDef(string key, string page, string title, string help, FieldDef[] fields)
	{
		Key = key;
		Page = page;
		Title = title;
		Help = help;
		Fields = fields;
	}

	public string Key { get; }

	public string Page { get; }

	public string Title { get; }

	public string Help { get; }

	public FieldDef[] Fields { get; }
}

/// <summary>
/// Every editable page block. The definitions (labels, field types) are in
/// BlockRegistry.Generated.cs; the stored values are in the `pageblock` table and the
/// first values in db/seed/blocks.json.
/// </summary>
public static partial class BlockRegistry
{
	private static BlockDef[] _ui = Array.Empty<BlockDef>();

	private static readonly Dictionary<string, (string Title, string Help)> UiTitles = new Dictionary<string, (string, string)>
	{
		["ui.common"] = ("Жалпы жазулар: мәзір, батырмалар, футер", null),
		["ui.form"] = ("«Кеңес алу» формасы: жазулар мен хабарламалар", "Сервер қайтаратын қате хабарламалары да осында."),
		["ui.services"] = ("Қызметтер: тұрақты жазулар", null),
		["ui.places"] = ("Қазақстан, нысандар: тұрақты жазулар", null),
		["ui.news"] = ("Жаңалықтар: тұрақты жазулар", null),
		["ui.kaztest"] = ("ҚАЗТЕСТ: тест терезесінің жазулары", "Тест сұрақтары әрқашан қазақша қалады; мұнда тек батырмалар мен түсіндірмелер.")
	};

	/// <summary>Interface texts (buttons, form labels, messages), from db/seed/ui.json: every string is one field.</summary>
	public static void LoadUi(Newtonsoft.Json.Linq.JObject defaults)
	{
		_ui = defaults.Properties().Where(p => p.Value is Newtonsoft.Json.Linq.JObject).Select(p =>
		{
			(string title, string help) = UiTitles.TryGetValue(p.Name, out var t) ? t : (p.Name, null);
			string placeholders = "{n}, {name} сияқты жақшадағы белгілерді өзгертпеңіз — олардың орнына сан не атау қойылады.";
			FieldDef[] fields = ((Newtonsoft.Json.Linq.JObject)p.Value).Properties().Select(f =>
			{
				string text = (string)f.Value ?? string.Empty;
				return new FieldDef(f.Name, text.Length > 70 ? text.Substring(0, 67) + "…" : text, text.Length > 70 ? FieldKind.Textarea : FieldKind.Text)
				{
					Help = text.Contains('{') ? placeholders : null
				};
			}).ToArray();
			return new BlockDef(p.Name, "ui", title, help, fields);
		}).ToArray();
	}

	public static IReadOnlyList<BlockDef> All => Defined.Concat(_ui).ToList();

	public static IEnumerable<(string Key, string Title)> AllPages => Pages.Append(("ui", "Интерфейс жазулары (батырмалар, формалар, тест)"));

	public static BlockDef Find(string key) => All.FirstOrDefault(b => b.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

	public static string PageTitle(string page) => AllPages.FirstOrDefault(p => p.Key == page).Title ?? page;

	/// <summary>Symbols of the site sprite that make sense as icons in the admin pickers.</summary>
	public static readonly string[] Icons =
	{
		"i-doc", "i-translate", "i-passport", "i-idcard", "i-stamp", "i-seal", "i-yurt", "i-road", "i-cap", "i-book", "i-headphones",
		"i-briefcase", "i-building", "i-home", "i-key", "i-family", "i-user", "i-award", "i-hospital", "i-globe", "i-lang", "i-pin",
		"i-map", "i-360", "i-shield", "i-check", "i-check-circle", "i-minus-circle", "i-ban", "i-clock", "i-car", "i-rings", "i-pen",
		"i-search-doc", "i-plane", "i-bridge", "i-ornament", "i-tenge", "i-info", "i-phone", "i-wechat", "i-whatsapp", "i-telegram",
		"i-instagram", "i-youtube", "i-douyin", "i-link", "i-sun", "i-moon"
	};
}
