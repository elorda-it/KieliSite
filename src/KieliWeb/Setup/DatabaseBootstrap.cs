using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using COMMON;
using Dapper;
using DBHelper;
using MODEL;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace KieliWeb.Setup;

/// <summary>
/// First start on an empty database: creates the tables (db/kieli_schema.sql), the admin
/// menu, roles and the first administrator, and imports the initial content (db/seed/*.json).
/// Every step only fills what is missing, so it is safe on every start and never changes
/// rows an administrator has edited. The one exception is content that may not be used any
/// more (<see cref="ReplaceOldKaztest"/>), which is replaced once.
/// </summary>
public static class DatabaseBootstrap
{
	public static void Run(string dbDirectory, IConfiguration configuration, string contentRoot)
	{
		using IDbConnection connection = Utilities.GetOpenConnection();
		int now = UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
		RunSchema(connection, Path.Combine(dbDirectory, "kieli_schema.sql"));
		SeedFramework(connection, now, configuration, contentRoot);
		EnsureLanguages(connection);
		EnsureMenu(connection, now);
		SeedBlocks(connection, now);
		string seed = Path.Combine(dbDirectory, "seed");
		SeedServices(connection, now, seed);
		SeedArticles(connection, now, seed);
		SeedKazakhstan(connection, now, seed);
		AddMissingPlaces(connection, now, seed);
		SeedKaztest(connection, now, seed);
		ReplaceOldKaztest(connection, now, seed);
		AddMissingKaztestVariants(connection, now, seed);
		FillKaztestAudio(connection, now, seed);
		SeedServicePages(connection, now, seed);
		SeedNewsSources(connection, now, seed);
		EnsureRowIds(connection);
		SeedTranslations(connection, dbDirectory, now);
		ReplaceOldKaztestTexts(connection, dbDirectory);
		MergeUiTranslations(connection, dbDirectory);
		ApplySeedUpgrades(connection, dbDirectory);
	}

	// ---- languages, menu, translations ----------------------------------------------------

	/// <summary>The site languages (Setup/SiteLanguages.cs) exist in the «language» table; which are shown is set in the admin.</summary>
	private static void EnsureLanguages(IDbConnection connection)
	{
		HashSet<string> have = connection.Query<string>("select languageCulture from language where qStatus = 0").Select(x => x.ToLowerInvariant()).ToHashSet();
		foreach (SiteLanguage l in SiteLanguages.All.Where(l => !have.Contains(l.Culture)))
		{
			connection.Insert(SiteLanguages.Row(l));
		}
	}

	/// <summary>Menu items added to Setup/AdminMenu.cs after the first start: create them and give them to the roles.</summary>
	private static void EnsureMenu(IDbConnection connection, int now)
	{
		List<Navigation> nav = connection.GetList<Navigation>("where qStatus = 0").ToList();
		List<Role> roles = connection.GetList<Role>("where qStatus = 0 order by id").ToList();
		List<Permission> permissions = connection.GetList<Permission>("where qStatus = 0").ToList();
		if (nav.Count == 0 || roles.Count == 0)
		{
			return;
		}
		int order = nav.Where(n => n.ParentId == 0).Select(n => n.DisplayOrder).DefaultIfEmpty(0).Max();
		foreach (AdminMenu.Group group in AdminMenu.Groups)
		{
			Navigation parent = nav.FirstOrDefault(n => n.ParentId == 0 && n.NavTitle == group.Title);
			foreach (AdminMenu.Item item in group.Items.Where(i => !nav.Any(n => n.NavUrl.Equals(i.Url, StringComparison.OrdinalIgnoreCase))))
			{
				if (parent == null)
				{
					parent = new Navigation { NavigationTypeId = 1, ParentId = 0, NavTitle = group.Title, NavUrl = string.Empty, Target = string.Empty, HasIcon = 1, Icon = group.Icon, Description = string.Empty, AddTime = now, UpdateTime = now, DisplayOrder = ++order, QStatus = 0 };
					parent.Id = connection.Insert(parent).GetValueOrDefault();
					nav.Add(parent);
				}
				int childOrder = nav.Where(n => n.ParentId == parent.Id).Select(n => n.DisplayOrder).DefaultIfEmpty(0).Max() + 1;
				Navigation child = new Navigation { NavigationTypeId = 1, ParentId = parent.Id, NavTitle = item.Title, NavUrl = item.Url, Target = string.Empty, HasIcon = 0, Icon = string.Empty, Description = string.Empty, AddTime = now, UpdateTime = now, DisplayOrder = childOrder, NoChild = 1, QStatus = 0 };
				child.Id = connection.Insert(child).GetValueOrDefault();
				nav.Add(child);
				// the full-access role (the first one) always; the content editor role for content items
				foreach (Role role in roles.Where((r, i) => i == 0 || (group.ContentOnly && r.Name == "Контент редакторы")))
				{
					foreach (Permission permission in permissions)
					{
						Grant(connection, now, role.Id, permission.Id, child.Id);
					}
				}
			}
		}
	}

	/// <summary>List rows carry an id (r1, r2…) that their translations point to; older data gets them here.</summary>
	private static void EnsureRowIds(IDbConnection connection)
	{
		foreach (Pageblock row in connection.GetList<Pageblock>("where qStatus = 0").ToList())
		{
			BlockDef def = BlockRegistry.Find(row.BlockKey);
			JObject data = ContentStore.Parse(row.DataJson);
			if (def != null && TranslationFiles.AddRowIds(def.Fields, data))
			{
				connection.Execute("update pageblock set dataJson = @json where id = @id", new { json = data.ToString(Formatting.None), id = row.Id });
			}
		}
		foreach (Servicepage page in connection.GetList<Servicepage>("where qStatus = 0").ToList())
		{
			JObject data = ContentStore.Parse(page.DataJson);
			if (TranslationFiles.AddRowIds(BlockRegistry.ServicePageFields, data))
			{
				connection.Execute("update servicepage set dataJson = @json where id = @id", new { json = data.ToString(Formatting.None), id = page.Id });
			}
		}
		foreach ((string table, string column, string field) in new[] { ("servicetab", "stepsJson", "text"), ("serviceitem", "bringJson", "text"), ("place", "factsJson", "label") })
		{
			foreach ((int id, string json) in connection.Query<(int, string)>($"select id, {column} from {table} where qStatus = 0").ToList())
			{
				FieldDef list = new FieldDef("rows", "rows", FieldKind.List) { Fields = new[] { new FieldDef(field, field, FieldKind.Text) } };
				JObject wrap = new JObject { ["rows"] = ContentStore.ParseArray(json) };
				if (TranslationFiles.AddRowIds(new[] { list }, wrap))
				{
					connection.Execute($"update {table} set {column} = @json where id = @id", new { json = wrap["rows"].ToString(Formatting.None), id });
				}
			}
		}
	}

