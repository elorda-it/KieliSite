using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using MODEL;
using static KieliWeb.Setup.Cms.CmsText;

namespace KieliWeb.Setup.Cms;

/// <summary>
/// The content tables edited in the admin. Each one is a menu item (see Setup/AdminMenu.cs) whose
/// address /{Controller}/{Action}/list also carries the role permissions.
/// </summary>
public static class Entities
{
	private static FieldDef Order() => new FieldDef("displayOrder", "Реті", FieldKind.Number) { Help = "Кіші сан — жоғарыда. Бос қалса, соңына қосылады." };

	private static FieldDef Slug(string example) => new FieldDef("slug", "Сілтеме белгісі", FieldKind.Text)
	{
		NoTranslate = true,
		Help = "Латын әрпі, сан және сызықша, мыс. " + example + ". Бос қалса, атауынан жасалады. Жарияланғаннан кейін өзгертпеген жөн — ескі сілтемелер ашылмай қалады."
	};

	private static (string, string)[] Pick(IDbConnection c, string sql) => c.Query<(int, string)>(sql).Select(x => (x.Item1.ToString(), x.Item2)).ToArray();

	private static int Count(IDbConnection c, string sql, object p = null) => c.ExecuteScalar<int>(sql, p);

	/// <summary>Fills an empty slug from the title, checks its form and that no other row uses it.</summary>
	private static string EnsureSlug(IDbConnection c, string table, dynamic row, string title, string scopeSql = "", object scope = null)
	{
		string slug = ((string)row.Slug ?? string.Empty).Trim().ToLowerInvariant();
		bool generated = slug.Length == 0;
		if (generated)
		{
			slug = Slugify(title);
			if (slug.Length == 0)
			{
				slug = table + "-" + DateTime.Now.ToString("yyMMddHHmm");
			}
		}
		if (!IsSlug(slug))
		{
			return "«Сілтеме белгісі»: тек кіші латын әрпі, сан және сызықша (мыс. sharyn-shatqaly).";
		}
		string baseSlug = slug;
		for (int n = 2; ; n++)
		{
			var args = new DynamicParameters(scope);
			args.Add("slug", slug);
			args.Add("id", (int)row.Id);
			if (Count(c, $"select count(1) from {table} where qStatus = 0 and slug = @slug and id <> @id {scopeSql}", args) == 0)
			{
				break;
			}
			if (!generated)
			{
				return $"«{slug}» сілтеме белгісі бос емес — басқасын жазыңыз.";
			}
			slug = baseSlug + "-" + n;
		}
		row.Slug = slug;
		return null;
	}

	private static void EnsureOrder(IDbConnection c, string table, dynamic row, bool isNew, string scopeSql = "", object scope = null)
	{
		if (isNew && (int)row.DisplayOrder == 0)
		{
			row.DisplayOrder = Count(c, $"select coalesce(max(displayOrder), 0) + 1 from {table} where qStatus = 0 {scopeSql}", scope);
		}
	}

	private static string InUse(int count, string text) => count > 0 ? text : null;

	private static string Ids(List<int> ids) => string.Join(",", ids);

	// ---- «Қызметтер» -------------------------------------------------------------------

	public static readonly EntityDef ServiceTab = new EntityDef
	{
		Controller = "Catalog",
		Action = "Tab",
		Group = "Қызметтер",
		Title = "Қызмет бөлімдері",
		Singular = "Қызмет бөлімі",
		Help = "«Сізге қандай көмек керек?» қойындылары (басты бет пен «Қызметтер» беті) және «Кеңес алу» тізімінің топтары. Бөлімдегі жеке қызметтер — «Қызмет түрлері» мәзірінде.",
		Model = typeof(Servicetab),
		Fields = c => new[]
		{
			new FieldDef("name", "Атауы", FieldKind.Text) { Required = true },
			new FieldDef("code", "Өтінім кодының бастамасы", FieldKind.Text) { Required = true, NoTranslate = true, Help = "2–4 бас әріп, мыс. AUD. Қызмет кодтары осыдан басталады: AUD-01, AUD-02…" },
			Slug("translate"),
			new FieldDef("icon", "Белгіше", FieldKind.Icon),
			new FieldDef("visual", "Сипаттаманың сол жағы", FieldKind.Select) { Options = new[] { ("illo", "Белгіше суреті"), ("doc", "Құжат үлгісі (аударма үшін)") } },
			new FieldDef("cta", "Батырма мәтіні", FieldKind.Text) { Help = "Қызмет түрінде бөлек жазылмаса, осы мәтін шығады." },
			new FieldDef("steps", "«Қалай жүреді» қадамдары", FieldKind.List) { Column = "StepsJson", Fields = new[] { new FieldDef("text", "Қадам", FieldKind.Text) } },
			new FieldDef("footnote", "Төменгі ескерту", FieldKind.Textarea),
			Order()
		},
		Columns = new[]
		{
			new ColumnDef("Реті", (o, l) => ((Servicetab)o).DisplayOrder.ToString(), "displayOrder"),
			new ColumnDef("Атауы", (o, l) => $"<b>{E(((Servicetab)o).Name)}</b>", "name"),
			new ColumnDef("Код", (o, l) => $"<code>{E(((Servicetab)o).Code)}</code>", "code"),
			new ColumnDef("Қызмет түрлері", (o, l) => l.Counts.TryGetValue(((Servicetab)o).Id, out int n) ? n.ToString() : "0")
		},
		Search = new[] { "name", "code", "slug" },
		Prepare = (c, o, isNew) =>
		{
			Servicetab t = (Servicetab)o;
			t.Code = (t.Code ?? string.Empty).Trim().ToUpperInvariant();
			EnsureOrder(c, "servicetab", t, isNew);
			return EnsureSlug(c, "servicetab", t, t.Name);
		},
		DeleteGuard = (c, ids) => InUse(Count(c, $"select count(1) from serviceitem where qStatus = 0 and tabId in ({Ids(ids)})"),
			"Бөлімде қызмет түрлері бар. Алдымен оларды өшіріңіз не басқа бөлімге ауыстырыңыз."),
		PublicUrl = (c, o) => "/kz/services#s-" + ((Servicetab)o).Slug
	};

