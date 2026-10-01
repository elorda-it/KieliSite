using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using COMMON;
using Dapper;
using DBHelper;
using MODEL.FormatModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using KieliWeb.Setup;
using KieliWeb.Setup.Cms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace KieliWeb.Controllers;

/// <summary>
/// The list / create / edit / delete screens of a content table, driven by its <see cref="EntityDef"/>.
/// Each admin controller maps its four actions (X, X [post], GetXList, SetXStatus) onto these,
/// so the menu permissions of /{controller}/{x}/list apply as everywhere in the admin.
/// </summary>
[Authorize(Roles = "Admin")]
public abstract class CmsController : QarBaseController
{
	protected CmsController(IMemoryCache memoryCache, IWebHostEnvironment environment)
		: base(memoryCache, environment)
	{
	}

	protected IActionResult CmsPage(EntityDef def, string query)
	{
		query = (query ?? string.Empty).Trim().ToLower();
		ViewData["query"] = query;
		ViewData["cms"] = true;
		ViewData["entity"] = def;
		using IDbConnection connection = Utilities.GetOpenConnection();
		switch (query)
		{
		case "list":
			ViewData["title"] = def.Title;
			if (def.Filter != null)
			{
				ViewData["filterOptions"] = def.Filter.Options(connection);
			}
			return View("~/Views/Console/Cms/List.cshtml");
		case "create":
		case "edit":
		{
			object row = null;
			if (query == "edit")
			{
				row = connection.Query(def.Model, $"select * from {def.Table} where qStatus = 0 and id = @id", new { id = GetIntQueryParam("id") }).FirstOrDefault();
				if (row == null)
				{
					return Redirect(def.ListUrl(CurrentLanguage));
				}
			}
			else if (!def.CanCreate)
			{
				return Redirect(def.ListUrl(CurrentLanguage));
			}
			FieldDef[] fields = def.Fields(connection);
			JObject values = FieldValues.FromEntity(row, fields);
			if (row == null)
			{
				Defaults(def, fields, values);
			}
			int rowId = row == null ? 0 : (int)def.Model.GetProperty("Id").GetValue(row);
			JObject data = def.DataFields == null ? null : row == null ? new JObject() : ContentStore.Parse((string)def.Model.GetProperty("DataJson").GetValue(row));
			// translations: only of a saved row of a translated table
			string lang = row != null && def.Translatable ? EditLanguage() : SiteLanguages.Base;
			List<LanguageTab> tabs = new List<LanguageTab>();
			JObject baseValues = null, baseData = null;
			if (row != null && def.Translatable)
			{
				Dictionary<string, (JObject Values, JObject Data)> translations = SiteLanguages.Translations.ToDictionary(l => l.Culture, l =>
				{
					Dictionary<(int, string), string> tr = TranslationStore.Load(connection, def.Table, new[] { rowId }, l.Culture);
					return (TranslationStore.EntityValues(tr, rowId, fields), tr.TryGetValue((rowId, TranslationStore.DataColumn), out string d) ? ContentStore.Parse(d) : new JObject());
				});
				tabs = Tabs(def.EditUrl(CurrentLanguage, rowId), lang, l =>
				{
					(int done, int total) = FieldValues.Coverage(fields, values, translations[l].Values);
					if (def.DataFields != null)
					{
						(int d2, int t2) = FieldValues.Coverage(def.DataFields, data, translations[l].Data);
						done += d2;
						total += t2;
					}
					return TranslationStore.Percent((done, total));
				});
				if (!SiteLanguages.IsBase(lang))
				{
					baseValues = values;
					baseData = data;
					values = TranslationStore.Align(fields, baseValues, translations[lang].Values);
					data = def.DataFields == null ? null : TranslationStore.Align(def.DataFields, baseData, translations[lang].Data);
				}
			}
			ViewData["title"] = def.Singular;
			string publicUrl = row == null || def.PublicUrl == null ? string.Empty : def.PublicUrl(connection, row);
			return View("~/Views/Console/Cms/Edit.cshtml", new CmsEditModel
			{
				Group = def.Group,
				ListTitle = def.Title,
				ListUrl = def.ListUrl(CurrentLanguage),
				Heading = (row == null ? def.Singular + " · жаңа" : def.Singular) + (SiteLanguages.IsBase(lang) ? string.Empty : " · " + SiteLanguages.Get(lang).Name),
				Help = def.Help,
				FormAction = $"/{CurrentLanguage}/{def.Controller}/{def.Action}",
				RowId = rowId,
				Fields = fields,
				Values = values,
				DataFields = def.DataFields,
				DataValues = data,
				DataTitle = "Беттің бөлімдері",
				PublicUrl = SiteLanguages.Localize(publicUrl, lang),
				Lang = lang,
				Tabs = tabs,
				BaseValues = baseValues,
				BaseDataValues = baseData
			});
		}
		default:
			return Redirect(def.ListUrl(CurrentLanguage));
		}
	}

