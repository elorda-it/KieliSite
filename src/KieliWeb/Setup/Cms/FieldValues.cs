using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using COMMON;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KieliWeb.Setup.Cms;

/// <summary>
/// Values of an admin form (page block or content row) as JSON: cleaning what the editor sent,
/// and moving values between the JSON and an entity's properties.
/// </summary>
public static class FieldValues
{
	private static readonly Regex ServiceKey = new Regex("^[a-z0-9-]+:[a-z0-9-]*$", RegexOptions.Compiled);

	private static readonly Regex BadScheme = new Regex(@"^\s*(javascript|vbscript|data):", RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static readonly string[] EmbedHosts = { "www.youtube.com", "youtube.com", "www.youtube-nocookie.com", "player.vimeo.com", "www.google.com", "maps.google.com", "yandex.kz", "yandex.ru" };

	/// <summary>Keeps only the defined fields, in their proper types; problems are added to <paramref name="errors"/>.</summary>
	public static JObject Clean(IEnumerable<FieldDef> fields, JToken input, List<string> errors, string where = null)
	{
		JObject source = input as JObject ?? new JObject();
		JObject result = new JObject();
		foreach (FieldDef f in fields)
		{
			JToken raw = source[f.Name];
			string label = where == null ? "«" + f.Label + "»" : where + ": «" + f.Label + "»";
			switch (f.Kind)
			{
			case FieldKind.List:
			{
				JArray rows = new JArray();
				HashSet<string> ids = new HashSet<string>();
				int n = 0;
				foreach (JToken row in raw as JArray ?? new JArray())
				{
					n++;
					JObject clean = Clean(f.Fields, row, errors, "«" + f.Label + "», " + n + "-жол");
					if (clean.Properties().Any(p => !IsEmpty(p.Value)))
					{
						// a row keeps its id for good: translations of the row are attached to it
						string id = RowId(row);
						if (id == null || !ids.Add(id))
						{
							do { id = NewRowId(); } while (!ids.Add(id));
						}
						clean["_id"] = id;
						rows.Add(clean);
					}
				}
				if (f.Required && rows.Count == 0)
				{
					errors.Add(label + " — кемінде бір жол керек.");
				}
				result[f.Name] = rows;
				break;
			}
			case FieldKind.Bool:
				result[f.Name] = raw != null && raw.Type == JTokenType.Boolean ? (bool)raw : IsTrue(raw?.ToString());
				break;
			default:
			{
				string v = raw == null || raw.Type == JTokenType.Null ? string.Empty : raw.Type == JTokenType.String ? (string)raw : raw.ToString(Formatting.None);
				v = f.Kind == FieldKind.Textarea || f.Kind == FieldKind.Lines || f.Kind == FieldKind.Html ? v.Replace("\r\n", "\n").Trim() : v.Trim();
				switch (f.Kind)
				{
				case FieldKind.Url:
				case FieldKind.Image:
				case FieldKind.Audio:
					if (BadScheme.IsMatch(v))
					{
						errors.Add(label + ": сілтеме http(s):// не / белгісінен басталуы керек.");
						v = string.Empty;
					}
					break;
				case FieldKind.Number:
					if (v.Length > 0 && !decimal.TryParse(v.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out _))
					{
						errors.Add(label + ": сан жазыңыз.");
					}
					v = v.Replace(',', '.');
					break;
				case FieldKind.Date:
					if (v.Length > 0 && !DateTime.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
					{
						errors.Add(label + ": күнді дұрыс таңдаңыз.");
					}
					break;
				case FieldKind.Select:
					if (v.Length > 0 && f.Options.Length > 0 && !f.Options.Any(o => o.Value == v))
					{
						errors.Add(label + ": тізімнен таңдаңыз.");
					}
					break;
				case FieldKind.Service:
					if (v.Length > 0 && !ServiceKey.IsMatch(v))
					{
						errors.Add(label + ": қызметті тізімнен таңдаңыз.");
						v = string.Empty;
					}
					break;
				case FieldKind.Html:
					v = SafeHtml(v);
					break;
				}
				if (f.Required && v.Length == 0)
				{
					errors.Add(label + " толтырылуы керек.");
				}
				// numbers stay numbers in the JSON (as in the seed), everything else is text
				if (f.Kind == FieldKind.Number && decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number))
				{
					result[f.Name] = number == Math.Truncate(number) && Math.Abs(number) < long.MaxValue ? new JValue((long)number) : new JValue(number);
				}
				else
				{
					result[f.Name] = v;
				}
				break;
			}
			}
		}
		return result;
	}

	private static readonly Regex RowIdPattern = new Regex("^[A-Za-z0-9_-]{1,24}$", RegexOptions.Compiled);

	public static string RowId(JToken row)
	{
		string id = (row as JObject)?["_id"]?.Type == JTokenType.String ? (string)row["_id"] : null;
		return id != null && RowIdPattern.IsMatch(id) ? id : null;
	}

	public static string NewRowId() => Guid.NewGuid().ToString("N").Substring(0, 8);

	/// <summary>
	/// A translation as sent from the admin: only the translatable fields; list rows keep the id of the
	/// Kazakh row they translate. Empty values are dropped (the Kazakh text shows instead).
	/// </summary>
	public static JObject CleanTranslation(IEnumerable<FieldDef> fields, JToken input)
	{
		JObject source = input as JObject ?? new JObject();
		JObject result = new JObject();
		foreach (FieldDef f in fields.Where(f => f.Translatable))
		{
			JToken raw = source[f.Name];
			if (f.Kind == FieldKind.List)
			{
				JArray rows = new JArray();
				foreach (JToken row in raw as JArray ?? new JArray())
				{
					string id = RowId(row);
					JObject clean = CleanTranslation(f.Fields, row);
					if (id != null && clean.HasValues)
					{
						clean["_id"] = id;
						rows.Add(clean);
					}
				}
				if (rows.Count > 0)
				{
					result[f.Name] = rows;
				}
				continue;
			}
			string v = raw == null || raw.Type == JTokenType.Null ? string.Empty : raw.Type == JTokenType.String ? (string)raw : raw.ToString(Formatting.None);
			v = v.Replace("\r\n", "\n").Trim();
			if (f.Kind == FieldKind.Html)
			{
				v = SafeHtml(v);
			}
			if (v.Length > 0)
			{
				result[f.Name] = v;
			}
		}
		return result;
	}

	/// <summary>
	/// Puts a translation over the Kazakh values: translated texts replace theirs, list rows are matched by
	/// their id (by position for old rows without one), and everything else (images, links, icons) stays.
	/// </summary>
	public static void ApplyTranslation(JObject target, IEnumerable<FieldDef> fields, JObject translation)
	{
		if (target == null || translation == null)
		{
			return;
		}
		foreach (FieldDef f in fields.Where(f => f.Translatable))
		{
			JToken t = translation[f.Name];
			if (t == null)
			{
				continue;
			}
			if (f.Kind == FieldKind.List)
			{
				if (!(target[f.Name] is JArray baseRows) || !(t is JArray trRows))
				{
					continue;
				}
				for (int i = 0; i < baseRows.Count; i++)
				{
					if (!(baseRows[i] is JObject row))
					{
						continue;
					}
					string id = RowId(row);
					JObject match = trRows.OfType<JObject>().FirstOrDefault(r => id != null && RowId(r) == id)
						?? (i < trRows.Count && trRows[i] is JObject byIndex && RowId(byIndex) == null ? byIndex : null);
					ApplyTranslation(row, f.Fields, match);
				}
				continue;
			}
			string v = t.Type == JTokenType.String ? (string)t : null;
			if (!string.IsNullOrWhiteSpace(v))
			{
				target[f.Name] = v;
			}
		}
	}

	/// <summary>Site links in a block or row follow the page language (/kz/… → /ru/…).</summary>
	public static void LocalizeLinks(JObject target, IEnumerable<FieldDef> fields, string culture)
	{
		if (target == null || SiteLanguages.IsBase(culture))
		{
			return;
		}
		foreach (FieldDef f in fields)
		{
			JToken t = target[f.Name];
			if (t == null)
			{
				continue;
			}
			if (f.Kind == FieldKind.List && t is JArray rows)
			{
				foreach (JObject row in rows.OfType<JObject>())
				{
					LocalizeLinks(row, f.Fields, culture);
				}
			}
			else if (t.Type == JTokenType.String && f.Kind == FieldKind.Url)
			{
				target[f.Name] = SiteLanguages.Localize((string)t, culture);
			}
			else if (t.Type == JTokenType.String && (f.Kind == FieldKind.Html || f.Kind == FieldKind.Textarea))
			{
				target[f.Name] = SiteLanguages.LocalizeHtml((string)t, culture);
			}
		}
	}

	/// <summary>How much of the Kazakh text has a translation: (translated, total) over non-empty Kazakh texts.</summary>
	public static (int Done, int Total) Coverage(IEnumerable<FieldDef> fields, JObject baseValues, JObject translation)
	{
		int done = 0, total = 0;
		foreach (FieldDef f in fields.Where(f => f.Translatable))
		{
			JToken b = baseValues?[f.Name];
			if (f.Kind == FieldKind.List)
			{
				JArray trRows = translation?[f.Name] as JArray;
				foreach (JObject row in (b as JArray ?? new JArray()).OfType<JObject>())
				{
					string id = RowId(row);
					JObject match = trRows?.OfType<JObject>().FirstOrDefault(r => id != null && RowId(r) == id);
					(int d, int t) = Coverage(f.Fields, row, match);
					done += d;
					total += t;
				}
				continue;
			}
			// only texts with words need a translation: dates, numbers and codes stay as they are
			if (b != null && b.Type == JTokenType.String && ((string)b).Any(char.IsLetter))
			{
				total++;
				if (translation?[f.Name] is JToken tv && tv.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)tv))
				{
					done++;
				}
			}
		}
		return (done, total);
	}

	public static bool IsEmpty(JToken t)
	{
		return t == null || t.Type == JTokenType.Null || (t.Type == JTokenType.String && string.IsNullOrWhiteSpace((string)t)) || (t is JArray a && a.Count == 0) || (t.Type == JTokenType.Boolean && !(bool)t);
	}

	private static bool IsTrue(string v)
	{
		v = (v ?? string.Empty).Trim().ToLowerInvariant();
		return v == "1" || v == "true" || v == "on" || v == "yes";
	}

	/// <summary>Rich text from the editor, without scripts, event handlers or javascript: links.</summary>
	public static string SafeHtml(string html)
	{
		if (string.IsNullOrWhiteSpace(html))
		{
			return string.Empty;
		}
		HtmlDocument doc = new HtmlDocument();
		doc.LoadHtml(html);
		foreach (HtmlNode node in doc.DocumentNode.Descendants().Where(n => n.Name is "script" or "style" or "object" or "embed" or "form" or "input" or "button" or "meta" or "link" or "base").ToList())
		{
			node.Remove();
		}
		foreach (HtmlNode node in doc.DocumentNode.Descendants("iframe").ToList())
		{
			string host = Uri.TryCreate(node.GetAttributeValue("src", string.Empty), UriKind.Absolute, out Uri src) && src.Scheme == "https" ? src.Host : string.Empty;
			if (!EmbedHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
			{
				node.Remove();
			}
		}
		foreach (HtmlNode node in doc.DocumentNode.Descendants().Where(n => n.NodeType == HtmlNodeType.Element))
		{
			foreach (HtmlAttribute attr in node.Attributes.ToList())
			{
				if (attr.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase) || ((attr.Name is "href" or "src" or "xlink:href" or "action" or "formaction") && BadScheme.IsMatch(attr.Value)))
				{
					attr.Remove();
				}
			}
		}
		return doc.DocumentNode.OuterHtml.Trim();
	}

	// ---- entity <-> JSON --------------------------------------------------------------

	private static PropertyInfo Property(Type type, FieldDef f)
	{
		return type.GetProperty(f.Column ?? f.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
	}

	/// <summary>The entity's values in the editor's JSON shape (lists from their *Json columns, dates as yyyy-MM-dd).</summary>
	public static JObject FromEntity(object entity, IEnumerable<FieldDef> fields)
	{
		JObject o = new JObject();
		if (entity == null)
		{
			return o;
		}
		foreach (FieldDef f in fields)
		{
			PropertyInfo p = Property(entity.GetType(), f);
			if (p == null)
			{
				continue;
			}
			object v = p.GetValue(entity);
			switch (f.Kind)
			{
			case FieldKind.List:
				o[f.Name] = ContentStore.ParseArray(v as string);
				break;
			case FieldKind.Bool:
				o[f.Name] = Convert.ToInt64(v ?? 0) != 0;
				break;
			case FieldKind.Date:
				int unix = Convert.ToInt32(v ?? 0);
				o[f.Name] = unix > 0 ? UnixTimeHelper.UnixTimeToDateTime(unix).ToString("yyyy-MM-dd") : string.Empty;
				break;
			default:
				o[f.Name] = v is decimal d ? d.ToString(CultureInfo.InvariantCulture) : Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty;
				break;
			}
		}
		return o;
	}

	/// <summary>Writes cleaned values back into the entity's properties, converting to each property's type.</summary>
	public static void ToEntity(object entity, IEnumerable<FieldDef> fields, JObject values)
	{
		foreach (FieldDef f in fields)
		{
			PropertyInfo p = Property(entity.GetType(), f);
			if (p == null || !p.CanWrite)
			{
				continue;
			}
			JToken t = values[f.Name];
			Type type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
			object value;
			if (f.Kind == FieldKind.List)
			{
				value = (t as JArray ?? new JArray()).ToString(Formatting.None);
			}
			else if (f.Kind == FieldKind.Date)
			{
				string s = (string)t ?? string.Empty;
				int current = Convert.ToInt32(p.GetValue(entity) ?? 0);
				if (current > 0 && UnixTimeHelper.UnixTimeToDateTime(current).ToString("yyyy-MM-dd") == s)
				{
					continue; // same day: keep the exact time it had
				}
				value = DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date) ? UnixTimeHelper.ConvertToUnixTime(date.AddHours(12)) : 0;
			}
			else if (f.Kind == FieldKind.Bool)
			{
				value = t != null && t.Type == JTokenType.Boolean && (bool)t ? 1 : 0;
			}
			else
			{
				value = t == null || t.Type == JTokenType.Null ? string.Empty : t.Type == JTokenType.String ? (string)t : t.ToString(Formatting.None);
			}
			p.SetValue(entity, ConvertTo(value, type));
		}
	}

	private static object ConvertTo(object value, Type type)
	{
		if (type == typeof(string))
		{
			return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
		}
		string s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
		if (s.Length == 0)
		{
			return Activator.CreateInstance(type);
		}
		if (type == typeof(decimal))
		{
			return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d) ? d : 0m;
		}
		try
		{
			if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal n))
			{
				return Convert.ChangeType(Math.Round(n), type, CultureInfo.InvariantCulture);
			}
		}
		catch (OverflowException)
		{
		}
		return Activator.CreateInstance(type);
	}
}