	public static readonly EntityDef ServiceItem = new EntityDef
	{
		Controller = "Catalog",
		Action = "Item",
		Group = "Қызметтер",
		Title = "Қызмет түрлері",
		Singular = "Қызмет түрі",
		Help = "Бөлімдегі жеке қызмет: қойындыдағы батырма, оның сипаттамасы және «Кеңес алу» тізіміндегі жол. Өтінім коды тапсырыс сілтемесінде де қолданылады (#order-бөлім-қызмет).",
		Model = typeof(Serviceitem),
		Fields = c => new[]
		{
			new FieldDef("tabId", "Бөлімі", FieldKind.Select) { Required = true, Options = Pick(c, "select id, name from servicetab where qStatus = 0 order by displayOrder, id") },
			new FieldDef("name", "Қысқа атауы (батырмада)", FieldKind.Text) { Required = true },
			new FieldDef("title", "Толық атауы (сипаттамада және өтінімде)", FieldKind.Text) { Required = true },
			new FieldDef("code", "Өтінім коды", FieldKind.Text) { NoTranslate = true, Help = "Бос қалса, бөлім кодынан жасалады, мыс. AUD-07." },
			Slug("license"),
			new FieldDef("icon", "Белгіше", FieldKind.Icon),
			new FieldDef("dir", "Бағыты", FieldKind.Text) { Help = "Қысқа жол, мыс. «中文 → Қаз / Рус»." },
			new FieldDef("who", "Кімге керек", FieldKind.Textarea),
			new FieldDef("bring", "Не дайындау керек", FieldKind.List) { Column = "BringJson", Fields = new[] { new FieldDef("text", "Құжат не әрекет", FieldKind.Text) } },
			new FieldDef("note", "Ескерту", FieldKind.Textarea),
			new FieldDef("cta", "Батырма мәтіні", FieldKind.Text) { Help = "Бос қалса, бөлімнің батырма мәтіні шығады." },
			new FieldDef("linkText", "Қосымша сілтеме мәтіні", FieldKind.Text),
			new FieldDef("linkUrl", "Қосымша сілтеме", FieldKind.Url),
			Order()
		},
		Columns = new[]
		{
			new ColumnDef("Бөлімі", (o, l) => E(CmsLookups.Name(l.ServiceTabs, ((Serviceitem)o).TabId)), "tabId"),
			new ColumnDef("Реті", (o, l) => ((Serviceitem)o).DisplayOrder.ToString(), "displayOrder"),
			new ColumnDef("Код", (o, l) => $"<code>{E(((Serviceitem)o).Code)}</code>", "code"),
			new ColumnDef("Атауы", (o, l) => $"<b>{E(((Serviceitem)o).Name)}</b><br><small class=\"text-muted\">{Short(((Serviceitem)o).Title)}</small>", "name")
		},
		Search = new[] { "name", "title", "code", "slug" },
		DefaultOrder = "tabId, displayOrder, id",
		Filter = new FilterDef("tabId", "Бөлімі", "tab", c => Pick(c, "select id, name from servicetab where qStatus = 0 order by displayOrder, id")),
		Prepare = (c, o, isNew) =>
		{
			Serviceitem i = (Serviceitem)o;
			string tabCode = c.ExecuteScalar<string>("select code from servicetab where qStatus = 0 and id = @id", new { id = i.TabId });
			if (tabCode == null)
			{
				return "Бөлімді таңдаңыз.";
			}
			EnsureOrder(c, "serviceitem", i, isNew, "and tabId = @tabId", new { tabId = i.TabId });
			if (string.IsNullOrWhiteSpace(i.Code))
			{
				int n = Count(c, "select count(1) from serviceitem where qStatus = 0 and tabId = @tabId and id <> @id", new { tabId = i.TabId, id = i.Id }) + 1;
				while (Count(c, "select count(1) from serviceitem where qStatus = 0 and code = @code and id <> @id", new { code = tabCode + "-" + n.ToString("00"), id = i.Id }) > 0)
				{
					n++;
				}
				i.Code = tabCode + "-" + n.ToString("00");
			}
			i.Code = i.Code.Trim().ToUpperInvariant();
			return EnsureSlug(c, "serviceitem", i, i.Name, "and tabId = @tabId", new { tabId = i.TabId });
		},
		PublicUrl = (c, o) =>
		{
			Serviceitem i = (Serviceitem)o;
			string tab = c.ExecuteScalar<string>("select slug from servicetab where id = @id", new { id = i.TabId });
			return "/kz/services#s-" + tab + "-" + i.Slug;
		}
	};

	public static readonly EntityDef ServicePage = new EntityDef
	{
		Controller = "Catalog",
		Action = "Page",
		Group = "Қызметтер",
		Title = "Қызмет беттері",
		Singular = "Қызмет беті",
		Help = "Бір қызметке арналған толық бет, мыс. «Ата жолы» картасы. Мекенжайы: /kz/service/{сілтеме белгісі}. Тізімі бос бөлімдер бетте көрсетілмейді.",
		Model = typeof(Servicepage),
		Fields = c => new[]
		{
			new FieldDef("title", "Беттің тақырыбы", FieldKind.Text) { Required = true },
			Slug("atazholy"),
			new FieldDef("seoDescription", "Сипаттама (іздеу жүйелері және бөлісу үшін)", FieldKind.Textarea),
			Order()
		},
		DataFields = BlockRegistry.ServicePageFields,
		Columns = new[]
		{
			new ColumnDef("Реті", (o, l) => ((Servicepage)o).DisplayOrder.ToString(), "displayOrder"),
			new ColumnDef("Тақырыбы", (o, l) => $"<b>{E(((Servicepage)o).Title)}</b>", "title"),
			new ColumnDef("Мекенжайы", (o, l) => $"<code>/kz/service/{E(((Servicepage)o).Slug)}</code>")
		},
		Search = new[] { "title", "slug" },
		Prepare = (c, o, isNew) =>
		{
			Servicepage p = (Servicepage)o;
			EnsureOrder(c, "servicepage", p, isNew);
			return EnsureSlug(c, "servicepage", p, p.Title);
		},
		PublicUrl = (c, o) => "/kz/service/" + ((Servicepage)o).Slug
	};

	// ---- «Жаңалықтар» ----------------------------------------------------------------------