	/// <summary>Draft translations shipped in db/seed/i18n/*.json, for texts that have no translation yet.</summary>
	private static void SeedTranslations(IDbConnection connection, string dbDirectory, int now)
	{
		foreach (SiteLanguage l in SiteLanguages.Translations)
		{
			string path = TranslationFiles.FilePath(dbDirectory, l.Culture);
			if (!File.Exists(path))
			{
				continue;
			}
			int added = TranslationFiles.Import(connection, l.Culture, JObject.Parse(File.ReadAllText(path)), now);
			if (added > 0)
			{
				Log.Information("KieliSite: {Count} {Lang} translations added from {Path}", added, l.Culture, path);
			}
		}
	}

	/// <summary>The draft translations of db/seed/i18n, by language (languages without a file are left out).</summary>
	private static Dictionary<string, JObject> TranslationSeeds(string dbDirectory) => SiteLanguages.Translations
		.Select(l => (l.Culture, Path: TranslationFiles.FilePath(dbDirectory, l.Culture)))
		.Where(x => File.Exists(x.Path))
		.ToDictionary(x => x.Culture, x => JObject.Parse(File.ReadAllText(x.Path)));

	/// <summary>
	/// Texts of the ҚАЗТЕСТ pages written for the old test (audio "played from probtest.testcenter.kz", the FAQ on
	/// listening answers not yet scored, the home page sample question from that test) get the wording of
	/// db/seed, in Kazakh and in every translation. Only while they still have the old text.
	/// </summary>
	private static void ReplaceOldKaztestTexts(IDbConnection connection, string dbDirectory)
	{
		Dictionary<string, Pageblock> blocks = connection.GetList<Pageblock>("where qStatus = 0 and blockKey in ('kaztest.list', 'kaztest.faq', 'home.kaztest')")
			.GroupBy(b => b.BlockKey).ToDictionary(g => g.Key, g => g.First());
		Dictionary<string, JObject> files = TranslationSeeds(dbDirectory);
		using IDbTransaction transaction = connection.BeginTransaction();
		int changed = 0;
		if (blocks.TryGetValue("kaztest.list", out Pageblock list))
		{
			JObject data = ContentStore.Parse(list.DataJson);
			if (((string)data["note"] ?? string.Empty).Contains("testcenter.kz"))
			{
				data["note"] = ContentStore.DefaultBlock("kaztest.list")["note"];
				SaveBlock(connection, list, data);
				EachTranslation(connection, list, (lang, tr) => tr["note"] = files.TryGetValue(lang, out JObject f) ? f["blocks"]?["kaztest.list"]?["note"] : null);
				changed++;
			}
		}
		if (blocks.TryGetValue("kaztest.faq", out Pageblock faq))
		{
			JObject data = ContentStore.Parse(faq.DataJson);
			JObject item = (data["items"] as JArray)?.OfType<JObject>().FirstOrDefault(r => (string)r["_id"] == "r5" && (string)r["q"] == "Неге кейбір тыңдалым сұрақтары есептелмейді?");
			JObject fresh = (ContentStore.DefaultBlock("kaztest.faq")["items"] as JArray)?.OfType<JObject>().FirstOrDefault(r => (string)r["_id"] == "r5");
			if (item != null && fresh != null)
			{
				item["q"] = fresh["q"];
				item["a"] = fresh["a"];
				SaveBlock(connection, faq, data);
				EachTranslation(connection, faq, (lang, tr) =>
				{
					// the seed files list rows by position: r5 is the fifth question
					JObject t = files.TryGetValue(lang, out JObject f) ? (f["blocks"]?["kaztest.faq"]?["items"] as JArray)?.ElementAtOrDefault(4) as JObject : null;
					JArray rows = tr["items"] as JArray ?? new JArray();
					rows.OfType<JObject>().Where(r => (string)r["_id"] == "r5").ToList().ForEach(r => r.Remove());
					if (t != null)
					{
						rows.Add(new JObject { ["_id"] = "r5", ["q"] = t["q"], ["a"] = t["a"] });
					}
					tr["items"] = rows;
				});
				changed++;
			}
		}
		if (blocks.TryGetValue("home.kaztest", out Pageblock home))
		{
			JObject data = ContentStore.Parse(home.DataJson);
			if ((string)data["mockQuestion"] == "Қамзолға не тігіледі?")
			{
				JObject fresh = ContentStore.DefaultBlock("home.kaztest");
				foreach (string name in new[] { "mockVariant", "mockSection", "mockTimer", "mockKicker", "mockQuestion", "mockOptions", "mockSelected" })
				{
					data[name] = fresh[name];
				}
				SaveBlock(connection, home, data);
				changed++;
			}
		}
		transaction.Commit();
		if (changed > 0)
		{
			Log.Information("KieliSite: {Count} ҚАЗТЕСТ page blocks updated for the new practice tests", changed);
		}
	}

	private static void SaveBlock(IDbConnection connection, Pageblock row, JObject data) =>
		connection.Execute("update pageblock set dataJson = @json, updateTime = @now where id = @id",
			new { json = data.ToString(Formatting.None), now = UnixTimeHelper.ConvertToUnixTime(DateTime.Now), id = row.Id });

	/// <summary>Runs <paramref name="change"/> on every translation of a page block and saves the result.</summary>
	private static void EachTranslation(IDbConnection connection, Pageblock row, Action<string, JObject> change)
	{
		foreach (Multilanguage tr in connection.Query<Multilanguage>("select * from multilanguage where qStatus = 0 and tableName = 'pageblock' and columnId = @id and columnName = 'dataJson'", new { id = row.Id }).ToList())
		{
			JObject value = ContentStore.Parse(tr.ColumnValue);
			change(tr.Language.ToLowerInvariant(), value);
			foreach (JProperty empty in value.Properties().Where(x => x.Value.Type == JTokenType.Null).ToList())
			{
				empty.Remove();
			}
			connection.Execute("update multilanguage set columnValue = @value where id = @id", new { value = value.ToString(Formatting.None), id = tr.Id });
		}
	}

