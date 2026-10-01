using System.Collections.Generic;
using MODEL.FormatModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using KieliWeb.Setup.Cms;

namespace KieliWeb.Controllers;

/// <summary>Admin «ҚАЗТЕСТ»: practice test variants, their questions, listening recordings and reading passages.</summary>
public class ExamController : CmsController
{
	public ExamController(IMemoryCache memoryCache, IWebHostEnvironment environment)
		: base(memoryCache, environment)
	{
	}

	public IActionResult Variant(string query) => CmsPage(Entities.KaztestVariant, query);

	[HttpPost]
	public IActionResult Variant(int id, string dataJson, string lang) => CmsSave(Entities.KaztestVariant, id, dataJson, lang);

	[HttpPost]
	public IActionResult GetVariantList(ApiUnifiedModel model) => CmsList(Entities.KaztestVariant, model);

	[HttpPost]
	public IActionResult SetVariantStatus(List<int> idList) => CmsDelete(Entities.KaztestVariant, idList);

	public IActionResult Question(string query) => CmsPage(Entities.KaztestQuestion, query);

	[HttpPost]
	public IActionResult Question(int id, string dataJson, string lang) => CmsSave(Entities.KaztestQuestion, id, dataJson, lang);

	[HttpPost]
	public IActionResult GetQuestionList(ApiUnifiedModel model) => CmsList(Entities.KaztestQuestion, model);

	[HttpPost]
	public IActionResult SetQuestionStatus(List<int> idList) => CmsDelete(Entities.KaztestQuestion, idList);

	public IActionResult Passage(string query) => CmsPage(Entities.KaztestPassage, query);

	[HttpPost]
	public IActionResult Passage(int id, string dataJson, string lang) => CmsSave(Entities.KaztestPassage, id, dataJson, lang);

	[HttpPost]
	public IActionResult GetPassageList(ApiUnifiedModel model) => CmsList(Entities.KaztestPassage, model);

	[HttpPost]
	public IActionResult SetPassageStatus(List<int> idList) => CmsDelete(Entities.KaztestPassage, idList);

	public IActionResult Recording(string query) => CmsPage(Entities.KaztestAudio, query);

	[HttpPost]
	public IActionResult Recording(int id, string dataJson, string lang) => CmsSave(Entities.KaztestAudio, id, dataJson, lang);

	[HttpPost]
	public IActionResult GetRecordingList(ApiUnifiedModel model) => CmsList(Entities.KaztestAudio, model);

	[HttpPost]
	public IActionResult SetRecordingStatus(List<int> idList) => CmsDelete(Entities.KaztestAudio, idList);
}
