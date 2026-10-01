using System.Collections.Generic;
using System.Linq;
using COMMON;
using MODEL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using KieliWeb.Caches;

namespace KieliWeb.Controllers;

[Route("api/[controller]")]
[ApiController]
public class QarApiBaseController : ControllerBase
{
	protected readonly IMemoryCache _memoryCache;

	protected readonly IWebHostEnvironment _environment;

	protected readonly IConfiguration _configuration;

	protected string CurrentLanguage
	{
		get
		{
			object obj;
			if (!base.HttpContext.Request.Headers.TryGetValue("language", out var language))
			{
				obj = QarCache.GetLanguageList(_memoryCache).FirstOrDefault((Language x) => x.IsDefault == 1)?.LanguageCulture;
				if (obj == null)
				{
					return "kz";
				}
			}
			else
			{
				obj = language.ToString();
			}
			return (string)obj;
		}
	}

	public QarApiBaseController(IMemoryCache memoryCache, IWebHostEnvironment environment, IConfiguration configuration)
	{
		_memoryCache = memoryCache;
		_environment = environment;
		_configuration = configuration;
	}

	public string T(string localKey)
	{
		return QarCache.GetLanguageValue(_memoryCache, localKey, CurrentLanguage);
	}

	protected string GetIpAddress()
	{
		if (base.HttpContext.Request.Headers.TryGetValue("X-Real-IP", out var ip))
		{
			return ip.ToString();
		}
		return base.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
	}

	protected int GetIntQueryParam(string paramName, int def)
	{
		string param = GetStringQueryParam(paramName, string.Empty);
		if (!int.TryParse(param, out var result))
		{
			return def;
		}
		return result;
	}

	protected string GetStringQueryParam(string paramName, string def = "")
	{
		try
		{
			return base.Request.Form[paramName].ToString();
		}
		catch
		{
			try
			{
				return base.HttpContext.Request.Query[paramName].ToString();
			}
			catch
			{
				return def;
			}
		}
	}

	protected List<int> GetIntListQueryParam(string paramName)
	{
		string paramIds = GetStringQueryParam(paramName, string.Empty);
		List<int> paramIdList = new List<int>();
		string[] array = paramIds.Split(',');
		foreach (string idStr in array)
		{
			if (int.TryParse(idStr, out var id) && id > 0)
			{
				paramIdList.Add(id);
			}
		}
		return paramIdList;
	}

	[AllowAnonymous]
	[HttpGet("{query}")]
	public IActionResult Language(string query)
	{
		query = (query ?? string.Empty).Trim().ToLower();
		if (!(query == "default"))
		{
			if (query == "list")
			{
				List<Language> allLanguageList = QarCache.GetLanguageList(_memoryCache);
				List<Language> languageList = allLanguageList.Where((Language x) => x.FrontendDisplay == 1).ToList();
				if (!languageList.Any())
				{
					Language defaultLanguage = allLanguageList.FirstOrDefault((Language x) => x.IsDefault == 1);
					if (defaultLanguage != null)
					{
						languageList.Add(defaultLanguage);
					}
				}
				return MessageHelper.RedirectAjax(T("ls_Searchsuccessful"), "success", "", languageList.Select((Language x) => new
				{
					FullName = x.FullName,
					ShortName = x.ShortName,
					LanguageCulture = x.LanguageCulture,
					UniqueSeoCode = x.UniqueSeoCode,
					IsDefault = (x.IsDefault == 1),
					FlagUrl = x.LanguageFlagImageUrl
				}));
			}
			return NotFound();
		}
		List<Language> allLanguageList2 = QarCache.GetLanguageList(_memoryCache);
		return MessageHelper.RedirectAjax(data: (allLanguageList2.FirstOrDefault((Language x) => x.IsDefault == 1 && x.FrontendDisplay == 1) ?? allLanguageList2.FirstOrDefault((Language x) => x.IsDefault == 1))?.LanguageCulture ?? string.Empty, message: T("ls_Searchsuccessful"), status: "success", backUrl: "");
	}
}