	public static readonly EntityDef Article = new EntityDef
	{
		Controller = "Press",
		Action = "Article",
		Group = "Жаңалықтар",
		Title = "Мақалалар мен жаңалықтар",
		Singular = "Мақала",
		Help = "Мәтіні бар мақала өз бетінде ашылады (/kz/news/…). Мәтіні жоқ қысқа жаңалық карточкасы «Сілтеме» өрісіндегі бетке апарады. «Маңызды» белгісі барлары тізімде бірінші тұрады. «Автоматты» белгісі барларын «Жаңалық көздері» өзі қосқан: керек емесін өшірсеңіз, қайта қосылмайды.",
		Model = typeof(Article),
		Fields = c => new[]
		{
			new FieldDef("categoryId", "Санаты", FieldKind.Select) { Required = true, Options = Pick(c, "select id, name from articlecategory where qStatus = 0 order by displayOrder, id") },
			new FieldDef("title", "Тақырыбы", FieldKind.Text) { Required = true },
			Slug("apostille"),
			new FieldDef("excerpt", "Қысқаша мазмұны", FieldKind.Textarea) { Help = "Карточкада және мақаланың басында шығады." },
			new FieldDef("bodyHtml", "Мақала мәтіні", FieldKind.Html) { Large = true, Help = "«Тақырып 2» (H2) бөлімдері оң жақтағы «Мазмұны» тізіміне өзі қосылады." },
			new FieldDef("linkUrl", "Сілтеме (мәтін жазылмаса)", FieldKind.Url) { Help = "Мәтіні жоқ жаңалық карточкасы осы бетке апарады, мыс. /kz/kaztest." },
			new FieldDef("coverImageUrl", "Мұқаба суреті", FieldKind.Image) { Help = "Бос қалса, түсті фон мен белгіше шығады." },
			new FieldDef("coverTone", "Мұқаба түсі (сурет жоқ болса)", FieldKind.Select) { Options = new[] { ("sky", "Көгілдір"), ("blue", "Көк"), ("sun", "Сары"), ("sand", "Құм түсті") } },
			new FieldDef("coverIcon", "Мұқаба белгішесі (сурет жоқ болса)", FieldKind.Icon),
			new FieldDef("publishTime", "Жарияланған күні", FieldKind.Date) { Help = "Тізім реті осы күн бойынша." },
			new FieldDef("dateText", "Көрсетілетін күн", FieldKind.Text) { Help = "Бос қалса, жарияланған күннен жазылады (мыс. 12.09.2026). Сөзбен жазылса (мыс. «Ақпан 2025»), аудармада да жазыңыз." },
			new FieldDef("readMinutes", "Оқу уақыты, минут", FieldKind.Number),
			new FieldDef("authorName", "Авторы", FieldKind.Text) { Help = "Толтырылса, мақалада автор блогы шығады (фото мен өмірбаян — «Беттер мен мәтіндер» → «Мақала: автор блогы»)." },
			new FieldDef("sourceName", "Дереккөз атауы", FieldKind.Text),
			new FieldDef("sourceUrl", "Дереккөз сілтемесі", FieldKind.Url),
			new FieldDef("serviceKey", "Мақаладағы батырма ашатын қызмет", FieldKind.Service),
			new FieldDef("isImportant", "«Маңызды» белгісі", FieldKind.Bool),
			new FieldDef("isPublished", "Сайтта көрсету", FieldKind.Bool),
			new FieldDef("seoDescription", "Сипаттама (іздеу жүйелері үшін)", FieldKind.Textarea) { Help = "Бос қалса, қысқаша мазмұны алынады." }
		},
		Columns = new[]
		{
			new ColumnDef("Күні", (o, l) => E(string.IsNullOrWhiteSpace(((Article)o).DateText) ? Date(((Article)o).PublishTime) : ((Article)o).DateText), "publishTime"),
			new ColumnDef("Санаты", (o, l) => E(CmsLookups.Name(l.ArticleCategories, ((Article)o).CategoryId)), "categoryId"),
			new ColumnDef("Тақырыбы", (o, l) =>
			{
				Article a = (Article)o;
				string badges = (a.IsImportant == 1 ? Badge("Маңызды", "warning") : string.Empty) + (a.IsPublished == 1 ? string.Empty : Badge("Жасырын", "danger"))
					+ (string.IsNullOrWhiteSpace(a.BodyHtml) ? Badge("Сілтеме", "info") : string.Empty)
					+ (a.NewsSourceId > 0 ? Badge("Автоматты · " + a.SourceName, "primary") : string.Empty);
				return $"<b>{Short(a.Title, 110)}</b><br>{badges}";
			}, "title"),
			new ColumnDef("Қаралды", (o, l) => ((Article)o).ViewCount.ToString(), "viewCount")
		},
		Search = new[] { "title", "excerpt", "slug" },
		DefaultOrder = "isImportant desc, publishTime desc, id desc",
		Filter = new FilterDef("categoryId", "Санаты", "cat", c => Pick(c, "select id, name from articlecategory where qStatus = 0 order by displayOrder, id")),
		Prepare = (c, o, isNew) =>
		{
			Article a = (Article)o;
			if (a.PublishTime == 0)
			{
				a.PublishTime = COMMON.UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
			}
			if (string.IsNullOrWhiteSpace(a.DateText))
			{
				a.DateText = COMMON.UnixTimeHelper.UnixTimeToDateTime(a.PublishTime).ToString("dd.MM.yyyy");
			}
			if (string.IsNullOrWhiteSpace(a.CoverTone))
			{
				a.CoverTone = "sky";
			}
			if (string.IsNullOrWhiteSpace(a.CoverIcon))
			{
				a.CoverIcon = "i-doc";
			}
			if (string.IsNullOrWhiteSpace(a.BodyHtml) && string.IsNullOrWhiteSpace(a.LinkUrl))
			{
				return "Мақала мәтінін жазыңыз не «Сілтеме» өрісін толтырыңыз — әйтпесе карточка ешқайда апармайды.";
			}
			return EnsureSlug(c, "article", a, a.Title);
		},
		PublicUrl = (c, o) => ContentStore.ArticleUrl((Article)o)
	};

