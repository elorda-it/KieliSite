using System;
using System.Collections.Generic;
using System.Linq;
using COMMON;
using MODEL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using KieliWeb.Caches;

namespace KieliWeb.Filters;

public class PermissionFilter : IActionFilter, IFilterMetadata
{
	public void OnActionExecuting(ActionExecutingContext context)
	{
		if (!(context.Controller is Controller controller) || !(context.ActionDescriptor is ControllerActionDescriptor action) || !context.HttpContext.User.Identity.Role().Equals("Admin", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		bool isSuper = context.HttpContext.User.Identity.IsSuperAdmin();
		IMemoryCache _memoryCache = context.HttpContext.RequestServices.GetService<IMemoryCache>();
		if (isSuper)
		{
			controller.ViewData["canView"] = true;
			controller.ViewData["canCreate"] = true;
			controller.ViewData["canEdit"] = true;
			controller.ViewData["canDelete"] = true;
			controller.ViewData["navigationList"] = QarCache.GetNavigationList(_memoryCache);
			return;
		}
		string language = context.HttpContext.Request.RouteValues["culture"].ToString();
		string method = context.HttpContext.Request.Method.ToUpper();
		int loginTime = context.HttpContext.User.Identity.LoginTime();
		if (QarSingleton.GetInstance().IsReLoginAdmin(context.HttpContext.User.Identity.AdminId(), out var updateTime) && loginTime < updateTime)
		{
			if (!(method == "GET"))
			{
				if (method == "POST")
				{
					context.Result = MessageHelper.RedirectAjax("Your session has timed out. Please login again.", "error", "", null);
					return;
				}
			}
			else
			{
				controller.ViewData["reloginReason"] = "Your session has timed out. Please login again.";
			}
		}
		List<int> roleIdList = context.HttpContext.User.Identity.RoleIds();
		List<int> canViewNavigationIdList = new List<int>();
		foreach (int roleId in roleIdList)
		{
			canViewNavigationIdList.AddRange(QarCache.GetNavigationIdListByRoleId(_memoryCache, roleId));
		}
		controller.ViewData["navigationList"] = (from x in QarCache.GetNavigationList(_memoryCache)
			where canViewNavigationIdList.Contains(x.Id)
			select x).ToList();
		string controllerName = context.HttpContext.Request.RouteValues["controller"].ToString();
		string actionName = context.HttpContext.Request.RouteValues["action"].ToString();
		string query = (context.HttpContext.Request.RouteValues["query"] ?? string.Empty).ToString().Trim().ToLower();
		QarCache.CheckNavigationPermission(_memoryCache, roleIdList, controller, action, method, out var canView, out var canCreate, out var canEdit, out var canDelete);
		controller.ViewData["canView"] = canView;
		controller.ViewData["canCreate"] = canCreate;
		controller.ViewData["canEdit"] = canEdit;
		controller.ViewData["canDelete"] = canDelete;
		switch (query)
		{
		case "create":
			if (!canCreate)
			{
				context.Result = new RedirectResult($"/{language}/{controllerName}/{actionName}/list");
				return;
			}
			break;
		case "edit":
			if (!canEdit)
			{
				context.Result = new RedirectResult($"/{language}/{controllerName}/{actionName}/list");
				return;
			}
			break;
		case "list":
			if (!canView)
			{
				context.Result = new RedirectResult("/" + language + "/admin/profile");
				return;
			}
			break;
		}
		if (!method.Equals("POST", StringComparison.OrdinalIgnoreCase) || controllerName.Equals("QarBase", StringComparison.OrdinalIgnoreCase) || actionName.Equals("AdditionalContent", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		if (actionName.StartsWith("Get", StringComparison.OrdinalIgnoreCase) && actionName.EndsWith("List", StringComparison.OrdinalIgnoreCase))
		{
			if (!canView)
			{
				context.Result = MessageHelper.RedirectAjax(QarCache.GetLanguageValue(_memoryCache, "ls_Accessdenied", language), "error", "", new
				{
					start = 0,
					length = 10,
					keyword = "",
					total = 0,
					totalPage = 0,
					dataList = new List<object>()
				});
			}
		}
		else if (actionName.StartsWith("Set", StringComparison.OrdinalIgnoreCase) && actionName.EndsWith("Status", StringComparison.OrdinalIgnoreCase))
		{
			if (!canDelete)
			{
				context.Result = MessageHelper.RedirectAjax(QarCache.GetLanguageValue(_memoryCache, "ls_Accessdenied", language), "error", "", null);
			}
		}
		else if (context.HttpContext.Request.HasFormContentType)
		{
			string id = context.HttpContext.Request.Form["id"];
			if (id == "0" && !canCreate)
			{
				context.Result = MessageHelper.RedirectAjax(QarCache.GetLanguageValue(_memoryCache, "ls_Accessdenied", language), "error", "", null);
			}
			else if (!canEdit)
			{
				context.Result = MessageHelper.RedirectAjax(QarCache.GetLanguageValue(_memoryCache, "ls_Accessdenied", language), "error", "", null);
			}
		}
	}

	public void OnActionExecuted(ActionExecutedContext context)
	{
	}
}
