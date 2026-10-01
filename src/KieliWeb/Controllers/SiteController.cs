using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using COMMON;
using DBHelper;
using Dapper;
using MODEL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using KieliWeb.Attributes;
using KieliWeb.Caches;
using Serilog;

namespace KieliWeb.Controllers;

[Authorize(Roles = "Admin")]
public class SiteController : QarBaseController
{
	public SiteController(IMemoryCache memoryCache, IWebHostEnvironment environment)
		: base(memoryCache, environment)
	{
	}

	public IActionResult Setting()
	{
		base.ViewData["title"] = T("ls_Sitesettings");
		return View($"~/Views/Console/{base.ControllerName}/{base.ActionName}.cshtml");
	}

	[HttpPost]
	public IActionResult Setting(string name, string value, int pk = 0)
	{
		name = (name ?? string.Empty).Trim().ToLower();
		if (value == null)
		{
			value = string.Empty;
		}
		using (IDbConnection connection = Utilities.GetOpenConnection())
		{
			int? result = 0;
			string[] props = new string[22]
			{
				"title", "description", "keywords", "copyright", "analyticsHtml", "analyticsScript", "aboutUs", "aboutProject", "address", "phone",
				"email", "pressSecretary", "mapEmbed", "facebook", "twitter", "instagram", "vk", "telegram", "youtube", "whatsapp",
				"tiktok", "mStartAndEndTime"
			};
			if (props.Any((string x) => x.Contains(name, StringComparison.OrdinalIgnoreCase)))
			{
				Sitesetting siteSetting = connection.GetList<Sitesetting>("where qStatus = 0 and id = @id", new
				{
					id = pk
				}).FirstOrDefault();
				if (siteSetting == null)
				{
					return MessageHelper.RedirectAjax(T("ls_Idoiiw"), "error", "", null);
				}
				if (name.Equals("mStartAndEndTime", StringComparison.OrdinalIgnoreCase))
				{
					string[] mourningDayList = value.Split("~");
					if (mourningDayList.Length != 2 || !DateTime.TryParseExact(mourningDayList[0].Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var mStartTime) || !DateTime.TryParseExact(mourningDayList[1].Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var mEndTime))
					{
						return MessageHelper.RedirectAjax("Мақала автоматты жолданатын уақытын дұрыс жазыңыз!", "error", "", null);
					}
					siteSetting.MStartTime = UnixTimeHelper.ConvertToUnixTime(mStartTime);
					siteSetting.MEndTime = UnixTimeHelper.ConvertToUnixTime(mEndTime);
				}
				if (name.Equals("title", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.Title = value;
				}
				if (name.Equals("description", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.Description = value;
				}
				if (name.Equals("keywords", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.Keywords = value;
				}
				if (name.Equals("copyright", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.Copyright = value;
				}
				if (name.Equals("analyticsHtml", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.AnalyticsHtml = value;
				}
				if (name.Equals("analyticsScript", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.AnalyticsScript = value;
				}
				if (name.Equals("address", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.Address = value;
				}
				if (name.Equals("phone", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.Phone = value;
				}
				if (name.Equals("email", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.Email = value;
				}
				if (name.Equals("mapEmbed", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.MapEmbed = value;
				}
				if (name.Equals("facebook", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.Facebook = value;
				}
				if (name.Equals("twitter", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.Twitter = value;
				}
				if (name.Equals("instagram", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.Instagram = value;
				}
				if (name.Equals("vk", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.Vk = value;
				}
				if (name.Equals("telegram", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.Telegram = value;
				}
				if (name.Equals("youtube", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.Youtube = value;
				}
				if (name.Equals("whatsapp", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.Whatsapp = value;
				}
				if (name.Equals("tiktok", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.Tiktok = value;
				}
				result = connection.Update(siteSetting);
			}
			if (result > 0)
			{
				QarCache.ClearCache(_memoryCache, "GetSiteSetting");
				return MessageHelper.RedirectAjax(T("ls_Updatesuccessfully"), "success", $"/{base.CurrentLanguage}/{base.ControllerName.ToLower()}/{base.ActionName.ToLower()}/list", null);
			}
		}
		return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", null);
	}

	[HttpPost]
	public IActionResult UploadSiteLogo(IFormFile logoImage, string type)
	{
		if (logoImage == null)
		{
			return MessageHelper.RedirectAjax(T("ls_Chooseaimage"), "error", "", "");
		}
		if (!logoImage.ContentType.Contains("image") || !_imageFileExtensions.Any((string item) => logoImage.FileName.EndsWith(item, StringComparison.OrdinalIgnoreCase)))
		{
			return MessageHelper.RedirectAjax(T("ls_Tiiions"), "error", "", null);
		}
		string absolutePathDirectory = _environment.WebRootPath + "/uploads/images/";
		if (!Directory.Exists(absolutePathDirectory))
		{
			Directory.CreateDirectory(absolutePathDirectory);
		}
		string fileFormat = Path.GetExtension(logoImage.FileName).ToLower();
		if (fileFormat.Equals(".jpeg"))
		{
			fileFormat = ".jpg";
		}
		string absolutePath = absolutePathDirectory + "logo-" + type + fileFormat;
		using (FileStream file = System.IO.File.Create(absolutePath))
		{
			logoImage.CopyTo(file);
			file.Flush();
		}
		string logoUrl = "/uploads/images/logo-" + type + fileFormat + "?t=" + RandomHelper.GetNumberRandom(5);
		using (IDbConnection connection = Utilities.GetOpenConnection())
		{
			Sitesetting siteSetting = connection.GetList<Sitesetting>("where qStatus = 0 order by id").FirstOrDefault();
			if (siteSetting != null)
			{
				if (type.Equals("lightLogo", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.LightLogo = logoUrl;
				}
				else if (type.Equals("darkLogo", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.DarkLogo = logoUrl;
				}
				else if (type.Equals("mobileLightLogo", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.MobileLightLogo = logoUrl;
				}
				else if (type.Equals("mobileDarkLogo", StringComparison.OrdinalIgnoreCase))
				{
					siteSetting.MobileDarkLogo = logoUrl;
				}
				connection.Update(siteSetting);
				QarCache.ClearCache(_memoryCache, "GetSiteSetting");
				return MessageHelper.RedirectAjax(T("ls_Updatesuccessfully"), "success", "", null);
			}
		}
		return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", null);
	}

	[HttpPost]
	public IActionResult UploadSiteIcon(IFormFile iconFile)
	{
		if (iconFile == null)
		{
			return MessageHelper.RedirectAjax(T("ls_Chooseaimage"), "error", "", "");
		}
		if (!iconFile.ContentType.Contains("image") || !_imageFileExtensions.ToList().Exists((string x) => Path.GetExtension(iconFile.FileName.ToLower()).EndsWith(x)))
		{
			return MessageHelper.RedirectAjax(T("ls_Tiiions") + "(*.png)", "error", "", null);
		}
		string absolutePathDirectory = _environment.WebRootPath + "/uploads/images/";
		if (!Directory.Exists(absolutePathDirectory))
		{
			Directory.CreateDirectory(absolutePathDirectory);
		}
		string fileFormat = Path.GetExtension(iconFile.FileName).ToLower();
		string absolutePath = absolutePathDirectory + "icon" + fileFormat;
		using (FileStream file = System.IO.File.Create(absolutePath))
		{
			iconFile.CopyTo(file);
			file.Flush();
		}
		string iconUrl = "/uploads/images/icon" + fileFormat + "?t=" + RandomHelper.GetNumberRandom(5);
		using (IDbConnection connection = Utilities.GetOpenConnection())
		{
			Sitesetting siteSetting = connection.GetList<Sitesetting>("where qStatus = 0 order by id").FirstOrDefault();
			if (siteSetting != null)
			{
				siteSetting.Favicon = iconUrl;
				connection.Update(siteSetting);
				QarCache.ClearCache(_memoryCache, "GetSiteSetting");
				return MessageHelper.RedirectAjax(T("ls_Updatesuccessfully"), "success", "", null);
			}
		}
		return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", null);
	}

	public IActionResult Language()
	{
		base.ViewData["title"] = T("ls_Sitelanguage");
		return View($"~/Views/Console/{base.ControllerName}/{base.ActionName}.cshtml");
	}

	[HttpPost]
	public IActionResult Language(List<Language> languageList)
	{
		if (languageList == null || languageList.Count == 0)
		{
			return MessageHelper.RedirectAjax(T("ls_Objectisempty"), "error", "", "jsonData");
		}
		using IDbConnection connection = Utilities.GetOpenConnection();
		using IDbTransaction tran = connection.BeginTransaction();
		try
		{
			foreach (Language item in languageList)
			{
				if (item.Id == 0)
				{
					return MessageHelper.RedirectAjax(T("ls_Idoiiw" + $"(id = {item.Id}) "), "error", "", "");
				}
				Language itemLanguage = connection.GetList<Language>("where id = @id", new
				{
					id = item.Id
				}).FirstOrDefault();
				if (itemLanguage == null)
				{
					return MessageHelper.RedirectAjax(T("ls_Idoiiw" + $"(id = {item.Id}) "), "error", "", "");
				}
				itemLanguage.FrontendDisplay = item.FrontendDisplay;
				itemLanguage.BackendDisplay = item.BackendDisplay;
				itemLanguage.DisplayOrder = item.DisplayOrder;
				itemLanguage.IsDefault = item.IsDefault;
				connection.Update(itemLanguage);
			}
			tran.Commit();
			QarCache.ClearCache(_memoryCache, "GetLanguageList");
			return MessageHelper.RedirectAjax(T("ls_Updatesuccessfully"), "success", $"/{base.CurrentLanguage}/{base.ControllerName.ToLower()}/{base.ActionName.ToLower()}/list", null);
		}
		catch (Exception exception)
		{
			Log.Error(exception, base.ActionName);
			tran.Rollback();
			return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", null);
		}
	}

	public IActionResult Flush()
	{
		base.ViewData["title"] = T("ls_Flushcache");
		return View($"~/Views/Console/{base.ControllerName}/{base.ActionName}.cshtml");
	}

	[NoRole]
	[HttpPost]
	public IActionResult FlushCache()
	{
		KieliWeb.Setup.ContentStore.Clear(_memoryCache);
		Type type = typeof(QarCache);
		MethodInfo[] methodInfos = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		IEnumerable<string> methodNames = methodInfos.Select((MethodInfo method) => method.Name).Distinct();
		foreach (string name in methodNames)
		{
			QarCache.ClearCache(_memoryCache, name);
		}
		return MessageHelper.RedirectAjax(T("ls_Flushsuccessfully"), "success", "", null);
	}
}