	public static readonly EntityDef ArticleCategory = new EntityDef
	{
		Controller = "Press",
		Action = "Category",
		Group = "Жаңалықтар",
		Title = "Мақала санаттары",
		Singular = "Санат",
		Help = "Жаңалықтар бетіндегі сүзгі батырмалары. Белгісі «news» санаттағы жазбалар жаңалық ретінде көрсетіледі (мұқабасында күні, мерзімінде дереккөзі).",
		Model = typeof(Articlecategory),
		Fields = c => new[]
		{
			new FieldDef("name", "Атауы", FieldKind.Text) { Required = true },
			Slug("docs"),
			Order()
		},
		Columns = new[]
		{
			new ColumnDef("Реті", (o, l) => ((Articlecategory)o).DisplayOrder.ToString(), "displayOrder"),
			new ColumnDef("Атауы", (o, l) => $"<b>{E(((Articlecategory)o).Name)}</b>", "name"),
			new ColumnDef("Белгісі", (o, l) => $"<code>{E(((Articlecategory)o).Slug)}</code>", "slug"),
			new ColumnDef("Мақала", (o, l) => l.Counts.TryGetValue(((Articlecategory)o).Id, out int n) ? n.ToString() : "0")
		},
		Search = new[] { "name", "slug" },
		Prepare = (c, o, isNew) =>
		{
			Articlecategory x = (Articlecategory)o;
			EnsureOrder(c, "articlecategory", x, isNew);
			return EnsureSlug(c, "articlecategory", x, x.Name);
		},
		DeleteGuard = (c, ids) => InUse(Count(c, $"select count(1) from article where qStatus = 0 and categoryId in ({Ids(ids)})"),
			"Бұл санатта мақалалар бар. Алдымен оларды басқа санатқа ауыстырыңыз."),
		PublicUrl = (c, o) => "/kz/news#cat-" + ((Articlecategory)o).Slug
	};

	public static readonly EntityDef NewsSource = new EntityDef
	{
		Controller = "Press",
		Action = "Source",
		Group = "Жаңалықтар",
		Title = "Жаңалық көздері",
		Singular = "Жаңалық көзі",
		Help = "Сайт әр сағат сайын осы сайттардың RSS не Atom арнасын оқып, кілт сөздерге сай жаңалықтарды «Жаңалықтар» бетіне қосады. Карточкаға тек тақырыбы, арнадағы қысқа сипаттамасы (280 таңбаға дейін), дереккөз атауы мен күні жазылады; карточка түпнұсқа мақаланы жаңа бетте ашады. Толық мәтін мен сурет көшірілмейді, сайттың robots.txt ережесі сақталады. Жаңа көз қоспас бұрын сол сайттың материалдарды пайдалану шарттарын оқыңыз — рұқсат етпесе, көзді қоспаңыз. Карточканы «Мақалалар мен жаңалықтар» тізімінен өшірсеңіз, ол қайта қосылмайды.",
		Model = typeof(Newssource),
		Translatable = false,
		Fields = c => new[]
		{
			new FieldDef("name", "Атауы", FieldKind.Text) { Required = true, NoTranslate = true, Help = "Карточкада «Дереккөз: …» болып шығады, мыс. Tengrinews.kz." },
			new FieldDef("feedUrl", "RSS не Atom арнасының сілтемесі", FieldKind.Url) { Required = true, Help = "Мыс. https://kaz.tengrinews.kz/news.rss. Әдетте сайттың төменгі жағында «RSS» деп тұрады." },
			new FieldDef("siteUrl", "Сайты", FieldKind.Url),
			new FieldDef("keywords", "Кілт сөздер", FieldKind.Textarea)
			{
				NoTranslate = true,
				Default = NewsCollector.DefaultKeywords,
				Help = "Жаңалықтың тақырыбында не сипаттамасында осылардың біреуі болса, ол алынады. Үтірмен не жаңа жолдан бөліңіз; «+» — сөздердің бәрі бірге болуы керек (мыс. мигрант + Қазақстан). Сөздің басын жазсаңыз жеткілікті (қандас → қандастар). Бос қалса, арнадағы жаңалықтың бәрі алынады — тек көші-қон туралы ғана жазатын көзге."
			},
			new FieldDef("excludeWords", "Алып тастайтын сөздер", FieldKind.Textarea) { NoTranslate = true, Help = "Осылардың біреуі болса, жаңалық алынбайды. Үтірмен бөліңіз." },
			new FieldDef("categoryId", "Санаты", FieldKind.Select) { Options = Pick(c, "select id, name from articlecategory where qStatus = 0 order by displayOrder, id"), Help = "Бос қалса — «news» белгісі бар санат." },
			new FieldDef("autoPublish", "Бірден жариялау", FieldKind.Bool) { Default = "true", Help = "Белгіленбесе, жаңалық «Жасырын» болып қосылады: «Мақалалар мен жаңалықтар» тізімінде тексеріп, «Сайтта көрсету» белгісін қойыңыз." },
			new FieldDef("maxPerRun", "Бір тексерісте ең көбі", FieldKind.Number) { Default = "5", Help = "Бір тексерісте осы көзден қосылатын жаңалық саны (1–20)." },
			new FieldDef("isEnabled", "Қосулы", FieldKind.Bool) { Default = "true" },
			new FieldDef("note", "Ескертпе", FieldKind.Textarea) { NoTranslate = true, Help = "Мыс. сайттың пайдалану шарттары не редакциямен келісім туралы." },
			Order()
		},
		Columns = new[]
		{
			new ColumnDef("Атауы", (o, l) =>
			{
				Newssource x = (Newssource)o;
				string host = Uri.TryCreate(x.FeedUrl, UriKind.Absolute, out Uri u) ? u.Host : x.FeedUrl;
				string badges = (x.IsEnabled == 1 ? string.Empty : Badge("Өшірулі", "danger")) + (x.AutoPublish == 1 ? string.Empty : Badge("Тексеріп жариялау", "warning"));
				return $"<b>{E(x.Name)}</b><br><small class=\"text-muted\">{E(host)}</small> {badges}";
			}, "name"),
			new ColumnDef("Соңғы тексеріс", (o, l) =>
			{
				Newssource x = (Newssource)o;
				return x.LastCheckTime > 0 ? $"{Date(x.LastCheckTime)} {E(COMMON.UnixTimeHelper.UnixTimeToDateTime(x.LastCheckTime).ToString("HH:mm"))}<br><small>{Short(x.LastStatus, 160)}</small>" : "—";
			}, "lastCheckTime"),
			new ColumnDef("Қосылғаны", (o, l) => ((Newssource)o).AddedCount.ToString(), "addedCount")
		},
		Search = new[] { "name", "feedUrl", "siteUrl" },
		ListCommand = ("Қазір тексеру", "ti ti-refresh"),
		Prepare = (c, o, isNew) =>
		{
			Newssource x = (Newssource)o;
			x.FeedUrl = (x.FeedUrl ?? string.Empty).Trim();
			if (!Uri.TryCreate(x.FeedUrl, UriKind.Absolute, out Uri feed) || (feed.Scheme != Uri.UriSchemeHttp && feed.Scheme != Uri.UriSchemeHttps))
			{
				return "Арна сілтемесі толық болуы керек: https://… деп басталады.";
			}
			if (Count(c, "select count(1) from newssource where qStatus = 0 and feedUrl = @feedUrl and id <> @id", new { feedUrl = x.FeedUrl, id = x.Id }) > 0)
			{
				return "Бұл арна тізімде бар.";
			}
			x.MaxPerRun = x.MaxPerRun <= 0 ? 5 : Math.Min(x.MaxPerRun, 20);
			x.Keywords = (x.Keywords ?? string.Empty).Trim();
			x.ExcludeWords = (x.ExcludeWords ?? string.Empty).Trim();
			if (x.CategoryId == 0)
			{
				x.CategoryId = c.ExecuteScalar<int?>("select id from articlecategory where qStatus = 0 and slug = 'news' order by id limit 1") ?? 0;
			}
			EnsureOrder(c, "newssource", x, isNew);
			return null;
		},
		PublicUrl = (c, o) => string.IsNullOrWhiteSpace(((Newssource)o).SiteUrl) ? ((Newssource)o).FeedUrl : ((Newssource)o).SiteUrl
	};

