using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using MODEL;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KieliWeb.Setup.Cms;

/// <summary>
/// Translations as the admin edits them: one «multilanguage» row per (table, row, field, language).
/// Page blocks keep their whole translation as one JSON value (column "dataJson"); content rows keep one
/// value per field, lists as JSON, and a service page's sections as JSON under "data".
/// </summary>
public static class TranslationStore
{
	public const string DataColumn = "data";

	/// <summary>(row id, column) → value, for some rows of a table in one language.</summary>
	public static Dictionary<(int, string), string> Load(IDbConnection c, string table, IEnumerable<int> ids, string lang)
	{
		List<int> list = ids.Distinct().ToList();
		if (list.Count == 0)
		{
			return new Dictionary<(int, string), string>();
		}
		return c.Query<(int Id, string Column, string Value)>(
				"select columnId, columnName, columnValue from multilanguage where qStatus = 0 and tableName = @table and language = @lang and columnId in @list order by id",
				new { table, lang, list })
			.GroupBy(x => (x.Id, x.Column.ToLowerInvariant()))
			.ToDictionary(g => g.Key, g => g.Last().Value);
	}

	/// <summary>Stores one translated value; an empty value removes the translation (the Kazakh text shows again).</summary>
	public static void Save(IDbConnection c, string table, int id, string column, string lang, string value)
	{
		List<Multilanguage> rows = c.GetList<Multilanguage>("where qStatus = 0 and tableName = @table and columnId = @id and columnName = @column and language = @lang",
			new { table, id, column, lang }).ToList();
		Multilanguage row = rows.FirstOrDefault();
		foreach (Multilanguage extra in rows.Skip(1))
		{
			c.Execute("update multilanguage set qStatus = 1 where id = @id", new { id = extra.Id });
		}
		if (string.IsNullOrWhiteSpace(value))
		{
			if (row != null)
			{
				c.Execute("update multilanguage set qStatus = 1 where id = @id", new { id = row.Id });
			}
			return;
		}
		if (row == null)
		{
			c.Insert(new Multilanguage { TableName = table, ColumnId = id, ColumnName = column, Language = lang, ColumnValue = value, QStatus = 0 });
		}
		else if (row.ColumnValue != value)
		{
			c.Execute("update multilanguage set columnValue = @value where id = @id", new { value, id = row.Id });
		}
	}

	/// <summary>The translation of a content row in the editor's JSON shape (lists as arrays).</summary>
	public static JObject EntityValues(Dictionary<(int, string), string> tr, int id, IEnumerable<FieldDef> fields)
	{
		JObject o = new JObject();
		foreach (FieldDef f in fields.Where(f => f.Translatable))
		{
			if (tr.TryGetValue((id, f.Name.ToLowerInvariant()), out string v))
			{
				o[f.Name] = f.Kind == FieldKind.List ? (JToken)ContentStore.ParseArray(v) : v;
			}
		}
		return o;
	}

	/// <summary>Saves a translated content row: each translatable field on its own, the service page sections as one JSON.</summary>
	public static void SaveEntity(IDbConnection c, EntityDef def, FieldDef[] fields, int id, string lang, JObject posted)
	{
		FieldDef[] translatable = fields.Where(f => f.Translatable).ToArray();
		JObject clean = FieldValues.CleanTranslation(translatable, posted);
		foreach (FieldDef f in translatable)
		{
			JToken v = clean[f.Name];
			Save(c, def.Table, id, f.Name, lang, v == null ? null : v.Type == JTokenType.String ? (string)v : v.ToString(Formatting.None));
		}
		if (def.DataFields != null)
		{
			JObject data = FieldValues.CleanTranslation(def.DataFields, posted[DataColumn]);
			Save(c, def.Table, id, DataColumn, lang, data.HasValues ? data.ToString(Formatting.None) : null);
		}
	}

	/// <summary>
	/// The translation laid out like the Kazakh values, for the form: every Kazakh list row gets its
	/// translated row (matched by id), empty where there is none yet.
	/// </summary>
	public static JObject Align(IEnumerable<FieldDef> fields, JObject baseValues, JObject translation)
	{
		JObject o = new JObject();
		foreach (FieldDef f in fields.Where(f => f.Translatable))
		{
			JToken t = translation?[f.Name];
			if (f.Kind == FieldKind.List)
			{
				JArray trRows = t as JArray ?? new JArray();
				JArray rows = new JArray();
				int i = 0;
				foreach (JObject b in (baseValues?[f.Name] as JArray ?? new JArray()).OfType<JObject>())
				{
					string id = FieldValues.RowId(b);
					JObject match = trRows.OfType<JObject>().FirstOrDefault(r => id != null && FieldValues.RowId(r) == id)
						?? (i < trRows.Count && trRows[i] is JObject byIndex && FieldValues.RowId(byIndex) == null ? byIndex : null);
					JObject row = Align(f.Fields, b, match);
					if (id != null)
					{
						row["_id"] = id;
					}
					rows.Add(row);
					i++;
				}
				o[f.Name] = rows;
			}
			else if (t != null && t.Type == JTokenType.String)
			{
				o[f.Name] = (string)t;
			}
		}
		return o;
	}

	/// <summary>Percentage of the Kazakh texts that are translated (100 when there is nothing to translate).</summary>
	public static int Percent((int Done, int Total) c) => c.Total == 0 ? 100 : (int)Math.Floor(c.Done * 100.0 / c.Total);
}
