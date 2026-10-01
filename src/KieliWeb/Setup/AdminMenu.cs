using System.Collections.Generic;

namespace KieliWeb.Setup;

/// <summary>
/// The admin sidebar as it is first written to the `navigation` table. After the first
/// start the table is the source of truth (rows can be renamed or reordered there);
/// a navUrl of "/{controller}/{action}/list" is what role permissions key on.
/// </summary>
public static class AdminMenu
{
	public record Item(string Title, string Url);

	public record Group(string Title, string Icon, Item[] Items, bool ContentOnly = true);

	public static readonly List<Group> Groups = new List<Group>
	{
		new Group("Беттер мен мәтіндер", "ti ti-layout-dashboard", new[]
		{
			new Item("Бет блоктары", "/content/block/list"),
		}),
		new Group("Қызметтер", "ti ti-briefcase", new[]
		{
			new Item("Қызмет бөлімдері", "/catalog/tab/list"),
			new Item("Қызмет түрлері", "/catalog/item/list"),
			new Item("Қызмет беттері", "/catalog/page/list"),
		}),
		new Group("Жаңалықтар", "ti ti-news", new[]
		{
			new Item("Мақалалар мен жаңалықтар", "/press/article/list"),
			new Item("Санаттар", "/press/category/list"),
			new Item("Жаңалық көздері", "/press/source/list"),
		}),
		new Group("«Киелі» · Қазақстан", "ti ti-map-2", new[]
		{
			new Item("Нысандар", "/atlas/place/list"),
			new Item("Өңірлер", "/atlas/region/list"),
			new Item("Нысан санаттары", "/atlas/category/list"),
			new Item("Мәдени мұра", "/atlas/heritage/list"),
		}),
		new Group("ҚАЗТЕСТ", "ti ti-checklist", new[]
		{
			new Item("Нұсқалар", "/exam/variant/list"),
			new Item("Сұрақтар", "/exam/question/list"),
			new Item("Оқылым мәтіндері", "/exam/passage/list"),
			new Item("Тыңдалым жазбалары", "/exam/recording/list"),
		}),
		new Group("Өтінімдер", "ti ti-inbox", new[]
		{
			new Item("Кеңес өтінімдері", "/lead/inbox/list"),
		}),
		new Group("Сайт баптаулары", "ti ti-settings", new[]
		{
			new Item("ls_Sitesettings", "/site/setting/list"),
			new Item("Сайт тілдері", "/site/language/list"),
			new Item("ls_Flushcache", "/site/flush/list"),
		}, ContentOnly: false),
		new Group("Әкімшілер", "ti ti-users", new[]
		{
			new Item("ls_Administrators", "/admin/person/list"),
			new Item("ls_Administratortype", "/admin/role/list"),
		}, ContentOnly: false),
	};
}