	// ---- «Киелі» · Қазақстан ------------------------------------------------------------------

	public static readonly EntityDef Place = new EntityDef
	{
		Controller = "Atlas",
		Action = "Place",
		Group = "«Киелі» · Қазақстан",
		Title = "Нысандар",
		Singular = "Нысан",
		Help = "Картадағы табиғи, киелі және тарихи орындар. Әрқайсысының өз беті бар: /kz/place/{сілтеме белгісі}.",
		Model = typeof(Place),
		Fields = c => new[]
		{
			new FieldDef("name", "Атауы", FieldKind.Text) { Required = true },
			Slug("sharyn"),
			new FieldDef("regionId", "Өңірі", FieldKind.Select) { Required = true, Options = Pick(c, "select id, name from region where qStatus = 0 order by displayOrder, id") },
			new FieldDef("categoryId", "Санаты", FieldKind.Select) { Required = true, Options = Pick(c, "select id, name from placecategory where qStatus = 0 order by displayOrder, id") },
			new FieldDef("imageUrl", "Негізгі суреті", FieldKind.Image) { Required = true },
			new FieldDef("fact", "Қысқа дерек (карточкада)", FieldKind.Text),
			new FieldDef("lead", "Кіріспе (сурет үстінде)", FieldKind.Textarea),
			new FieldDef("bodyHtml", "Толық мәтін", FieldKind.Html) { Large = true },
			new FieldDef("facts", "Деректер жолағы", FieldKind.List) { Column = "FactsJson", Fields = new[] { new FieldDef("label", "Атауы", FieldKind.Text), new FieldDef("value", "Мәні", FieldKind.Text) } },
			new FieldDef("pano", "360° панорамалар", FieldKind.List)
			{
				Column = "PanoJson",
				Help = "Google Maps → Street View → «Бөлісу» → «Картаны ендіру»: src=\"…\" ішіндегі сілтеме.",
				Fields = new[] { new FieldDef("url", "Ендіру сілтемесі", FieldKind.Url) }
			},
			new FieldDef("lat", "Ендік (lat)", FieldKind.Number) { Help = "Мыс. 43.3513. Карта сілтемелері мен координаттар үшін." },
			new FieldDef("lon", "Бойлық (lon)", FieldKind.Number) { Help = "Мыс. 79.077." },
			Order()
		},
		Columns = new[]
		{
			new ColumnDef("Сурет", (o, l) => Thumb(((Place)o).ImageUrl)),
			new ColumnDef("Атауы", (o, l) => $"<b>{E(((Place)o).Name)}</b><br><small class=\"text-muted\">{Short(((Place)o).Fact, 60)}</small>", "name"),
			new ColumnDef("Өңірі", (o, l) => E(CmsLookups.Name(l.Regions, ((Place)o).RegionId)), "regionId"),
			new ColumnDef("Санаты", (o, l) => E(CmsLookups.Name(l.PlaceCategories, ((Place)o).CategoryId)), "categoryId"),
			new ColumnDef("Реті", (o, l) => ((Place)o).DisplayOrder.ToString(), "displayOrder")
		},
		Search = new[] { "name", "slug", "fact" },
		Filter = new FilterDef("regionId", "Өңірі", "region", c => Pick(c, "select id, name from region where qStatus = 0 order by displayOrder, id")),
		Prepare = (c, o, isNew) =>
		{
			Place p = (Place)o;
			EnsureOrder(c, "place", p, isNew);
			if (p.Lat < -90 || p.Lat > 90 || p.Lon < -180 || p.Lon > 180)
			{
				return "Координаттар қате: ендік −90…90, бойлық −180…180.";
			}
			return EnsureSlug(c, "place", p, p.Name);
		},
		PublicUrl = (c, o) => "/kz/place/" + ((Place)o).Slug
	};

	public static readonly EntityDef Region = new EntityDef
	{
		Controller = "Atlas",
		Action = "Region",
		Group = "«Киелі» · Қазақстан",
		Title = "Өңірлер",
		Singular = "Өңір",
		Help = "17 өңір картадағы пішіндерге «Картадағы нөмірі» арқылы байланған, сондықтан жаңа өңір қосуға болмайды. Атауын, нысан санын және ретін өзгертуге болады.",
		Model = typeof(Region),
		CanCreate = false,
		Fields = c => new[]
		{
			new FieldDef("name", "Атауы", FieldKind.Text) { Required = true },
			new FieldDef("shortName", "Қысқа атауы (өңірлер тізімінде)", FieldKind.Text) { Help = "Мыс. «Ақмола облысы» → «Ақмола»." },
			new FieldDef("placeCount", "Нысан саны", FieldKind.Number) { Help = "Өңірдегі барлық нысан саны (картаның түсі осыдан). Сайтқа қосылған нысан көп болса, сол сан шығады." },
			new FieldDef("isCity", "Республикалық маңызы бар қала", FieldKind.Bool),
			Order()
		},
		Columns = new[]
		{
			new ColumnDef("Реті", (o, l) => ((Region)o).DisplayOrder.ToString(), "displayOrder"),
			new ColumnDef("Атауы", (o, l) => $"<b>{E(((Region)o).Name)}</b>" + (((Region)o).IsCity == 1 ? " " + Badge("қала", "info") : string.Empty), "name"),
			new ColumnDef("Картадағы нөмірі", (o, l) => ((Region)o).MapId.ToString(), "mapId"),
			new ColumnDef("Нысан саны", (o, l) => ((Region)o).PlaceCount + (l.Counts.TryGetValue(((Region)o).Id, out int n) ? $" <small class=\"text-muted\">(сайтта {n})</small>" : string.Empty), "placeCount")
		},
		Search = new[] { "name", "shortName" },
		DeleteGuard = (c, ids) => "Өңірлер картаға байланған — оларды өшіруге болмайды."
	};