	/// <summary>The language asked for in ?lang= (a known translation language), else Kazakh.</summary>
	protected string EditLanguage()
	{
		string lang = (Request.Query["lang"].ToString() ?? string.Empty).Trim().ToLowerInvariant();
		return SiteLanguages.Translations.Any(l => l.Culture == lang) ? lang : SiteLanguages.Base;
	}

	/// <summary>The language tabs of an edit form: Kazakh, then each translation with how much of it is done.</summary>
	protected static List<LanguageTab> Tabs(string editUrl, string active, Func<string, int> percent)
	{
		return SiteLanguages.All.Select(l => new LanguageTab(l.Culture, l.Name, SiteLanguages.IsBase(l.Culture) ? 100 : percent(l.Culture),
			SiteLanguages.IsBase(l.Culture) ? editUrl : editUrl + (editUrl.Contains('?') ? "&" : "?") + "lang=" + l.Culture, l.Culture == active)).ToList();
	}

	/// <summary>Small language badges for a list: green = translated, amber = in part, grey = not yet.</summary>
	protected static string TranslationBadges(Func<string, int> percent)
	{
		return string.Concat(SiteLanguages.Translations.Select(l =>
		{
			int p = percent(l.Culture);
			string css = p >= 100 ? "success" : p > 0 ? "warning" : "secondary";
			string code = l.Culture == "zh-cn" ? "ZH" : l.Culture.ToUpperInvariant();
			return $"<span class=\"badge bg-light-{css} text-{css} me-1\" title=\"{l.Name}: {p}%\">{code}</span>";
		}));
	}

	/// <summary>A new row starts published, in the filter the list was showing (?tab=3 …).</summary>
	private void Defaults(EntityDef def, FieldDef[] fields, JObject values)
	{
		foreach (FieldDef f in fields)
		{
			if (f.Kind == FieldKind.Bool && (f.Name == "isPublished"))
			{
				values[f.Name] = true;
			}
			if (f.Default != null)
			{
				values[f.Name] = f.Kind == FieldKind.Bool ? (JToken)(f.Default == "true") : f.Default;
			}
			if (def.Filter != null && f.Name.Equals(def.Filter.Column, StringComparison.OrdinalIgnoreCase))
			{
				string v = Request.Query[def.Filter.QueryKey];
				if (!string.IsNullOrEmpty(v))
				{
					values[f.Name] = v;
				}
			}
			if (f.Kind == FieldKind.Select && f.Required && string.IsNullOrEmpty((string)values[f.Name]) && f.Options.Length > 0)
			{
				values[f.Name] = f.Options[0].Value;
			}
		}
	}