	/// <summary>
	/// Interface texts (ui.* blocks) added after a language was imported: their draft translations are added to
	/// the existing translation. Texts that are already translated are never changed.
	/// </summary>
	private static void MergeUiTranslations(IDbConnection connection, string dbDirectory)
	{
		Dictionary<string, int> blocks = connection.Query<(string, int)>("select blockKey, id from pageblock where qStatus = 0 and blockKey like 'ui.%'")
			.GroupBy(x => x.Item1).ToDictionary(g => g.Key, g => g.First().Item2);
		int added = 0;
		foreach ((string lang, JObject file) in TranslationSeeds(dbDirectory))
		{
			foreach (JProperty block in (file["blocks"] as JObject ?? new JObject()).Properties().Where(b => b.Name.StartsWith("ui.") && b.Value is JObject))
			{
				if (!blocks.TryGetValue(block.Name, out int id))
				{
					continue;
				}
				Multilanguage tr = connection.Query<Multilanguage>("select * from multilanguage where qStatus = 0 and tableName = 'pageblock' and columnId = @id and columnName = 'dataJson' and language = @lang",
					new { id, lang }).FirstOrDefault();
				if (tr == null)
				{
					continue;   // a block without any translation is imported whole by SeedTranslations
				}
				JObject value = ContentStore.Parse(tr.ColumnValue);
				List<JProperty> missing = ((JObject)block.Value).Properties().Where(k => k.Value.Type == JTokenType.String && value[k.Name] == null).ToList();
				if (missing.Count == 0)
				{
					continue;
				}
				missing.ForEach(k => value[k.Name] = k.Value);
				connection.Execute("update multilanguage set columnValue = @value where id = @id", new { value = value.ToString(Formatting.None), id = tr.Id });
				added += missing.Count;
			}
		}
		if (added > 0)
		{
			Log.Information("KieliSite: {Count} new interface texts translated from db/seed/i18n", added);
		}
	}

	/// <summary>
	/// Seed texts that changed after a site was installed (db/seed/upgrades.json: the kieli.kz name, page titles and
	/// descriptions…). A text that still has its old seed value — nobody edited it in the admin — gets the current
	/// seed value, in Kazakh (blocks.json) and in each translation (i18n/*.json).
	/// </summary>
	private static void ApplySeedUpgrades(IDbConnection connection, string dbDirectory)
	{
		string file = Path.Combine(dbDirectory, "seed", "upgrades.json");
		if (!File.Exists(file))
		{
			return;
		}
		JObject upgrades = JObject.Parse(File.ReadAllText(file));
		Dictionary<string, JObject> seeds = TranslationSeeds(dbDirectory);
		Dictionary<string, Pageblock> blocks = connection.GetList<Pageblock>("where qStatus = 0").GroupBy(b => b.BlockKey).ToDictionary(g => g.Key, g => g.First());
		int changed = 0;
		using IDbTransaction transaction = connection.BeginTransaction();
		foreach (JObject u in (upgrades["blocks"] as JArray ?? new JArray()).OfType<JObject>())
		{
			string key = (string)u["key"], list = (string)u["list"], item = (string)u["item"], field = (string)u["field"], lang = (string)u["lang"], from = (string)u["from"] ?? string.Empty;
			if (!blocks.TryGetValue(key, out Pageblock row))
			{
				continue;
			}
			JObject defaults = ContentStore.DefaultBlock(key);
			// the row a list field belongs to: same _id in the stored data, same position in the seed files
			int position = list == null ? -1 : (defaults[list] as JArray ?? new JArray()).OfType<JObject>().ToList().FindIndex(r => (string)r["_id"] == item);
			JObject Target(JObject data) => list == null ? data : (data[list] as JArray)?.OfType<JObject>().FirstOrDefault(r => (string)r["_id"] == item);
			if (SiteLanguages.IsBase(lang))
			{
				JObject data = ContentStore.Parse(row.DataJson);
				JObject target = Target(data);
				JToken fresh = list == null ? defaults[field] : position >= 0 ? defaults[list][position]?[field] : null;
				if (target != null && ((string)target[field] ?? string.Empty) == from && fresh != null)
				{
					target[field] = fresh;
					row.DataJson = data.ToString(Formatting.None);
					SaveBlock(connection, row, data);
					changed++;
				}
				continue;
			}
			Multilanguage tr = connection.Query<Multilanguage>("select * from multilanguage where qStatus = 0 and tableName = 'pageblock' and columnId = @id and columnName = 'dataJson' and language = @lang",
				new { id = row.Id, lang }).FirstOrDefault();
			if (tr == null)
			{
				continue;
			}
			JObject value = ContentStore.Parse(tr.ColumnValue);
			JObject trTarget = Target(value);
			if (trTarget == null || ((string)trTarget[field] ?? string.Empty) != from)
			{
				continue;
			}
			JToken seedBlock = seeds.TryGetValue(lang, out JObject seedFile) ? seedFile["blocks"]?[key] : null;
			JToken seedValue = list == null ? seedBlock?[field] : position >= 0 ? (seedBlock?[list] as JArray)?.ElementAtOrDefault(position)?[field] : null;
			if (seedValue == null || seedValue.Type == JTokenType.Null)
			{
				trTarget.Remove(field);   // no translation any more: the Kazakh text is shown
			}
			else
			{
				trTarget[field] = seedValue;
			}
			connection.Execute("update multilanguage set columnValue = @value where id = @id", new { value = value.ToString(Formatting.None), id = tr.Id });
			changed++;
		}
		// menu icons the admin's icon font does not have (the menu is kept in the navigation table)
		foreach (JObject u in (upgrades["navigation"] as JArray ?? new JArray()).OfType<JObject>())
		{
			changed += connection.Execute("update navigation set icon = @to where icon = @from", new { to = (string)u["to"], from = (string)u["from"] });
		}
		foreach (JObject u in (upgrades["settings"] as JArray ?? new JArray()).OfType<JObject>())
		{
			if ((string)u["field"] == "title")
			{
				changed += connection.Execute("update sitesetting set title = @to where title = @from", new { to = (string)u["to"], from = (string)u["from"] });
			}
		}
		transaction.Commit();
		if (changed > 0)
		{
			Log.Information("KieliSite: {Count} texts updated to the current db/seed values (db/seed/upgrades.json)", changed);
		}
	}

	private static void RunSchema(IDbConnection connection, string path)
	{
		string sql = string.Join("\n", File.ReadAllLines(path).Where(l => !l.TrimStart().StartsWith("--")));
		List<string> statements = sql.Split(";\n").Select(s => s.Trim().TrimEnd(';')).Where(s => s.Length > 0).ToList();
		// before anything is created or changed: the database is empty, or one this program made
		StopOnForeignTables(connection, statements);
		foreach (string statement in statements)
		{
			connection.Execute(statement);
		}
		AddNewColumns(connection, statements);
	}

	/// <summary>The tables of db/kieli_schema.sql with their column names (lower case) and CREATE statement.</summary>
	private static IEnumerable<(string Table, List<string> Columns, string Statement)> SchemaTables(List<string> statements)
	{
		foreach (string statement in statements)
		{
			System.Text.RegularExpressions.Match table = System.Text.RegularExpressions.Regex.Match(statement, @"CREATE TABLE IF NOT EXISTS `(\w+)`");
			if (table.Success)
			{
				yield return (table.Groups[1].Value, System.Text.RegularExpressions.Regex.Matches(statement, @"^\s+`(\w+)`", System.Text.RegularExpressions.RegexOptions.Multiline)
					.Select(m => m.Groups[1].Value.ToLowerInvariant()).ToList(), statement);
			}
		}
	}