	public static readonly EntityDef PlaceCategory = new EntityDef
	{
		Controller = "Atlas",
		Action = "Category",
		Group = "«Киелі» · Қазақстан",
		Title = "Нысан санаттары",
		Singular = "Нысан санаты",
		Help = "Нысандар тізіміндегі сүзгі батырмалары (Табиғат, Киелі орындар…).",
		Model = typeof(Placecategory),
		Fields = c => new[]
		{
			new FieldDef("name", "Атауы (жекеше, карточкада)", FieldKind.Text) { Required = true },
			new FieldDef("pluralName", "Атауы (көпше, сүзгі батырмасында)", FieldKind.Text),
			Slug("tabigat"),
			Order()
		},
		Columns = new[]
		{
			new ColumnDef("Реті", (o, l) => ((Placecategory)o).DisplayOrder.ToString(), "displayOrder"),
			new ColumnDef("Атауы", (o, l) => $"<b>{E(((Placecategory)o).Name)}</b> · {E(((Placecategory)o).PluralName)}", "name"),
			new ColumnDef("Нысан", (o, l) => l.Counts.TryGetValue(((Placecategory)o).Id, out int n) ? n.ToString() : "0")
		},
		Search = new[] { "name", "pluralName", "slug" },
		Prepare = (c, o, isNew) =>
		{
			Placecategory x = (Placecategory)o;
			EnsureOrder(c, "placecategory", x, isNew);
			return EnsureSlug(c, "placecategory", x, x.Name);
		},
		DeleteGuard = (c, ids) => InUse(Count(c, $"select count(1) from place where qStatus = 0 and categoryId in ({Ids(ids)})"),
			"Бұл санатта нысандар бар. Алдымен оларды басқа санатқа ауыстырыңыз.")
	};

	public static readonly EntityDef Heritage = new EntityDef
	{
		Controller = "Atlas",
		Action = "Heritage",
		Group = "«Киелі» · Қазақстан",
		Title = "Мәдени мұра",
		Singular = "Мұра",
		Help = "«ЮНЕСКО тізіміндегі мұра» бөлімінің карточкалары (Қазақстан беті). Басты беттегі қойындыға атауы бойынша қосылады.",
		Model = typeof(Heritage),
		Fields = c => new[]
		{
			new FieldDef("name", "Атауы", FieldKind.Text) { Required = true },
			new FieldDef("kind", "Түрі", FieldKind.Text) { Help = "Мыс. «Дүниежүзілік мұра» не «Материалдық емес мұра»." },
			new FieldDef("year", "Тізімге енген жылы", FieldKind.Text) { NoTranslate = true },
			new FieldDef("imageUrl", "Суреті", FieldKind.Image) { Required = true },
			new FieldDef("linkUrl", "Сілтеме (міндетті емес)", FieldKind.Url),
			Order()
		},
		Columns = new[]
		{
			new ColumnDef("Сурет", (o, l) => Thumb(((Heritage)o).ImageUrl)),
			new ColumnDef("Атауы", (o, l) => $"<b>{E(((Heritage)o).Name)}</b>", "name"),
			new ColumnDef("Түрі", (o, l) => E(((Heritage)o).Kind) + " " + E(((Heritage)o).Year), "kind"),
			new ColumnDef("Реті", (o, l) => ((Heritage)o).DisplayOrder.ToString(), "displayOrder")
		},
		Search = new[] { "name", "kind" },
		Prepare = (c, o, isNew) =>
		{
			EnsureOrder(c, "heritage", o, isNew);
			return null;
		},
		PublicUrl = (c, o) => "/kz/kazakhstan#mura"
	};

	// ---- ҚАЗТЕСТ ----------------------------------------------------------------------------------

	private static (string, string)[] Variants(IDbConnection c) => Pick(c, "select id, title from kaztestvariant where qStatus = 0 order by displayOrder, id");

	public static readonly EntityDef KaztestVariant = new EntityDef
	{
		Controller = "Exam",
		Action = "Variant",
		Group = "ҚАЗТЕСТ",
		Title = "Байқау нұсқалары",
		Singular = "Нұсқа",
		Help = "Әр нұсқа — толық байқау тесті: тыңдалым (жазбалар мен сұрақтар) және оқылым (мәтіндер мен сұрақтар). Сайтта сұрағы бар және «Сайтта көрсету» белгіленген нұсқалар шығады. Уақыт пен өту шегі: «Беттер мен мәтіндер» → ҚАЗТЕСТ → «Нұсқалар бөлімі және тест баптаулары».",
		Model = typeof(Kaztestvariant),
		Fields = c => new[]
		{
			new FieldDef("title", "Атауы", FieldKind.Text) { Required = true, Help = "Мыс. «3-нұсқа»." },
			new FieldDef("isPublished", "Сайтта көрсету", FieldKind.Bool),
			Order()
		},
		Columns = new[]
		{
			new ColumnDef("Реті", (o, l) => ((Kaztestvariant)o).DisplayOrder.ToString(), "displayOrder"),
			new ColumnDef("Атауы", (o, l) => $"<b>{E(((Kaztestvariant)o).Title)}</b>" + (((Kaztestvariant)o).IsPublished == 1 ? string.Empty : " " + Badge("Жасырын", "danger")), "title"),
			new ColumnDef("Сұрақтар", (o, l) =>
			{
				int id = ((Kaztestvariant)o).Id;
				int n = l.Counts.TryGetValue(id, out int k) ? k : 0;
				return $"<a href=\"/kz/exam/question/list?variant={id}\">{n} сұрақ →</a> · <a href=\"/kz/exam/recording/list?variant={id}\">тыңдалым жазбалары →</a> · <a href=\"/kz/exam/passage/list?variant={id}\">оқылым мәтіндері →</a>";
			})
		},
		Search = new[] { "title" },
		Prepare = (c, o, isNew) =>
		{
			EnsureOrder(c, "kaztestvariant", o, isNew);
			return null;
		},
		DeleteGuard = (c, ids) => InUse(Count(c, $"select count(1) from kaztestquestion where qStatus = 0 and variantId in ({Ids(ids)})"),
			"Нұсқада сұрақтар бар. Нұсқаны сайттан алып тастау үшін «Сайтта көрсету» белгісін алыңыз."),
		PublicUrl = (c, o) => "/kz/kaztest#nusqalar"
	};