	protected IActionResult CmsSave(EntityDef def, int id, string dataJson, string lang = null)
	{
		if (!string.IsNullOrEmpty(lang) && !SiteLanguages.IsBase(lang))
		{
			return CmsSaveTranslation(def, id, dataJson, lang.ToLowerInvariant());
		}
		JObject posted;
		try
		{
			posted = JObject.Parse(string.IsNullOrWhiteSpace(dataJson) ? "{}" : dataJson);
		}
		catch (JsonException)
		{
			return MessageHelper.RedirectAjax("Форма деректері оқылмады. Бетті жаңартып, қайталаңыз.", "error", "", null);
		}
		int now = UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
		using IDbConnection connection = Utilities.GetOpenConnection();
		FieldDef[] fields = def.Fields(connection);
		List<string> errors = new List<string>();
		JObject values = FieldValues.Clean(fields, posted, errors);
		JObject data = def.DataFields == null ? null : FieldValues.Clean(def.DataFields, posted["data"], errors);
		if (errors.Count > 0)
		{
			return MessageHelper.RedirectAjax(string.Join(" ", errors.Take(3)), "error", "", null);
		}
		bool isNew = id == 0;
		object row;
		if (isNew)
		{
			if (!def.CanCreate)
			{
				return MessageHelper.RedirectAjax(T("ls_Accessdenied"), "error", "", null);
			}
			row = Activator.CreateInstance(def.Model);
			// every text column is NOT NULL: start from empty strings
			foreach (var p in def.Model.GetProperties().Where(p => p.PropertyType == typeof(string) && p.CanWrite))
			{
				p.SetValue(row, string.Empty);
			}
			def.Model.GetProperty("AddTime")?.SetValue(row, now);
		}
		else
		{
			row = connection.Query(def.Model, $"select * from {def.Table} where qStatus = 0 and id = @id", new { id }).FirstOrDefault();
			if (row == null)
			{
				return MessageHelper.RedirectAjax(T("ls_Idoiiw"), "error", "", null);
			}
		}
		FieldValues.ToEntity(row, fields, values);
		if (data != null)
		{
			def.Model.GetProperty("DataJson").SetValue(row, data.ToString(Formatting.None));
		}
		def.Model.GetProperty("UpdateTime")?.SetValue(row, now);
		try
		{
			string problem = def.Prepare?.Invoke(connection, row, isNew);
			if (problem != null)
			{
				return MessageHelper.RedirectAjax(problem, "error", "", null);
			}
			if (isNew)
			{
				id = InsertRow(connection, def, row);
			}
			else
			{
				UpdateRow(connection, def, row);
			}
		}
		catch (Exception exception)
		{
			Log.Error(exception, "Cms save {Table}", def.Table);
			return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", null);
		}
		ContentStore.Clear(_memoryCache);
		return MessageHelper.RedirectAjax(isNew ? "Қосылды" : "Сақталды", "success", isNew ? def.EditUrl(CurrentLanguage, id) : string.Empty, new { id });
	}

	/// <summary>Saves the translation of a row into one language; the row itself is not changed.</summary>
	private IActionResult CmsSaveTranslation(EntityDef def, int id, string dataJson, string lang)
	{
		if (!def.Translatable || !SiteLanguages.Translations.Any(l => l.Culture == lang))
		{
			return MessageHelper.RedirectAjax(T("ls_Accessdenied"), "error", "", null);
		}
		JObject posted;
		try
		{
			posted = JObject.Parse(string.IsNullOrWhiteSpace(dataJson) ? "{}" : dataJson);
		}
		catch (JsonException)
		{
			return MessageHelper.RedirectAjax("Форма деректері оқылмады. Бетті жаңартып, қайталаңыз.", "error", "", null);
		}
		using IDbConnection connection = Utilities.GetOpenConnection();
		if (connection.ExecuteScalar<int>($"select count(1) from {def.Table} where qStatus = 0 and id = @id", new { id }) == 0)
		{
			return MessageHelper.RedirectAjax(T("ls_Idoiiw"), "error", "", null);
		}
		try
		{
			TranslationStore.SaveEntity(connection, def, def.Fields(connection), id, lang, posted);
		}
		catch (Exception exception)
		{
			Log.Error(exception, "Cms translation {Table} {Lang}", def.Table, lang);
			return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", null);
		}
		ContentStore.Clear(_memoryCache);
		return MessageHelper.RedirectAjax("Аударма сақталды — сайтта бірден көрінеді", "success", "", null);
	}

	private static int InsertRow(IDbConnection connection, EntityDef def, object row)
	{
		// SimpleCRUD's Insert<T> needs the static type
		var insert = typeof(SimpleCRUD).GetMethods().First(m => m.Name == "Insert" && m.IsGenericMethod && m.GetGenericArguments().Length == 1 && m.GetParameters().Length == 4);
		object result = insert.MakeGenericMethod(def.Model).Invoke(null, new object[] { connection, row, null, null });
		return Convert.ToInt32(result ?? 0);
	}

	private static void UpdateRow(IDbConnection connection, EntityDef def, object row)
	{
		var update = typeof(SimpleCRUD).GetMethods().First(m => m.Name == "Update" && m.IsGenericMethod && m.GetParameters().Length == 4);
		update.MakeGenericMethod(def.Model).Invoke(null, new object[] { connection, row, null, null });
	}