	/// <summary>The columns of every table already in the database: name, nullable, default, extra (auto_increment).</summary>
	private static Dictionary<string, List<(string Column, string Nullable, string Default, string Extra)>> ExistingColumns(IDbConnection connection) => connection
		.Query<(string Table, string Column, string Nullable, string Default, string Extra)>("select table_name, column_name, is_nullable, column_default, extra from information_schema.columns where table_schema = database()")
		.GroupBy(x => x.Table.ToLowerInvariant())
		.ToDictionary(g => g.Key, g => g.Select(x => (x.Column, x.Nullable, x.Default, x.Extra)).ToList());

	/// <summary>
	/// CREATE TABLE IF NOT EXISTS leaves an existing table alone, so a table of the same name made by another program (the old
	/// kieli.kz site's «region» with its required «regionName») would break the first inserts. Such a table is recognised before
	/// anything is changed — more than half of our columns are missing, or it has a required column (NOT NULL, no default) this
	/// program does not know — and the start stops with the list; the database is left as it was.
	/// </summary>
	private static void StopOnForeignTables(IDbConnection connection, List<string> statements)
	{
		Dictionary<string, List<(string Column, string Nullable, string Default, string Extra)>> existing = ExistingColumns(connection);
		List<string> problems = new List<string>();
		foreach ((string table, List<string> columns, string _) in SchemaTables(statements))
		{
			if (!existing.TryGetValue(table.ToLowerInvariant(), out var have))
			{
				continue;
			}
			HashSet<string> names = have.Select(c => c.Column.ToLowerInvariant()).ToHashSet();
			List<string> missing = columns.Where(c => !names.Contains(c)).ToList();
			List<string> required = have.Where(c => !columns.Contains(c.Column.ToLowerInvariant()) && c.Nullable == "NO" && c.Default == null
				&& !(c.Extra ?? string.Empty).Contains("auto_increment", StringComparison.OrdinalIgnoreCase)).Select(c => c.Column).ToList();
			if (missing.Count * 2 > columns.Count)
			{
				problems.Add(table + " (missing " + string.Join(", ", missing) + ")");
			}
			else if (required.Count > 0)
			{
				problems.Add(table + " (columns of another program: " + string.Join(", ", required) + ")");
			}
		}
		if (problems.Count > 0)
		{
			throw new InvalidOperationException("KieliSite: this database already has tables of another program, so nothing was changed in it. "
				+ "Use an empty database (README «连接 kieli_db»). Tables: " + string.Join("; ", problems));
		}
	}