	public static readonly EntityDef KaztestQuestion = new EntityDef
	{
		Controller = "Exam",
		Action = "Question",
		Group = "ҚАЗТЕСТ",
		Title = "Сұрақтар",
		Singular = "Сұрақ",
		Help = "Тыңдалым сұрағына «Тыңдалым жазбалары» мәзіріндегі жазбаның нөмірі, оқылым сұрағына оқылым мәтінінің нөмірі қажет. Дұрыс жауабы белгіленбеген сұрақ нәтижеде есептелмейді. Мәтіндегі <u>…</u> асты сызылып көрсетіледі. Жауаптарды ресми тесттегідей қысқасынан ұзынына қарай орналастырған дұрыс.",
		Model = typeof(Kaztestquestion),
		Translatable = false,
		Fields = c => new[]
		{
			new FieldDef("variantId", "Нұсқасы", FieldKind.Select) { Required = true, Options = Variants(c) },
			new FieldDef("sectionNo", "Бөлімі", FieldKind.Select) { Required = true, Options = new[] { ("1", "1 — Тыңдалым"), ("2", "2 — Оқылым") } },
			new FieldDef("questionNo", "Сұрақ нөмірі (бөлім ішінде)", FieldKind.Number) { Required = true },
			new FieldDef("questionText", "Тапсырма", FieldKind.Textarea) { Required = true },
			new FieldDef("subText", "Сұрақ не сөйлем", FieldKind.Textarea),
			new FieldDef("optionA", "A жауабы", FieldKind.Text) { Required = true },
			new FieldDef("optionB", "B жауабы", FieldKind.Text) { Required = true },
			new FieldDef("optionC", "C жауабы", FieldKind.Text),
			new FieldDef("optionD", "D жауабы", FieldKind.Text),
			new FieldDef("answer", "Дұрыс жауабы", FieldKind.Select) { Options = new[] { ("A", "A"), ("B", "B"), ("C", "C"), ("D", "D") } },
			new FieldDef("audioNo", "Тыңдалым жазбасының нөмірі", FieldKind.Number) { Help = "Тыңдалым сұрағы үшін: «Тыңдалым жазбалары» мәзіріндегі осы нұсқаның жазба нөмірі (1, 2, 3)." },
			new FieldDef("passageNo", "Оқылым мәтінінің нөмірі", FieldKind.Number) { Help = "Оқылым сұрағы үшін: «Оқылым мәтіндері» мәзіріндегі осы нұсқаның мәтін нөмірі; 0 не бос — мәтінсіз." }
		},
		Columns = new[]
		{
			new ColumnDef("Нұсқасы", (o, l) => E(CmsLookups.Name(l.Variants, ((Kaztestquestion)o).VariantId)), "variantId"),
			new ColumnDef("Бөлімі", (o, l) => ((Kaztestquestion)o).SectionNo == 1 ? "Тыңдалым" : "Оқылым", "sectionNo"),
			new ColumnDef("№", (o, l) => ((Kaztestquestion)o).QuestionNo.ToString(), "questionNo"),
			new ColumnDef("Жазба / мәтін", (o, l) =>
			{
				Kaztestquestion q = (Kaztestquestion)o;
				return q.SectionNo == 1 ? (q.AudioNo > 0 ? $"{q.AudioNo}-жазба" : Badge("жазбасы жоқ", "warning")) : q.PassageNo > 0 ? $"{q.PassageNo}-мәтін" : "—";
			}),
			new ColumnDef("Сұрақ", (o, l) => $"{Short(((Kaztestquestion)o).QuestionText, 70)}<br><small class=\"text-muted\">{Short(((Kaztestquestion)o).SubText, 70)}</small>"),
			new ColumnDef("Жауабы", (o, l) => string.IsNullOrWhiteSpace(((Kaztestquestion)o).Answer) ? Badge("белгісіз", "warning") : $"<b>{E(((Kaztestquestion)o).Answer)}</b>")
		},
		Search = new[] { "questionText", "subText", "optionA", "optionB", "optionC", "optionD" },
		DefaultOrder = "variantId, sectionNo, questionNo, id",
		Filter = new FilterDef("variantId", "Нұсқасы", "variant", Variants),
		Prepare = (c, o, isNew) =>
		{
			Kaztestquestion q = (Kaztestquestion)o;
			q.SectionName = q.SectionNo == 1 ? "Тыңдалым" : "Оқылым";
			q.AudioUrl ??= string.Empty;
			if (q.SectionNo == 1 && q.AudioNo <= 0)
			{
				return "Тыңдалым сұрағына жазба нөмірін жазыңыз (1, 2 не 3).";
			}
			q.Answer = (q.Answer ?? string.Empty).Trim().ToUpperInvariant();
			if (q.Answer.Length > 0 && string.IsNullOrWhiteSpace(q.Answer == "C" ? q.OptionC : q.Answer == "D" ? q.OptionD : "x"))
			{
				return "Дұрыс жауап ретінде бос нұсқа таңдалды.";
			}
			if (Count(c, "select count(1) from kaztestquestion where qStatus = 0 and variantId = @v and sectionNo = @s and questionNo = @n and id <> @id",
				new { v = q.VariantId, s = q.SectionNo, n = q.QuestionNo, id = q.Id }) > 0)
			{
				return $"Бұл нұсқаның осы бөлімінде {q.QuestionNo}-сұрақ бар. Басқа нөмір жазыңыз.";
			}
			return null;
		},
		PublicUrl = (c, o) => "/kz/kaztest#nusqalar"
	};

