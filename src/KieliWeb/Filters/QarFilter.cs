using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using COMMON;
using MODEL;
using MODEL.FormatModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using KieliWeb.Caches;

namespace KieliWeb.Filters;

public class QarFilter : IActionFilter, IFilterMetadata
{
	public void OnActionExecuting(ActionExecutingContext context)
	{
		if (!(context.Controller is Controller controller))
		{
			return;
		}
		IMemoryCache memoryCache = context.HttpContext.RequestServices.GetService<IMemoryCache>();
		string language = ((context.HttpContext.Request.RouteValues["culture"] as string) ?? string.Empty).ToLower();
		controller.ViewData["language"] = language;
		List<Language> languageList = QarCache.GetLanguageList(memoryCache);
		controller.ViewData["languageList"] = languageList;
		controller.ViewData["uniqueSeoCode"] = languageList.FirstOrDefault((Language x) => x.LanguageCulture.Equals(language))?.UniqueSeoCode ?? "kk";
		string fileInputLanguage = string.Empty;
		string tinyMceLanguage = string.Empty;
		string dateTimePickerLanguage = string.Empty;
		switch (language)
		{
		case "tote":
			fileInputLanguage = "kz-tote";
			tinyMceLanguage = "kz";
			dateTimePickerLanguage = "kz";
			break;
		case "kz":
			fileInputLanguage = "kz";
			tinyMceLanguage = "kk";
			dateTimePickerLanguage = "kz";
			break;
		case "ru":
			fileInputLanguage = "ru";
			tinyMceLanguage = "ru";
			dateTimePickerLanguage = "ru";
			break;
		case "zh-cn":
			fileInputLanguage = "zh";
			tinyMceLanguage = "zh_CN";
			dateTimePickerLanguage = "zh";
			break;
		}
		controller.ViewData["fileInputLanguage"] = fileInputLanguage;
		controller.ViewData["tinyMCELanguage"] = tinyMceLanguage;
		controller.ViewData["dateTimePickerLanguage"] = dateTimePickerLanguage;
		controller.ViewData["controllerName"] = context.HttpContext.Request.RouteValues["controller"]?.ToString();
		controller.ViewData["actionName"] = context.HttpContext.Request.RouteValues["action"]?.ToString();
		controller.ViewData["languageIsoCode"] = QarCache.GetLanguageList(memoryCache).FirstOrDefault((Language x) => x.LanguageCulture.Equals(language))?.ISOCode;
		controller.ViewData["siteSetting"] = QarCache.GetSiteSetting(memoryCache, language);
		controller.ViewData["userList"] = QarCache.GetAllAdminList(memoryCache).FindAll((Admin x) => x.IsSuper != 1).ConvertAll((Admin x) => new Admin
		{
			AvatarUrl = x.AvatarUrl,
			Email = x.Email,
			Name = x.Name,
			Id = x.Id,
			SkinName = x.SkinName
		});
		controller.ViewData["host"] = "https://" + context.HttpContext.Request.Host.Host.ToLower();
		string userAgent = context.HttpContext.Request.Headers["User-Agent"].ToString();
		controller.ViewData["isMobile"] = IsMobileDevice(userAgent);
		IIdentity identity = context.HttpContext.User.Identity;
		if (identity != null && identity.IsAuthenticated)
		{
			string role = identity.Role();
			if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
			{
				controller.ViewData["adminId"] = identity.AdminId();
				controller.ViewData["roleNames"] = identity.RoleNames();
				controller.ViewData["role"] = role;
				controller.ViewData["realName"] = identity.RealName();
				controller.ViewData["email"] = identity.Email();
				controller.ViewData["skinName"] = identity.SkinName();
				string avatarUrl = identity.AvatarUrl();
				controller.ViewData["avatarUrl"] = (string.IsNullOrEmpty(avatarUrl) ? "/images/default_avatar.png" : avatarUrl);
			}
		}
	}

	public void OnActionExecuted(ActionExecutedContext context)
	{
		string controllerName = context.RouteData.Values["controller"]?.ToString()?.ToLower();
		if (controllerName == null || controllerName.Equals("admin") || controllerName.Equals("modal") || controllerName.Equals("qarbase") || controllerName.Equals("site"))
		{
			return;
		}
		IActionResult actionResult = context.Result;
		string language = (context.HttpContext.Request.RouteValues["culture"] ?? string.Empty) as string;
		language = (string.IsNullOrEmpty(language) ? "kz" : language);
		string qUrl = "";
		if (actionResult is ViewResult viewResult)
		{
			if (language.Equals("latyn") || language.Equals("tote"))
			{
				IServiceProvider services = context.HttpContext.RequestServices;
				ViewResultExecutor executor = services.GetRequiredService<IActionResultExecutor<ViewResult>>() as ViewResultExecutor;
				IOptions<MvcViewOptions> option = services.GetRequiredService<IOptions<MvcViewOptions>>();
				ViewEngineResult result = executor.FindView(context, viewResult);
				result.EnsureSuccessful(null);
				IView view = result.View;
				StringBuilder builder = new StringBuilder();
				using (StringWriter writer = new StringWriter(builder))
				{
					ViewContext viewContext = new ViewContext(context, view, viewResult.ViewData, viewResult.TempData, writer, option.Value.HtmlHelperOptions);
					view.RenderAsync(viewContext).GetAwaiter().GetResult();
					writer.Flush();
				}
				string html = builder.ToString();
				_ = (StringValues)string.Empty;
				string userAgent = "pc";
				html = HtmlAgilityPackHelper.ConvertHtmlTextNode(html, language, userAgent, qUrl);
				context.Result = new ContentResult
				{
					Content = html,
					ContentType = "text/html"
				};
			}
		}
		else
		{
			if (!(actionResult is JsonResult jsonResult) || (!language.Equals("latyn") && !language.Equals("tote")) || !(jsonResult.Value is AjaxMsgModel model))
			{
				return;
			}
			if (!(language == "latyn"))
			{
				if (language == "tote")
				{
					model.Message = Cyrl2ToteHelper.Cyrl2Tote(model.Message);
				}
			}
			else
			{
				model.Message = Cyrl2LatynHelper.Cyrl2Latyn(model.Message);
			}
			jsonResult.Value = model;
		}
	}

	private bool IsMobileDevice(string userAgent)
	{
		if (string.IsNullOrEmpty(userAgent))
		{
			return false;
		}
		userAgent = userAgent.ToLower();
		string[] mobileKeywords = new string[9] { "mobile", "android", "iphone", "ipad", "windows phone", "blackberry", "opera mini", "iemobile", "webos" };
		return mobileKeywords.Any((string keyword) => userAgent.Contains(keyword));
	}
}