	protected IActionResult CmsList(EntityDef def, ApiUnifiedModel model)
	{
		int start = Math.Max(0, model.Start);
		int length = model.Length > 0 && model.Length <= 200 ? model.Length : 25;
		string keyword = (model.Keyword ?? string.Empty).Trim();
		using IDbConnection connection = Utilities.GetOpenConnection();
		DynamicParameters args = new DynamicParameters();
		string where = " from " + def.Table + " where qStatus = 0 ";
		if (keyword.Length > 0 && def.Search.Length > 0)
		{
			where += " and (" + string.Join(" or ", def.Search.Select(c => c + " like @keyword")) + ") ";
			args.Add("keyword", "%" + keyword + "%");
		}
		if (def.Filter != null && int.TryParse(GetStringQueryParam("filter"), out int filter) && filter > 0)
		{
			where += " and " + def.Filter.Column + " = @filter ";
			args.Add("filter", filter);
		}
		// DataTables column 0 is the checkbox; ordering only by the columns that allow it
		string order = def.DefaultOrder;
		DataTableOrderModel o = model.OrderList?.FirstOrDefault();
		if (o != null && o.Column >= 1 && o.Column <= def.Columns.Length && def.Columns[o.Column - 1].SortSql != null)
		{
			order = def.Columns[o.Column - 1].SortSql + (string.Equals(o.Dir, "desc", StringComparison.OrdinalIgnoreCase) ? " desc" : " asc") + ", id";
		}
		int total = connection.ExecuteScalar<int>("select count(1)" + where, args);
		List<object> rows = connection.Query(def.Model, "select *" + where + " order by " + order + $" limit {start}, {length}", args).ToList();
		CmsLookups lookups = CmsLookups.Load(connection);
		lookups.Counts.Clear();
		foreach (KeyValuePair<int, int> kv in Entities.Counts(connection, def))
		{
			lookups.Counts[kv.Key] = kv.Value;
		}
		// translation status of each row, per language
		FieldDef[] fields = def.Translatable ? def.Fields(connection) : null;
		List<int> ids = rows.Select(r => (int)def.Model.GetProperty("Id").GetValue(r)).ToList();
		Dictionary<string, Dictionary<(int, string), string>> translations = def.Translatable
			? SiteLanguages.Translations.ToDictionary(l => l.Culture, l => TranslationStore.Load(connection, def.Table, ids, l.Culture))
			: null;
		List<Dictionary<string, object>> dataList = rows.Select(r =>
		{
			int rowId = (int)def.Model.GetProperty("Id").GetValue(r);
			Dictionary<string, object> d = new Dictionary<string, object> { ["id"] = rowId };
			for (int i = 0; i < def.Columns.Length; i++)
			{
				d["c" + i] = def.Columns[i].Html(r, lookups);
			}
			d["url"] = def.PublicUrl?.Invoke(connection, r) ?? string.Empty;
			if (def.Translatable)
			{
				JObject baseValues = FieldValues.FromEntity(r, fields);
				JObject baseData = def.DataFields == null ? null : ContentStore.Parse((string)def.Model.GetProperty("DataJson").GetValue(r));
				d["tr"] = TranslationBadges(lang =>
				{
					Dictionary<(int, string), string> tr = translations[lang];
					(int done, int total) = FieldValues.Coverage(fields, baseValues, TranslationStore.EntityValues(tr, rowId, fields));
					if (def.DataFields != null)
					{
						(int d2, int t2) = FieldValues.Coverage(def.DataFields, baseData, tr.TryGetValue((rowId, TranslationStore.DataColumn), out string dj) ? ContentStore.Parse(dj) : new JObject());
						done += d2;
						total += t2;
					}
					return TranslationStore.Percent((done, total));
				});
			}
			return d;
		}).ToList();
		return MessageHelper.RedirectAjax(T("ls_Searchsuccessful"), "success", "", new { start, length, keyword, total, dataList });
	}

	protected IActionResult CmsDelete(EntityDef def, List<int> idList)
	{
		idList = (idList ?? new List<int>()).Where(x => x > 0).Distinct().ToList();
		if (idList.Count == 0)
		{
			return MessageHelper.RedirectAjax("Өшіретін жолдарды белгілеңіз.", "error", "", null);
		}
		using IDbConnection connection = Utilities.GetOpenConnection();
		string problem = def.DeleteGuard?.Invoke(connection, idList);
		if (problem != null)
		{
			return MessageHelper.RedirectAjax(problem, "error", "", null);
		}
		connection.Execute($"update {def.Table} set qStatus = 1, updateTime = @now where id in @idList", new { now = UnixTimeHelper.ConvertToUnixTime(DateTime.Now), idList });
		ContentStore.Clear(_memoryCache);
		return MessageHelper.RedirectAjax(T("ls_Deletedsuccessfully"), "success", "", null);
	}
}
