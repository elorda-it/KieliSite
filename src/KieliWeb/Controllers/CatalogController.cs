using System.Collections.Generic;
using MODEL.FormatModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using KieliWeb.Setup.Cms;

namespace KieliWeb.Controllers;

/// <summary>Admin «Қызметтер»: service tabs, service items, service pages.</summary>
public class CatalogController : CmsController
{
	public CatalogController(IMemoryCache memoryCache, IWebHostEnvironment environment)
		: base(memoryCache, environment)
	{
	}

	public IActionResult Tab(string query) => CmsPage(Entities.ServiceTab, query);

	[HttpPost]
	public IActionResult Tab(int id, string dataJson, string lang) => CmsSave(Entities.ServiceTab, id, dataJson, lang);

	[HttpPost]
	public IActionResult GetTabList(ApiUnifiedModel model) => CmsList(Entities.ServiceTab, model);

	[HttpPost]
	public IActionResult SetTabStatus(List<int> idList) => CmsDelete(Entities.ServiceTab, idList);

	public IActionResult Item(string query) => CmsPage(Entities.ServiceItem, query);

	[HttpPost]
	public IActionResult Item(int id, string dataJson, string lang) => CmsSave(Entities.ServiceItem, id, dataJson, lang);

	[HttpPost]
	public IActionResult GetItemList(ApiUnifiedModel model) => CmsList(Entities.ServiceItem, model);

	[HttpPost]
	public IActionResult SetItemStatus(List<int> idList) => CmsDelete(Entities.ServiceItem, idList);

	public IActionResult Page(string query) => CmsPage(Entities.ServicePage, query);

	[HttpPost]
	public IActionResult Page(int id, string dataJson, string lang) => CmsSave(Entities.ServicePage, id, dataJson, lang);

	[HttpPost]
	public IActionResult GetPageList(ApiUnifiedModel model) => CmsList(Entities.ServicePage, model);

	[HttpPost]
	public IActionResult SetPageStatus(List<int> idList) => CmsDelete(Entities.ServicePage, idList);
}
