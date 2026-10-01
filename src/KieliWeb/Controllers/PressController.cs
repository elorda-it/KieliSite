using System.Collections.Generic;
using System.Threading.Tasks;
using COMMON;
using MODEL.FormatModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using KieliWeb.Setup;
using KieliWeb.Setup.Cms;

namespace KieliWeb.Controllers;

/// <summary>Admin «Жаңалықтар»: articles and news, their categories, and the feeds of the automatic news.</summary>
public class PressController : CmsController
{
	public PressController(IMemoryCache memoryCache, IWebHostEnvironment environment)
		: base(memoryCache, environment)
	{
	}

	public IActionResult Article(string query) => CmsPage(Entities.Article, query);

	[HttpPost]
	public IActionResult Article(int id, string dataJson, string lang) => CmsSave(Entities.Article, id, dataJson, lang);

	[HttpPost]
	public IActionResult GetArticleList(ApiUnifiedModel model) => CmsList(Entities.Article, model);

	[HttpPost]
	public IActionResult SetArticleStatus(List<int> idList) => CmsDelete(Entities.Article, idList);

	public IActionResult Category(string query) => CmsPage(Entities.ArticleCategory, query);

	[HttpPost]
	public IActionResult Category(int id, string dataJson, string lang) => CmsSave(Entities.ArticleCategory, id, dataJson, lang);

	[HttpPost]
	public IActionResult GetCategoryList(ApiUnifiedModel model) => CmsList(Entities.ArticleCategory, model);

	[HttpPost]
	public IActionResult SetCategoryStatus(List<int> idList) => CmsDelete(Entities.ArticleCategory, idList);

	public IActionResult Source(string query) => CmsPage(Entities.NewsSource, query);

	/// <summary>Saves a source; with run=1 (the list's «Қазір тексеру») checks the feeds now instead: id -1 = every enabled source.</summary>
	[HttpPost]
	public async Task<IActionResult> Source(int id, string dataJson, string lang, string run)
	{
		if (run != "1")
		{
			return CmsSave(Entities.NewsSource, id, dataJson, lang);
		}
		NewsCollector.Result result = await NewsCollector.RunAsync(_memoryCache, id > 0 ? id : 0);
		return MessageHelper.RedirectAjax(result.Summary, result.Busy || (result.Errors.Count > 0 && result.Added == 0) ? "error" : "success", string.Empty, null);
	}

	[HttpPost]
	public IActionResult GetSourceList(ApiUnifiedModel model) => CmsList(Entities.NewsSource, model);

	[HttpPost]
	public IActionResult SetSourceStatus(List<int> idList) => CmsDelete(Entities.NewsSource, idList);
}
