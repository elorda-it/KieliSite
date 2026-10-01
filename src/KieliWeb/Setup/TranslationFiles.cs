using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using Dapper;
using MODEL;
using KieliWeb.Setup.Cms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KieliWeb.Setup;

/// <summary>
/// Translation files (db/seed/i18n/{lang}.json): every translatable text of the site, keyed by what it
/// belongs to (block key, service slug, place slug…). <see cref="Export"/> writes the Kazakh source;
/// <see cref="Import"/> fills translations that do not exist yet — texts edited in the admin are never replaced.
/// List rows are in the same order as the Kazakh rows.
/// </summary>
public static class TranslationFiles
{
	/// <summary>A content table, how its rows are named in the file, and which of its fields are translated.</summary>
	private sealed record Source(EntityDef Def, string Name, Func<IDbConnection, List<(int Id, string Key, object Row)>> Rows);

	private static List<(int, string, object)> RowsOf<T>(IDbConnection c, string sql, Func<T, string> key) =>
		c.Query<T>(sql).Select(r => ((int)typeof(T).GetProperty("Id").GetValue(r), key(r), (object)r)).ToList();

	private static IEnumerable<Source> Sources(IDbConnection c)
	{
		Dictionary<int, string> tabSlugs = c.Query<(int, string)>("select id, slug from servicetab where qStatus = 0").ToDictionary(x => x.Item1, x => x.Item2);
		yield return new Source(Entities.ServiceTab, "servicetab", k => RowsOf<Servicetab>(k, "select * from servicetab where qStatus = 0 order by displayOrder, id", r => r.Slug));
		yield return new Source(Entities.ServiceItem, "serviceitem", k => RowsOf<Serviceitem>(k, "select * from serviceitem where qStatus = 0 order by tabId, displayOrder, id",
			r => (tabSlugs.TryGetValue(r.TabId, out string t) ? t : "?") + ":" + r.Slug));
		yield return new Source(Entities.ServicePage, "servicepage", k => RowsOf<Servicepage>(k, "select * from servicepage where qStatus = 0 order by displayOrder, id", r => r.Slug));
		yield return new Source(Entities.ArticleCategory, "articlecategory", k => RowsOf<Articlecategory>(k, "select * from articlecategory where qStatus = 0 order by displayOrder, id", r => r.Slug));
		yield return new Source(Entities.Article, "article", k => RowsOf<Article>(k, "select * from article where qStatus = 0 order by publishTime desc, id", r => r.Slug));
		yield return new Source(Entities.Region, "region", k => RowsOf<Region>(k, "select * from region where qStatus = 0 order by displayOrder, id", r => r.MapId.ToString()));
		yield return new Source(Entities.PlaceCategory, "placecategory", k => RowsOf<Placecategory>(k, "select * from placecategory where qStatus = 0 order by displayOrder, id", r => r.Slug));
		yield return new Source(Entities.Place, "place", k => RowsOf<Place>(k, "select * from place where qStatus = 0 order by displayOrder, id", r => r.Slug));
		yield return new Source(Entities.Heritage, "heritage", k => RowsOf<Heritage>(k, "select * from heritage where qStatus = 0 order by displayOrder, id", r => r.Name));
		yield return new Source(Entities.KaztestVariant, "kaztestvariant", k => RowsOf<Kaztestvariant>(k, "select * from kaztestvariant where qStatus = 0 order by displayOrder, id", r => r.Title));
	}

	/// <summary>The Kazakh texts of a row, as the translation file holds them.</summary>
	private static JObject RowTexts(EntityDef def, FieldDef[] fields, object row)
	{
		JObject values = FieldValues.FromEntity(row, fields.Where(f => f.Translatable));
		JObject o = TextsOnly(fields, values);
		if (def.DataFields != null)
		{
			JObject data = TextsOnly(def.DataFields, ContentStore.Parse((string)def.Model.GetProperty("DataJson").GetValue(row)));
			if (data.HasValues)
			{
				o["data"] = data;
			}
		}
		return o;
	}

