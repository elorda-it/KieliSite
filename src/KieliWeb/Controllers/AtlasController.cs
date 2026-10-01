using System.Collections.Generic;
using MODEL.FormatModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using KieliWeb.Setup.Cms;

namespace KieliWeb.Controllers;

/// <summary>Admin «Киелі» · Қазақстан: places, regions, place categories, heritage.</summary>
public class AtlasController : CmsController
{
	public AtlasController(IMemoryCache memoryCache, IWebHostEnvironment environment)
		: base(memoryCache, environment)
	{
	}

	public IActionResult Place(string query) => CmsPage(Entities.Place, query);

	[HttpPost]
	public IActionResult Place(int id, string dataJson, string lang) => CmsSave(Entities.Place, id, dataJson, lang);

	[HttpPost]
	public IActionResult GetPlaceList(ApiUnifiedModel model) => CmsList(Entities.Place, model);

	[HttpPost]
	public IActionResult SetPlaceStatus(List<int> idList) => CmsDelete(Entities.Place, idList);

	public IActionResult Region(string query) => CmsPage(Entities.Region, query);

	[HttpPost]
	public IActionResult Region(int id, string dataJson, string lang) => CmsSave(Entities.Region, id, dataJson, lang);

	[HttpPost]
	public IActionResult GetRegionList(ApiUnifiedModel model) => CmsList(Entities.Region, model);

	[HttpPost]
	public IActionResult SetRegionStatus(List<int> idList) => CmsDelete(Entities.Region, idList);

	public IActionResult Category(string query) => CmsPage(Entities.PlaceCategory, query);

	[HttpPost]
	public IActionResult Category(int id, string dataJson, string lang) => CmsSave(Entities.PlaceCategory, id, dataJson, lang);

	[HttpPost]
	public IActionResult GetCategoryList(ApiUnifiedModel model) => CmsList(Entities.PlaceCategory, model);

	[HttpPost]
	public IActionResult SetCategoryStatus(List<int> idList) => CmsDelete(Entities.PlaceCategory, idList);

	public IActionResult Heritage(string query) => CmsPage(Entities.Heritage, query);

	[HttpPost]
	public IActionResult Heritage(int id, string dataJson, string lang) => CmsSave(Entities.Heritage, id, dataJson, lang);

	[HttpPost]
	public IActionResult GetHeritageList(ApiUnifiedModel model) => CmsList(Entities.Heritage, model);

	[HttpPost]
	public IActionResult SetHeritageStatus(List<int> idList) => CmsDelete(Entities.Heritage, idList);
}