	public static readonly EntityDef KaztestPassage = new EntityDef
	{
		Controller = "Exam",
		Action = "Passage",
		Group = "ҚАЗТЕСТ",
		Title = "Оқылым мәтіндері",
		Singular = "Оқылым мәтіні",
		Help = "Оқылым сұрақтарының жанында тұратын мәтіндер. Сұрақ мәтінге «Оқылым мәтінінің нөмірі» арқылы байланады.",
		Model = typeof(Kaztestpassage),
		Translatable = false,
		Fields = c => new[]
		{
			new FieldDef("variantId", "Нұсқасы", FieldKind.Select) { Required = true, Options = Variants(c) },
			new FieldDef("passageNo", "Мәтін нөмірі", FieldKind.Number) { Required = true },
			new FieldDef("bodyText", "Мәтін", FieldKind.Textarea) { Required = true, Help = "Абзацтардың арасына бос жол қалдырыңыз." }
		},
		Columns = new[]
		{
			new ColumnDef("Нұсқасы", (o, l) => E(CmsLookups.Name(l.Variants, ((Kaztestpassage)o).VariantId)), "variantId"),
			new ColumnDef("№", (o, l) => ((Kaztestpassage)o).PassageNo.ToString(), "passageNo"),
			new ColumnDef("Мәтін", (o, l) => Short(((Kaztestpassage)o).BodyText, 120))
		},
		Search = new[] { "bodyText" },
		DefaultOrder = "variantId, passageNo, id",
		Filter = new FilterDef("variantId", "Нұсқасы", "variant", Variants),
		Prepare = (c, o, isNew) =>
		{
			Kaztestpassage p = (Kaztestpassage)o;
			if (Count(c, "select count(1) from kaztestpassage where qStatus = 0 and variantId = @v and passageNo = @n and id <> @id", new { v = p.VariantId, n = p.PassageNo, id = p.Id }) > 0)
			{
				return $"Бұл нұсқада {p.PassageNo}-мәтін бар. Басқа нөмір жазыңыз.";
			}
			return null;
		}
	};

	public static readonly EntityDef KaztestAudio = new EntityDef
	{
		Controller = "Exam",
		Action = "Recording",
		Group = "ҚАЗТЕСТ",
		Title = "Тыңдалым жазбалары",
		Singular = "Тыңдалым жазбасы",
		Help = "Тыңдалым бөлімінің жазбалары: кім не айтатыны (мәтін) және аудио файлы. Тыңдалым сұрағы жазбаға «Тыңдалым жазбасының нөмірі» арқылы байланады. Аудиосы жоқ жазбаның сұрақтары сайтта ашық тұрады, бірақ тыңдалым бөлімі бағаланбайды. Жазбаның мәтіні тест біткен соң «Жауаптарды көру» бетінде көрсетіледі.",
		Model = typeof(Kaztestaudio),
		Translatable = false,
		Fields = c => new[]
		{
			new FieldDef("variantId", "Нұсқасы", FieldKind.Select) { Required = true, Options = Variants(c) },
			new FieldDef("audioNo", "Жазба нөмірі", FieldKind.Number) { Required = true, Help = "Нұсқа ішіндегі реті: 1, 2, 3." },
			new FieldDef("title", "Атауы (сайтта көрсетілмейді)", FieldKind.Text) { Help = "Мыс. «Диалог: Базардағы кездесу». Тест кезінде көрсетілмейді — жауапты ашып қоймау үшін." },
			new FieldDef("audioUrl", "Аудио файл", FieldKind.Audio) { Help = "MP3 (ұсынылады), M4A, OGG, WAV не WEBM, 25 МБ-қа дейін. Бос болса — сайтта «Аудиожазба әзірленуде» деп тұрады." },
			new FieldDef("credit", "Аудионың авторы", FieldKind.Text) { Help = "Плеердің астында көрсетіледі: дауыс берген адам не жасанды дауыс пен оның лицензиясы. Өз жазбаңызды жүктесеңіз, өзгертіңіз не өшіріңіз." },
			new FieldDef("creditUrl", "Авторға сілтеме", FieldKind.Url),
			new FieldDef("voices", "Дауыстар", FieldKind.Textarea) { Help = "Жазбаны кім оқиды: мыс. «Ерлан — ер адам, 30 жас шамасында; Сәуле — әйел адам»." },
			new FieldDef("script", "Жазбаның мәтіні", FieldKind.Textarea) { Required = true, Help = "Әр сөйлеушінің сөзі жеке жолда: «Ерлан: Сәлеметсіз бе!». Монологта бір ғана есім болады." }
		},
		Columns = new[]
		{
			new ColumnDef("Нұсқасы", (o, l) => E(CmsLookups.Name(l.Variants, ((Kaztestaudio)o).VariantId)), "variantId"),
			new ColumnDef("№", (o, l) => ((Kaztestaudio)o).AudioNo.ToString(), "audioNo"),
			new ColumnDef("Атауы", (o, l) => $"<b>{E(((Kaztestaudio)o).Title)}</b><br><small class=\"text-muted\">{Short(((Kaztestaudio)o).Voices, 90)}</small>", "title"),
			new ColumnDef("Аудио", (o, l) =>
			{
				Kaztestaudio a = (Kaztestaudio)o;
				return string.IsNullOrWhiteSpace(a.AudioUrl) ? Badge("жүктелмеген", "warning")
					: Badge("бар", "success") + ((a.Credit ?? string.Empty).Contains("Piper") ? Badge("жасанды дауыс", "info") : string.Empty);
			})
		},
		Search = new[] { "title", "voices", "script" },
		DefaultOrder = "variantId, audioNo, id",
		Filter = new FilterDef("variantId", "Нұсқасы", "variant", Variants),
		Prepare = (c, o, isNew) =>
		{
			Kaztestaudio a = (Kaztestaudio)o;
			a.Title ??= string.Empty;
			a.Voices ??= string.Empty;
			a.AudioUrl ??= string.Empty;
			a.Credit ??= string.Empty;
			a.CreditUrl ??= string.Empty;
			if (Count(c, "select count(1) from kaztestaudio where qStatus = 0 and variantId = @v and audioNo = @n and id <> @id", new { v = a.VariantId, n = a.AudioNo, id = a.Id }) > 0)
			{
				return $"Бұл нұсқада {a.AudioNo}-жазба бар. Басқа нөмір жазыңыз.";
			}
			return null;
		},
		PublicUrl = (c, o) => "/kz/kaztest#nusqalar"
	};

	/// <summary>Row counts shown in some lists (items per tab, articles per category…).</summary>
	public static Dictionary<int, int> Counts(IDbConnection c, EntityDef def)
	{
		string sql = def == ServiceTab ? "select tabId, count(1) from serviceitem where qStatus = 0 group by tabId"
			: def == ArticleCategory ? "select categoryId, count(1) from article where qStatus = 0 group by categoryId"
			: def == Region ? "select regionId, count(1) from place where qStatus = 0 group by regionId"
			: def == PlaceCategory ? "select categoryId, count(1) from place where qStatus = 0 group by categoryId"
			: def == KaztestVariant ? "select variantId, count(1) from kaztestquestion where qStatus = 0 group by variantId"
			: null;
		return sql == null ? new Dictionary<int, int>() : c.Query<(int, int)>(sql).ToDictionary(x => x.Item1, x => x.Item2);
	}
}