	/// <summary>Only the non-empty translatable texts; list rows without their ids (the order says which row it is).</summary>
	private static JObject TextsOnly(IEnumerable<FieldDef> fields, JObject values)
	{
		JObject o = new JObject();
		foreach (FieldDef f in fields.Where(f => f.Translatable))
		{
			JToken v = values?[f.Name];
			if (f.Kind == FieldKind.List)
			{
				JArray rows = new JArray((v as JArray ?? new JArray()).OfType<JObject>().Select(r => TextsOnly(f.Fields, r)));
				if (rows.Any(r => r.HasValues))
				{
					o[f.Name] = rows;
				}
			}
			else if (v != null && v.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)v))
			{
				o[f.Name] = (string)v;
			}
		}
		return o;
	}

	public static JObject Export(IDbConnection c)
	{
		JObject file = new JObject();
		JObject blocks = new JObject();
		Dictionary<string, Pageblock> rows = c.GetList<Pageblock>("where qStatus = 0").GroupBy(b => b.BlockKey).ToDictionary(g => g.Key, g => g.First());
		foreach (BlockDef def in BlockRegistry.All)
		{
			JObject values = ContentStore.DefaultBlock(def.Key);
			if (rows.TryGetValue(def.Key, out Pageblock row))
			{
				values.Merge(ContentStore.Parse(row.DataJson), new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
			}
			JObject texts = TextsOnly(def.Fields, values);
			if (texts.HasValues)
			{
				blocks[def.Key] = texts;
			}
		}
		file["blocks"] = blocks;
		foreach (Source s in Sources(c))
		{
			FieldDef[] fields = s.Def.Fields(c);
			JObject table = new JObject();
			foreach ((int _, string key, object row) in s.Rows(c))
			{
				JObject texts = RowTexts(s.Def, fields, row);
				if (texts.HasValues)
				{
					table[key] = texts;
				}
			}
			file[s.Name] = table;
		}
		return file;
	}

	// ---- import -----------------------------------------------------------------------

	/// <summary>Fills the translations of one language from its file; returns how many texts were added.</summary>
	public static int Import(IDbConnection c, string lang, JObject file, int now)
	{
		HashSet<(string, int, string)> existing = c.Query<(string, int, string)>(
				"select tableName, columnId, columnName from multilanguage where qStatus = 0 and language = @lang", new { lang })
			.Select(x => (x.Item1.ToLowerInvariant(), x.Item2, x.Item3.ToLowerInvariant())).ToHashSet();
		int added = 0;
		void Put(string table, int id, string column, string value)
		{
			if (string.IsNullOrWhiteSpace(value) || existing.Contains((table, id, column.ToLowerInvariant())))
			{
				return;
			}
			c.Insert(new Multilanguage { TableName = table, ColumnId = id, ColumnName = column, Language = lang, ColumnValue = value, QStatus = 0 });
			existing.Add((table, id, column.ToLowerInvariant()));
			added++;
		}

		Dictionary<string, Pageblock> blocks = c.GetList<Pageblock>("where qStatus = 0").GroupBy(b => b.BlockKey).ToDictionary(g => g.Key, g => g.First());
		foreach (JProperty p in (file["blocks"] as JObject ?? new JObject()).Properties())
		{
			BlockDef def = BlockRegistry.Find(p.Name);
			if (def == null || !blocks.TryGetValue(def.Key, out Pageblock row) || !(p.Value is JObject texts))
			{
				continue;
			}
			JObject values = ContentStore.DefaultBlock(def.Key);
			values.Merge(ContentStore.Parse(row.DataJson), new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
			JObject tr = FieldValues.CleanTranslation(def.Fields, WithRowIds(def.Fields, values, texts));
			if (tr.HasValues)
			{
				Put("pageblock", row.Id, "dataJson", tr.ToString(Formatting.None));
			}
		}

		foreach (Source s in Sources(c))
		{
			if (!(file[s.Name] is JObject table))
			{
				continue;
			}
			FieldDef[] fields = s.Def.Fields(c).Where(f => f.Translatable).ToArray();
			foreach ((int id, string key, object row) in s.Rows(c))
			{
				if (!(table[key] is JObject texts))
				{
					continue;
				}
				JObject values = FieldValues.FromEntity(row, fields);
				JObject tr = FieldValues.CleanTranslation(fields, WithRowIds(fields, values, texts));
				foreach (JProperty v in tr.Properties())
				{
					Put(s.Def.Table, id, v.Name, v.Value.Type == JTokenType.String ? (string)v.Value : v.Value.ToString(Formatting.None));
				}
				if (s.Def.DataFields != null && texts["data"] is JObject dataTexts)
				{
					JObject data = ContentStore.Parse((string)s.Def.Model.GetProperty("DataJson").GetValue(row));
					JObject dataTr = FieldValues.CleanTranslation(s.Def.DataFields, WithRowIds(s.Def.DataFields, data, dataTexts));
					if (dataTr.HasValues)
					{
						Put(s.Def.Table, id, "data", dataTr.ToString(Formatting.None));
					}
				}
			}
		}
		return added;
	}

	/// <summary>The file lists rows by position; give each translated row the id of the Kazakh row at that position.</summary>
	private static JObject WithRowIds(IEnumerable<FieldDef> fields, JObject baseValues, JObject texts)
	{
		JObject o = (JObject)texts.DeepClone();
		foreach (FieldDef f in fields.Where(f => f.Kind == FieldKind.List))
		{
			if (!(o[f.Name] is JArray trRows) || !(baseValues?[f.Name] is JArray baseRows))
			{
				continue;
			}
			for (int i = 0; i < trRows.Count && i < baseRows.Count; i++)
			{
				if (trRows[i] is JObject tr && baseRows[i] is JObject b)
				{
					JObject withIds = WithRowIds(f.Fields, b, tr);
					string id = FieldValues.RowId(b);
					if (id != null)
					{
						withIds["_id"] = id;
					}
					trRows[i] = withIds;
				}
			}
		}
		return o;
	}

	/// <summary>Every list row gets an id (r1, r2… by position) where it has none yet.</summary>
	public static bool AddRowIds(IEnumerable<FieldDef> fields, JObject values)
	{
		bool changed = false;
		foreach (FieldDef f in fields.Where(f => f.Kind == FieldKind.List))
		{
			if (!(values?[f.Name] is JArray rows))
			{
				continue;
			}
			HashSet<string> used = rows.OfType<JObject>().Select(FieldValues.RowId).Where(x => x != null).ToHashSet();
			for (int i = 0; i < rows.Count; i++)
			{
				if (rows[i] is JValue plain && plain.Type == JTokenType.String)
				{
					rows[i] = new JObject { [f.Fields.FirstOrDefault()?.Name ?? "text"] = plain };
					changed = true;
				}
				if (!(rows[i] is JObject row))
				{
					continue;
				}
				changed |= AddRowIds(f.Fields, row);
				if (FieldValues.RowId(row) == null)
				{
					string id = "r" + (i + 1);
					while (!used.Add(id))
					{
						id = FieldValues.NewRowId();
					}
					row["_id"] = id;
					changed = true;
				}
			}
		}
		return changed;
	}

	public static string FilePath(string dbDirectory, string lang) => Path.Combine(dbDirectory, "seed", "i18n", lang + ".json");
}