	/// <summary>A column added to db/kieli_schema.sql in a later version is added to the existing table, with its definition from the file.</summary>
	private static void AddNewColumns(IDbConnection connection, List<string> statements)
	{
		Dictionary<string, List<(string Column, string Nullable, string Default, string Extra)>> existing = ExistingColumns(connection);
		foreach ((string table, List<string> columns, string statement) in SchemaTables(statements))
		{
			if (!existing.TryGetValue(table.ToLowerInvariant(), out var have))
			{
				continue;
			}
			HashSet<string> names = have.Select(c => c.Column.ToLowerInvariant()).ToHashSet();
			foreach (string column in columns.Where(c => !names.Contains(c)))
			{
				System.Text.RegularExpressions.Match line = System.Text.RegularExpressions.Regex.Match(statement, @"^\s+(`" + column + @"`[^\n]*?),?\s*$",
					System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
				if (line.Success)
				{
					connection.Execute("ALTER TABLE `" + table + "` ADD COLUMN " + line.Groups[1].Value.TrimEnd(','));
					Log.Information("KieliSite: column {Column} added to {Table}", column, table);
				}
			}
		}
	}

	private static bool Empty<T>(IDbConnection connection) => connection.RecordCount<T>(string.Empty) == 0;

	// ---- admins, roles, permissions, admin menu, language, site settings ----------------

	private static void SeedFramework(IDbConnection connection, int now, IConfiguration configuration, string contentRoot)
	{
		if (Empty<Language>(connection))
		{
			connection.Insert(new Language
			{
				ShortName = "Қаз",
				FullName = "Қазақша",
				LanguageCulture = "kz",
				UniqueSeoCode = "kk",
				ISOCode = "kk",
				LanguageFlagImageUrl = string.Empty,
				DisplayOrder = 1,
				IsSubLanguage = 0,
				IsDefault = 1,
				FrontendDisplay = 1,
				BackendDisplay = 1,
				QStatus = 0
			});
		}
		if (Empty<Permission>(connection))
		{
			foreach (string type in new[] { "view", "create", "edit", "delete" })
			{
				connection.Insert(new Permission
				{
					TableName = "Navigation",
					LocalKey = "ls_" + char.ToUpper(type[0]) + type.Substring(1),
					ManageType = type,
					AddTime = now,
					UpdateTime = now,
					QStatus = 0
				});
			}
		}
		if (Empty<Sitesetting>(connection))
		{
			connection.Insert(new Sitesetting
			{
				Title = "kieli.kz",
				Description = "«BASTAU LINE» ЖШС: Қазақстанға көшу, оқу, құжат аудармасы, нотариат, апостиль — қазақ, қытай және орыс тілдерінде.",
				Keywords = "BASTAU LINE, Омар Бекмұрат, Ата жолы, қандас, аударма, апостиль, ҚАЗТЕСТ, Киелі",
				LogoUrl = "/kieli/img/logo.svg",
				DarkLogo = "/kieli/img/logo-wordmark.svg",
				LightLogo = "/kieli/img/logo-wordmark-light.svg",
				MobileLogoUrl = "/kieli/img/logo.svg",
				Favicon = "/kieli/img/favicon.svg",
				AdminLogoUrl = "/kieli/img/logo-wordmark.svg",
				Email = "info@kieli.kz",
				Phone = "+7 700 000 00 00",
				Address = "Астана қ., Айнакөл көшесі, 66",
				Copyright = "© BASTAU LINE",
				MaxErrorCount = 5,
				QStatus = 0
			});
		}
		if (Empty<Navigation>(connection))
		{
			int order = 1;
			foreach (AdminMenu.Group group in AdminMenu.Groups)
			{
				int parentId = connection.Insert(new Navigation
				{
					NavigationTypeId = 1,
					ParentId = 0,
					NavTitle = group.Title,
					NavUrl = string.Empty,
					Target = string.Empty,
					HasIcon = 1,
					Icon = group.Icon,
					Description = string.Empty,
					AddTime = now,
					UpdateTime = now,
					DisplayOrder = order++,
					QStatus = 0
				}).GetValueOrDefault();
				int child = 1;
				foreach (AdminMenu.Item item in group.Items)
				{
					connection.Insert(new Navigation
					{
						NavigationTypeId = 1,
						ParentId = parentId,
						NavTitle = item.Title,
						NavUrl = item.Url,
						Target = string.Empty,
						HasIcon = 0,
						Icon = string.Empty,
						Description = string.Empty,
						AddTime = now,
						UpdateTime = now,
						DisplayOrder = child++,
						NoChild = 1,
						QStatus = 0
					});
				}
			}
		}
		if (Empty<Role>(connection))
		{
			List<Permission> permissions = connection.GetList<Permission>("where qStatus = 0").ToList();
			List<Navigation> navigation = connection.GetList<Navigation>("where qStatus = 0 and parentId > 0").ToList();
			List<string> contentUrls = AdminMenu.Groups.Where(g => g.ContentOnly).SelectMany(g => g.Items).Select(i => i.Url).ToList();
			int adminRole = InsertRole(connection, now, "Әкімші", "Сайттың барлық бөлімі, әкімшілер мен баптаулар");
			int editorRole = InsertRole(connection, now, "Контент редакторы", "Беттер, қызметтер, жаңалықтар, Қазақстан, ҚАЗТЕСТ, өтінімдер");
			foreach (Navigation nav in navigation)
			{
				foreach (Permission permission in permissions)
				{
					Grant(connection, now, adminRole, permission.Id, nav.Id);
					if (contentUrls.Contains(nav.NavUrl))
					{
						Grant(connection, now, editorRole, permission.Id, nav.Id);
					}
				}
			}
		}
		if (Empty<Admin>(connection))
		{
			string email = (configuration["Site:InitialAdmin:Email"] ?? "admin@kieli.kz").Trim().ToLower();
			string password = configuration["Site:InitialAdmin:Password"];
			bool generated = string.IsNullOrWhiteSpace(password);
			if (generated)
			{
				password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12)).Replace("+", "x").Replace("/", "y").Replace("=", string.Empty);
				// Shown once, on the server only: sign in and change it under «Менің аккаунтым». Written before the account
				// exists: if the site may not write there, the start stops and no account is left that nobody can open.
				string file = Path.Combine(contentRoot, "logs", "initial-admin.txt");
				try
				{
					Directory.CreateDirectory(Path.GetDirectoryName(file));
					File.WriteAllText(file, $"KieliSite first administrator\nlogin: {email}\npassword: {password}\nChange the password after the first sign-in, then delete this file.\n");
				}
				catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
				{
					throw new InvalidOperationException("KieliSite: there is no administrator yet and Site:InitialAdmin:Password is empty, so a generated password "
						+ "has to be written to " + file + ", but the site may not write there. Set Site:InitialAdmin:Password in the configuration, "
						+ "or let the user that runs the site write to logs/. Nothing was created.", exception);
				}
				Log.Warning("KieliSite: the first administrator's generated password is in {File}", file);
			}
			int adminId = connection.Insert(new Admin
			{
				Email = email,
				Phone = string.Empty,
				Password = MD5Helper.PasswordMd5Encrypt(password),
				Name = "Әкімші",
				AvatarUrl = string.Empty,
				Description = string.Empty,
				IsSuper = 1,
				HiddenColumnJson = string.Empty,
				SkinName = "light",
				ReLogin = 0,
				AddTime = now,
				UpdateTime = now,
				QStatus = 0
			}).GetValueOrDefault();
			int roleId = connection.GetList<Role>("where qStatus = 0 order by id").First().Id;
			connection.Insert(new Adminrole { AdminId = adminId, RoleId = roleId, AddTime = now, UpdateTime = now, QStatus = 0 });
		}
	}

	private static int InsertRole(IDbConnection connection, int now, string name, string description)
	{
		return connection.Insert(new Role { Name = name, Description = description, AddTime = now, UpdateTime = now, QStatus = 0 }).GetValueOrDefault();
	}

	private static void Grant(IDbConnection connection, int now, int roleId, int permissionId, int navigationId)
	{
		connection.Insert(new Rolepermission
		{
			RoleId = roleId,
			PermissionId = permissionId,
			TableName = "Navigation",
			ColumnId = navigationId,
			AddTime = now,
			UpdateTime = now,
			QStatus = 0
		});
	}

	// ---- content ------------------------------------------------------------------------

	private static void SeedBlocks(IDbConnection connection, int now)
	{
		HashSet<string> existing = connection.Query<string>("select blockKey from pageblock").ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (BlockDef def in BlockRegistry.All.Where(b => !existing.Contains(b.Key)))
		{
			connection.Insert(new Pageblock
			{
				BlockKey = def.Key,
				DataJson = ContentStore.DefaultBlock(def.Key).ToString(Formatting.None),
				AddTime = now,
				UpdateTime = now,
				QStatus = 0
			});
		}
	}

	/// <summary>The feeds of the automatic news (db/seed/newssources.json): a feed that is not in the table yet is added;
	/// one deleted in the admin stays deleted (its row is still there).</summary>
	private static void SeedNewsSources(IDbConnection connection, int now, string seed)
	{
		JToken data = ReadSeed(seed, "newssources.json")?["items"]?["sources"];
		if (data == null)
		{
			return;
		}
		HashSet<string> have = connection.Query<string>("select feedUrl from newssource").Select(u => u.Trim().ToLowerInvariant()).ToHashSet();
		int category = connection.ExecuteScalar<int?>("select id from articlecategory where qStatus = 0 and slug = 'news' order by id limit 1") ?? 0;
		int order = connection.ExecuteScalar<int?>("select max(displayOrder) from newssource") ?? 0;
		foreach (JToken x in data)
		{
			string feed = ((string)x["feedUrl"] ?? string.Empty).Trim();
			if (feed.Length == 0 || have.Contains(feed.ToLowerInvariant()))
			{
				continue;
			}
			connection.Insert(new Newssource
			{
				Name = (string)x["name"],
				SiteUrl = (string)x["siteUrl"] ?? string.Empty,
				FeedUrl = feed,
				Keywords = (string)x["keywords"] ?? NewsCollector.DefaultKeywords,
				ExcludeWords = (string)x["excludeWords"] ?? string.Empty,
				CategoryId = category,
				AutoPublish = (byte)((bool?)x["autoPublish"] ?? true ? 1 : 0),
				MaxPerRun = (int?)x["maxPerRun"] ?? 5,
				IsEnabled = (byte)((bool?)x["enabled"] ?? true ? 1 : 0),
				Note = (string)x["note"] ?? string.Empty,
				LastCheckTime = 0,
				LastStatus = string.Empty,
				Etag = string.Empty,
				LastModified = string.Empty,
				AddedCount = 0,
				DisplayOrder = ++order,
				AddTime = now,
				UpdateTime = now,
				QStatus = 0
			});
		}
	}

	private static JObject ReadSeed(string seedDirectory, string name)
	{
		string path = Path.Combine(seedDirectory, name);
		return File.Exists(path) ? JObject.Parse("{\"items\":" + File.ReadAllText(path) + "}") : null;
	}

	private static string Rows(JToken list, string field = "text")
	{
		// plain string lists are stored as rows of one field, like every list in the admin
		JArray rows = new JArray((list as JArray ?? new JArray()).Select(x => x is JObject ? x : new JObject { [field] = x }));
		return rows.ToString(Formatting.None);
	}

	private static void SeedServices(IDbConnection connection, int now, string seed)
	{
		JToken data = ReadSeed(seed, "services.json")?["items"];
		if (data == null || !Empty<Servicetab>(connection))
		{
			return;
		}
		int tabOrder = 1;
		foreach (JToken t in data["tabs"])
		{
			int tabId = connection.Insert(new Servicetab
			{
				Slug = (string)t["slug"],
				Code = (string)t["code"],
				Name = (string)t["name"],
				Icon = (string)t["icon"] ?? string.Empty,
				Cta = (string)t["cta"] ?? string.Empty,
				Visual = (string)t["visual"] ?? "illo",
				StepsJson = Rows(t["steps"]),
				Footnote = (string)t["footnote"] ?? string.Empty,
				DisplayOrder = tabOrder++,
				AddTime = now,
				UpdateTime = now,
				QStatus = 0
			}).GetValueOrDefault();
			int itemOrder = 1;
			foreach (JToken i in t["items"])
			{
				connection.Insert(new Serviceitem
				{
					TabId = tabId,
					Slug = (string)i["slug"],
					Code = (string)i["code"],
					Name = (string)i["name"],
					Icon = (string)i["icon"] ?? string.Empty,
					Title = (string)i["title"],
					Dir = (string)i["dir"] ?? string.Empty,
					Who = (string)i["who"] ?? string.Empty,
					BringJson = Rows(i["bring"]),
					Note = (string)i["note"] ?? string.Empty,
					LinkUrl = (string)i["linkUrl"] ?? string.Empty,
					LinkText = (string)i["linkText"] ?? string.Empty,
					Cta = (string)i["cta"] ?? string.Empty,
					DisplayOrder = itemOrder++,
					AddTime = now,
					UpdateTime = now,
					QStatus = 0
				});
			}
		}
	}

	private static int UnixDate(string isoDate, int fallback)
	{
		return DateTime.TryParse(isoDate, out DateTime date) ? UnixTimeHelper.ConvertToUnixTime(date) : fallback;
	}

	private static void SeedArticles(IDbConnection connection, int now, string seed)
	{
		JToken data = ReadSeed(seed, "articles.json")?["items"];
		if (data == null || !Empty<Article>(connection))
		{
			return;
		}
		Dictionary<string, int> categories = new Dictionary<string, int>();
		int order = 1;
		foreach (JToken c in data["categories"])
		{
			categories[(string)c["slug"]] = connection.Insert(new Articlecategory
			{
				Slug = (string)c["slug"],
				Name = (string)c["name"],
				DisplayOrder = order++,
				AddTime = now,
				UpdateTime = now,
				QStatus = 0
			}).GetValueOrDefault();
		}
		foreach (JToken a in data["articles"])
		{
			connection.Insert(new Article
			{
				CategoryId = categories.TryGetValue((string)a["category"], out int cat) ? cat : 0,
				Slug = (string)a["slug"],
				Title = (string)a["title"],
				Excerpt = (string)a["excerpt"] ?? string.Empty,
				BodyHtml = (string)a["bodyHtml"] ?? string.Empty,
				LinkUrl = (string)a["linkUrl"] ?? string.Empty,
				CoverImageUrl = (string)a["coverImageUrl"] ?? string.Empty,
				CoverTone = (string)a["coverTone"] ?? "sky",
				CoverIcon = (string)a["coverIcon"] ?? "i-doc",
				DateText = (string)a["dateText"] ?? string.Empty,
				PublishTime = UnixDate((string)a["publishDate"], now),
				ReadMinutes = (int?)a["readMinutes"] ?? 0,
				SourceName = (string)a["sourceName"] ?? string.Empty,
				SourceUrl = string.Empty,
				AuthorName = (string)a["authorName"] ?? string.Empty,
				ServiceKey = (string)a["serviceKey"] ?? string.Empty,
				IsImportant = (byte)((int?)a["isImportant"] ?? 0),
				IsPublished = 1,
				SeoDescription = (string)a["seoDescription"] ?? string.Empty,
				ViewCount = 0,
				DisplayOrder = 0,
				AddTime = now,
				UpdateTime = now,
				QStatus = 0
			});
		}
	}

	private static readonly Dictionary<string, string> CategoryPlural = new Dictionary<string, string>
	{
		["tabigat"] = "Табиғат",
		["kieli"] = "Киелі орындар",
		["tarih"] = "Тарихи орындар",
		["eskertkish"] = "Ескерткіштер"
	};

	/// <summary>A place of db/seed/kazakhstan.json as a row; its region and category are found by map id and slug.</summary>
	private static Place PlaceRow(JToken p, Dictionary<int, int> regionIds, Dictionary<string, int> categoryIds, int order, int now)
	{
		JArray facts = new JArray(((JArray)p["facts"] ?? new JArray()).Select(f => new JObject { ["label"] = f[0], ["value"] = f[1] }));
		JArray pano = new JArray(((JArray)p["pano"] ?? new JArray()).Select(u => new JObject { ["url"] = u }));
		return new Place
		{
			Slug = (string)p["slug"],
			RegionId = regionIds.TryGetValue((int)p["regionMapId"], out int region) ? region : 0,
			CategoryId = categoryIds.TryGetValue((string)p["category"], out int cat) ? cat : 0,
			Name = (string)p["name"],
			Fact = (string)p["fact"] ?? string.Empty,
			Lead = (string)p["lead"] ?? string.Empty,
			BodyHtml = (string)p["bodyHtml"] ?? string.Empty,
			FactsJson = facts.ToString(Formatting.None),
			ImageUrl = (string)p["imageUrl"] ?? string.Empty,
			PanoJson = pano.ToString(Formatting.None),
			Lat = (decimal?)p["lat"] ?? 0m,
			Lon = (decimal?)p["lon"] ?? 0m,
			LegacyId = (int?)p["legacyId"] ?? 0,
			IsFeatured = 0,
			DisplayOrder = order,
			AddTime = now,
			UpdateTime = now,
			QStatus = 0
		};
	}

	/// <summary>
	/// Places added to db/seed/kazakhstan.json after a site was installed (the 170 places of the old kieli.kz, 2026-10) are added
	/// to its database: every seed place whose old id or address is not in the table. A place deleted in the admin keeps its
	/// row, so it is not added again. Their translations come with the draft translations (SeedTranslations, by address).
	/// </summary>
	private static void AddMissingPlaces(IDbConnection connection, int now, string seed)
	{
		JToken data = ReadSeed(seed, "kazakhstan.json")?["items"];
		if (data == null || Empty<Place>(connection))
		{
			return;
		}
		HashSet<int> legacy = connection.Query<int>("select legacyId from place where legacyId > 0").ToHashSet();
		HashSet<string> slugs = connection.Query<string>("select slug from place").Select(x => x.ToLowerInvariant()).ToHashSet();
		Dictionary<int, int> regionIds = connection.Query<(int MapId, int Id)>("select mapId, id from region where qStatus = 0 order by id")
			.GroupBy(r => r.MapId).ToDictionary(g => g.Key, g => g.First().Id);
		Dictionary<string, int> categoryIds = connection.Query<(string Slug, int Id)>("select slug, id from placecategory where qStatus = 0 order by id")
			.GroupBy(c => c.Slug).ToDictionary(g => g.Key, g => g.First().Id);
		int order = connection.ExecuteScalar<int?>("select max(displayOrder) from place") ?? 0, added = 0;
		foreach (JToken p in data["places"])
		{
			int legacyId = (int?)p["legacyId"] ?? 0;
			string slug = ((string)p["slug"] ?? string.Empty).ToLowerInvariant();
			if (slug.Length == 0 || slugs.Contains(slug) || (legacyId > 0 && legacy.Contains(legacyId)))
			{
				continue;
			}
			connection.Insert(PlaceRow(p, regionIds, categoryIds, ++order, now));
			slugs.Add(slug);
			added++;
		}
		if (added > 0)
		{
			Log.Information("KieliSite: {Count} places added from db/seed/kazakhstan.json", added);
		}
	}

	private static void SeedKazakhstan(IDbConnection connection, int now, string seed)
	{
		JToken data = ReadSeed(seed, "kazakhstan.json")?["items"];
		if (data == null || !Empty<Place>(connection))
		{
			return;
		}
		Dictionary<int, int> regionIds = new Dictionary<int, int>();
		int order = 1;
		foreach (JToken r in data["regions"])
		{
			string name = (string)r["name"];
			string shortName = name.EndsWith(" облысы") && name != "Алматы облысы" ? name.Substring(0, name.Length - " облысы".Length) : name;
			regionIds[(int)r["mapId"]] = connection.Insert(new Region
			{
				MapId = (int)r["mapId"],
				Name = name,
				ShortName = shortName,
				PlaceCount = (int?)r["placeCount"] ?? 0,
				IsCity = (byte)((int?)r["isCity"] ?? 0),
				DisplayOrder = order++,
				AddTime = now,
				UpdateTime = now,
				QStatus = 0
			}).GetValueOrDefault();
		}
		Dictionary<string, int> categoryIds = new Dictionary<string, int>();
		order = 1;
		foreach (JToken c in data["categories"])
		{
			string slug = (string)c["slug"];
			categoryIds[slug] = connection.Insert(new Placecategory
			{
				Slug = slug,
				Name = (string)c["name"],
				PluralName = CategoryPlural.TryGetValue(slug, out string plural) ? plural : (string)c["name"],
				DisplayOrder = order++,
				AddTime = now,
				UpdateTime = now,
				QStatus = 0
			}).GetValueOrDefault();
		}
		order = 1;
		foreach (JToken p in data["places"])
		{
			connection.Insert(PlaceRow(p, regionIds, categoryIds, order++, now));
		}
		order = 1;
		foreach (JToken h in data["heritage"])
		{
			connection.Insert(new Heritage
			{
				Name = (string)h["name"],
				Kind = (string)h["kind"] ?? string.Empty,
				Year = (string)h["year"] ?? string.Empty,
				ImageUrl = (string)h["imageUrl"] ?? string.Empty,
				LinkUrl = string.Empty,
				DisplayOrder = order++,
				AddTime = now,
				UpdateTime = now,
				QStatus = 0
			});
		}
	}

	private static void SeedKaztest(IDbConnection connection, int now, string seed)
	{
		JToken data = ReadSeed(seed, "kaztest.json")?["items"];
		if (data == null || !Empty<Kaztestvariant>(connection))
		{
			return;
		}
		int order = 1;
		foreach (JToken t in data["variants"])
		{
			int variantId = connection.Insert(new Kaztestvariant
			{
				Title = (string)t["title"],
				IsPublished = 1,
				DisplayOrder = order++,
				AddTime = now,
				UpdateTime = now,
				QStatus = 0
			}).GetValueOrDefault();
			InsertKaztestContent(connection, variantId, t, now);
		}
	}

	/// <summary>Passages, listening recordings and questions of one variant of db/seed/kaztest.json.</summary>
	private static void InsertKaztestContent(IDbConnection connection, int variantId, JToken t, int now)
	{
		int passageNo = 1;
		foreach (JToken passage in t["passages"] ?? new JArray())
		{
			connection.Insert(new Kaztestpassage
			{
				VariantId = variantId,
				PassageNo = passageNo++,
				BodyText = string.Join("\n\n", passage.Select(x => (string)x)),
				AddTime = now,
				UpdateTime = now,
				QStatus = 0
			});
		}
		int audioNo = 1;
		foreach (JToken r in t["recordings"] ?? new JArray())
		{
			connection.Insert(new Kaztestaudio
			{
				VariantId = variantId,
				AudioNo = audioNo++,
				Title = (string)r["title"] ?? string.Empty,
				Voices = (string)r["voices"] ?? string.Empty,
				Script = (string)r["script"] ?? string.Empty,
				AudioUrl = (string)r["audio"] ?? string.Empty,
				Credit = (string)r["credit"] ?? string.Empty,
				CreditUrl = (string)r["creditUrl"] ?? string.Empty,
				AddTime = now,
				UpdateTime = now,
				QStatus = 0
			});
		}
		int sectionNo = 1;
		foreach (JToken section in t["sections"])
		{
			foreach (JToken q in section["questions"])
			{
				JArray opts = (JArray)q["opts"];
				int? p = (int?)q["p"];
				int? a = (int?)q["a"];
				connection.Insert(new Kaztestquestion
				{
					VariantId = variantId,
					SectionNo = sectionNo,
					SectionName = (string)section["name"],
					QuestionNo = (int)q["n"],
					QuestionText = (string)q["q"],
					SubText = (string)q["sub"] ?? string.Empty,
					OptionA = (string)opts.ElementAtOrDefault(0) ?? string.Empty,
					OptionB = (string)opts.ElementAtOrDefault(1) ?? string.Empty,
					OptionC = (string)opts.ElementAtOrDefault(2) ?? string.Empty,
					OptionD = (string)opts.ElementAtOrDefault(3) ?? string.Empty,
					Answer = (string)q["ans"] ?? string.Empty,
					AudioUrl = (string)q["audio"] ?? string.Empty,
					AudioNo = a.HasValue ? a.Value + 1 : 0,
					PassageNo = p.HasValue ? p.Value + 1 : 0,
					AddTime = now,
					UpdateTime = now,
					QStatus = 0
				});
			}
			sectionNo++;
		}
	}

	/// <summary>
	/// Variants added to db/seed/kaztest.json after the first import (3-нұсқа, 2026-10) are added to an existing
	/// database, after the others. A variant counts as present when a variant of that title or its first reading
	/// text is there, even deleted: a variant an administrator removed is not brought back.
	/// </summary>
	private static void AddMissingKaztestVariants(IDbConnection connection, int now, string seed)
	{
		JArray fresh = ReadSeed(seed, "kaztest.json")?["items"]?["variants"] as JArray;
		if (fresh == null || Empty<Kaztestvariant>(connection))
		{
			return;
		}
		static string Norm(string text) => (text ?? string.Empty).Replace("\r\n", "\n").Trim();
		HashSet<string> titles = connection.Query<string>("select title from kaztestvariant").Select(t => (t ?? string.Empty).Trim()).ToHashSet();
		HashSet<string> texts = connection.Query<string>("select bodyText from kaztestpassage where passageNo = 1").Select(Norm).ToHashSet();
		int order = connection.ExecuteScalar<int>("select coalesce(max(displayOrder), 0) from kaztestvariant");
		foreach (JToken t in fresh)
		{
			string title = ((string)t["title"] ?? string.Empty).Trim();
			string first = Norm(string.Join("\n\n", (t["passages"]?.FirstOrDefault() as JArray ?? new JArray()).Select(x => (string)x)));
			if (title.Length == 0 || titles.Contains(title) || texts.Contains(first))
			{
				continue;
			}
			int id = connection.Insert(new Kaztestvariant
			{
				Title = title,
				IsPublished = 1,
				DisplayOrder = ++order,
				AddTime = now,
				UpdateTime = now,
				QStatus = 0
			}).GetValueOrDefault();
			InsertKaztestContent(connection, id, t, now);
			titles.Add(title);
			Log.Information("KieliSite: ҚАЗТЕСТ variant {Title} added from db/seed/kaztest.json", title);
		}
	}

	/// <summary>
	/// Recordings of db/seed/kaztest.json that got their audio after they were imported (the generated
	/// listening audio of 2026-10): a recording still without audio, whose script is the seed's, gets it.
	/// </summary>
	private static void FillKaztestAudio(IDbConnection connection, int now, string seed)
	{
		JArray fresh = ReadSeed(seed, "kaztest.json")?["items"]?["variants"] as JArray;
		if (fresh == null)
		{
			return;
		}
		List<int> variants = connection.Query<int>("select id from kaztestvariant where qStatus = 0 order by displayOrder, id").ToList();
		int filled = 0;
		for (int i = 0; i < Math.Min(variants.Count, fresh.Count); i++)
		{
			JArray recordings = fresh[i]["recordings"] as JArray ?? new JArray();
			for (int j = 0; j < recordings.Count; j++)
			{
				string audio = (string)recordings[j]["audio"];
				if (string.IsNullOrWhiteSpace(audio))
				{
					continue;
				}
				filled += connection.Execute("update kaztestaudio set audioUrl = @audio, credit = @credit, creditUrl = @creditUrl, updateTime = @now " +
					"where qStatus = 0 and variantId = @variant and audioNo = @no and audioUrl = '' and replace(script, '\r', '') = @script",
					new
					{
						audio,
						credit = (string)recordings[j]["credit"] ?? string.Empty,
						creditUrl = (string)recordings[j]["creditUrl"] ?? string.Empty,
						now,
						variant = variants[i],
						no = j + 1,
						script = ((string)recordings[j]["script"] ?? string.Empty).Replace("\r", string.Empty)
					});
			}
		}
		if (filled > 0)
		{
			Log.Information("KieliSite: audio added to {Count} ҚАЗТЕСТ listening recordings", filled);
		}
	}

	/// <summary>
	/// The first practice tests were copied from the National Testing Center's trial test (audio played from
	/// probtest.testcenter.kz), which may not be reused. Every variant that still holds those questions gets
	/// BASTAU LINE's own test of db/seed/kaztest.json in the same order; the variant row (title, order,
	/// translations) stays. The old rows are deleted, not hidden: that text must not stay in the database.
	/// </summary>
	private static void ReplaceOldKaztest(IDbConnection connection, int now, string seed)
	{
		List<int> old = connection.Query<int>("select distinct variantId from kaztestquestion where audioUrl like '%testcenter.kz%'").ToList();
		JArray fresh = ReadSeed(seed, "kaztest.json")?["items"]?["variants"] as JArray;
		if (old.Count == 0 || fresh == null)
		{
			return;
		}
		List<Kaztestvariant> variants = connection.Query<Kaztestvariant>("select * from kaztestvariant where id in @old order by displayOrder, id", new { old }).ToList();
		using IDbTransaction transaction = connection.BeginTransaction();
		for (int i = 0; i < variants.Count; i++)
		{
			int id = variants[i].Id;
			connection.Execute("delete from kaztestquestion where variantId = @id", new { id });
			connection.Execute("delete from kaztestpassage where variantId = @id", new { id });
			connection.Execute("delete from kaztestaudio where variantId = @id", new { id });
			if (i < fresh.Count)
			{
				InsertKaztestContent(connection, id, fresh[i], now);
				connection.Execute("update kaztestvariant set updateTime = @now where id = @id", new { now, id });
			}
		}
		transaction.Commit();
		Log.Information("KieliSite: the ҚАЗТЕСТ questions taken from testcenter.kz were replaced in {Count} variants", variants.Count);
	}

	private static void SeedServicePages(IDbConnection connection, int now, string seed)
	{
		JToken data = ReadSeed(seed, "servicepages.json")?["items"];
		if (data == null || !Empty<Servicepage>(connection))
		{
			return;
		}
		int order = 1;
		foreach (JToken p in data)
		{
			connection.Insert(new Servicepage
			{
				Slug = (string)p["slug"],
				Title = (string)p["title"],
				SeoDescription = (string)p["seoDescription"] ?? string.Empty,
				DataJson = p["data"].ToString(Formatting.None),
				DisplayOrder = order++,
				AddTime = now,
				UpdateTime = now,
				QStatus = 0
			});
		}
	}
}
