using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using COMMON;
using DBHelper;
using Dapper;
using MODEL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Caching.Memory;
using KieliWeb.Attributes;
using KieliWeb.Controllers;

namespace KieliWeb.Caches;

public static class QarCache
{
	private static string GetCurrentMethod([CallerMemberName] string methodName = "")
	{
		return methodName;
	}

	public static void ClearCache(IMemoryCache memoryCache, string cacheName, int id = 0)
	{
		memoryCache.Remove(cacheName);
		memoryCache.Remove(cacheName + "_1");
		memoryCache.Remove($"{cacheName}_{id}");
		foreach (Language language in GetLanguageList(memoryCache))
		{
			string culture = language.LanguageCulture ?? string.Empty;
			memoryCache.Remove(cacheName + "_" + culture);
			memoryCache.Remove(cacheName + "_" + culture.ToLower());
			for (int i = 1; i <= 30; i++)
			{
				memoryCache.Remove($"{cacheName}_{culture}_{i}");
			}
		}
	}

	public static List<Language> GetLanguageList(IMemoryCache memoryCache)
	{
		string cacheName = GetCurrentMethod("GetLanguageList");
		if (!memoryCache.TryGetValue<List<Language>>(cacheName, out List<Language> list))
		{
			using IDbConnection connection = Utilities.GetOpenConnection();
			list = (from x in connection.GetList<Language>("where qStatus = 0")
				select new Language
				{
					Id = x.Id,
					ShortName = x.ShortName,
					FullName = x.FullName,
					LanguageCulture = x.LanguageCulture,
					UniqueSeoCode = x.UniqueSeoCode,
					ISOCode = x.ISOCode,
					LanguageFlagImageUrl = x.LanguageFlagImageUrl,
					DisplayOrder = x.DisplayOrder,
					IsSubLanguage = x.IsSubLanguage,
					IsDefault = x.IsDefault,
					FrontendDisplay = x.FrontendDisplay,
					BackendDisplay = x.BackendDisplay,
					QStatus = x.QStatus
				} into x
				orderby x.DisplayOrder
				select x).ToList();
			memoryCache.Set(cacheName, list, TimeSpan.FromDays(7));
		}
		return list;
	}

	private static Dictionary<string, Dictionary<string, string>> GetLanguagePackDictionary(IMemoryCache memoryCache)
	{
		string cacheName = GetCurrentMethod("GetLanguagePackDictionary");
		if (memoryCache.TryGetValue<Dictionary<string, Dictionary<string, string>>>(cacheName, out Dictionary<string, Dictionary<string, string>> allLanguagePackDic))
		{
			return allLanguagePackDic;
		}
		allLanguagePackDic = new Dictionary<string, Dictionary<string, string>>();
		List<Language> languageList = GetLanguageList(memoryCache);
		string[] canConvertLanguages = new string[2] { "tote", "latyn" };
		string jsonLanguagePack = LanguagePackHelper.GetLanguagePackJsonString();
		if (string.IsNullOrEmpty(jsonLanguagePack))
		{
			return allLanguagePackDic;
		}
		Dictionary<string, Dictionary<string, string>> languagePackDictionary = JsonHelper.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(jsonLanguagePack);
		foreach (KeyValuePair<string, Dictionary<string, string>> entry in languagePackDictionary)
		{
			if (allLanguagePackDic.ContainsKey(entry.Key))
			{
				continue;
			}
			Dictionary<string, string> currentLanguagePackDic = new Dictionary<string, string>();
			foreach (KeyValuePair<string, string> valueEntry in entry.Value)
			{
				string key = valueEntry.Key.ToLower().Trim();
				if (key.Equals("kz", StringComparison.OrdinalIgnoreCase) && languageList.Exists((Language x) => canConvertLanguages.Contains(x.LanguageCulture)))
				{
					if (languageList.Exists((Language x) => x.LanguageCulture.Equals("kz", StringComparison.OrdinalIgnoreCase)) && !currentLanguagePackDic.ContainsKey("kz"))
					{
						currentLanguagePackDic.TryAdd("kz", valueEntry.Value);
					}
					if (languageList.Exists((Language x) => x.LanguageCulture.Equals("latyn", StringComparison.OrdinalIgnoreCase)) && !currentLanguagePackDic.ContainsKey("latyn"))
					{
						currentLanguagePackDic.TryAdd("latyn", Cyrl2LatynHelper.Cyrl2Latyn(valueEntry.Value));
					}
					if (languageList.Exists((Language x) => x.LanguageCulture.Equals("tote", StringComparison.OrdinalIgnoreCase)) && !currentLanguagePackDic.ContainsKey("tote"))
					{
						currentLanguagePackDic.TryAdd("tote", Cyrl2ToteHelper.Cyrl2Tote(valueEntry.Value));
					}
				}
				else if (languageList.Exists((Language x) => x.LanguageCulture.Equals(key, StringComparison.OrdinalIgnoreCase)))
				{
					currentLanguagePackDic.TryAdd(key, valueEntry.Value);
				}
			}
			allLanguagePackDic.Add(entry.Key, currentLanguagePackDic);
		}
		memoryCache.Set(cacheName, allLanguagePackDic, TimeSpan.FromDays(1));
		return allLanguagePackDic;
	}

	public static string GetLanguageValue(IMemoryCache memoryCache, string localKey, string language)
	{
		language = (language ?? string.Empty).ToLower().Trim();
		Dictionary<string, Dictionary<string, string>> languagePackDictionary = GetLanguagePackDictionary(memoryCache);
		if (languagePackDictionary.ContainsKey(localKey) && languagePackDictionary[localKey].ContainsKey(language))
		{
			if (!string.IsNullOrEmpty(languagePackDictionary[localKey][language]))
			{
				return languagePackDictionary[localKey][language];
			}
			if (!languagePackDictionary[localKey].ContainsKey("en"))
			{
				return localKey;
			}
			return languagePackDictionary[localKey]["en"];
		}
		return localKey;
	}

	public static List<Navigation> GetNavigationList(IMemoryCache memoryCache, int navigationTypeId = 1)
	{
		string cacheName = $"{GetCurrentMethod("GetNavigationList")}_{navigationTypeId}";
		if (!memoryCache.TryGetValue<List<Navigation>>(cacheName, out List<Navigation> list))
		{
			using IDbConnection connection = Utilities.GetOpenConnection();
			list = connection.GetList<Navigation>("where qStatus = 0 and navigationTypeId =  @navigationTypeId order by displayOrder asc ", new { navigationTypeId }).ToList();
			memoryCache.Set(cacheName, list, DateTimeOffset.MaxValue);
		}
		return list;
	}

	public static Sitesetting GetSiteSetting(IMemoryCache memoryCache, string language = "kz")
	{
		string cacheName = GetCurrentMethod("GetSiteSetting") + "_" + language;
		if (!memoryCache.TryGetValue<Sitesetting>(cacheName, out Sitesetting siteSetting))
		{
			using IDbConnection connection = Utilities.GetOpenConnection();
			siteSetting = connection.GetList<Sitesetting>("where qStatus = 0 order by id").FirstOrDefault();
			memoryCache.Set(cacheName, siteSetting, TimeSpan.FromMinutes(20L));
		}
		return siteSetting ?? new Sitesetting();
	}

	public static List<Additionalcontent> GetAdditionalContentList(IMemoryCache memoryCache, string language)
	{
		language = (language ?? string.Empty).ToLower().Trim();
		string cacheName = GetCurrentMethod("GetAdditionalContentList") + "_" + language;
		if (!memoryCache.TryGetValue<List<Additionalcontent>>(cacheName, out List<Additionalcontent> list))
		{
			using IDbConnection connection = Utilities.GetOpenConnection();
			list = connection.GetList<Additionalcontent>("where qStatus = 0 ").ToList();
			UpdateEntityListWithMultiLanguage(connection, list, language, new List<string> { "Title", "ShortDescription", "FullDescription" });
			memoryCache.Set(cacheName, list, TimeSpan.FromHours(1));
		}
		return list;
	}

	public static List<Role> GetRoleList(IMemoryCache memoryCache, string language)
	{
		string cacheName = GetCurrentMethod("GetRoleList") + "_" + language;
		if (!memoryCache.TryGetValue<List<Role>>(cacheName, out List<Role> list))
		{
			using IDbConnection connection = Utilities.GetOpenConnection();
			list = connection.GetList<Role>("where qStatus = 0 ").ToList();
			UpdateEntityListWithMultiLanguage(connection, list, language, new List<string> { "Name", "Description" });
			memoryCache.Set(cacheName, list, TimeSpan.FromMinutes(10L));
		}
		return list;
	}

	public static List<Permission> GetPermissionList(IMemoryCache memoryCache)
	{
		string cacheName = GetCurrentMethod("GetPermissionList");
		if (!memoryCache.TryGetValue<List<Permission>>(cacheName, out List<Permission> list))
		{
			using IDbConnection connection = Utilities.GetOpenConnection();
			list = connection.GetList<Permission>("where qStatus = 0").ToList();
			memoryCache.Set(cacheName, list, DateTimeOffset.MaxValue);
		}
		return list;
	}

	public static List<Rolepermission> GetRolePermissionList(IMemoryCache memoryCache)
	{
		string cacheName = GetCurrentMethod("GetRolePermissionList");
		if (!memoryCache.TryGetValue<List<Rolepermission>>(cacheName, out List<Rolepermission> list))
		{
			using IDbConnection connection = Utilities.GetOpenConnection();
			list = connection.GetList<Rolepermission>("where qStatus = 0").ToList();
			memoryCache.Set(cacheName, list, DateTimeOffset.MaxValue);
		}
		return list;
	}

	public static List<int> GetNavigationIdListByRoleId(IMemoryCache memoryCache, int roleId)
	{
		string cacheName = $"{GetCurrentMethod("GetNavigationIdListByRoleId")}_{roleId}";
		if (!memoryCache.TryGetValue<List<int>>(cacheName, out List<int> list))
		{
			list = new List<int>();
			List<Rolepermission> rolePermissionList = (from x in GetRolePermissionList(memoryCache)
				where roleId == x.RoleId
				select x).ToList();
			List<Navigation> navigationList = GetNavigationList(memoryCache);
			foreach (Navigation navigation in navigationList.Where((Navigation x) => x.ParentId == 0).ToList())
			{
				foreach (Navigation childNavigation in navigationList.Where((Navigation x) => x.ParentId == navigation.Id).ToList())
				{
					if (rolePermissionList.Exists((Rolepermission r) => r.TableName.Equals("Navigation", StringComparison.OrdinalIgnoreCase) && r.ColumnId == childNavigation.Id))
					{
						list.Add(childNavigation.Id);
						if (!list.Contains(navigation.Id))
						{
							list.Add(navigation.Id);
						}
					}
				}
			}
		}
		return list;
	}

	public static List<Admin> GetAllAdminList(IMemoryCache memoryCache)
	{
		string cacheName = GetCurrentMethod("GetAllAdminList");
		if (!memoryCache.TryGetValue<List<Admin>>(cacheName, out List<Admin> adminList))
		{
			using IDbConnection connection = Utilities.GetOpenConnection();
			adminList = connection.GetList<Admin>("where 1 = 1 ").ToList();
			memoryCache.Set(cacheName, adminList, TimeSpan.FromDays(10));
		}
		return adminList ?? new List<Admin>();
	}

	public static void CheckNavigationPermission(IMemoryCache memoryCache, List<int> roleIdList, Controller controller, ControllerActionDescriptor action, string method, out bool canView, out bool canCreate, out bool canEdit, out bool canDelete)
	{
		if (roleIdList == null)
		{
			roleIdList = new List<int>();
		}
		string actionName = action.ActionName.ToLower();
		string controllerName = action.ControllerName.ToLower();
		NoRoleAttribute controllerAttributes = controller.GetType().GetCustomAttribute<NoRoleAttribute>(inherit: false);
		IEnumerable<NoRoleAttribute> actionAttributes = action.MethodInfo.GetCustomAttributes<NoRoleAttribute>(inherit: false);
		if (controllerAttributes != null || actionAttributes.Any())
		{
			canView = (canCreate = (canEdit = (canDelete = true)));
			return;
		}
		if (method.Equals("POST", StringComparison.OrdinalIgnoreCase))
		{
			if (actionName.StartsWith("get") && actionName.EndsWith("list"))
			{
				string text = actionName;
				actionName = text.Substring(3, text.Length - 4 - 3);
			}
			else if (actionName.StartsWith("set") && actionName.EndsWith("status"))
			{
				string text = actionName;
				actionName = text.Substring(3, text.Length - 6 - 3);
			}
		}
		string url = $"/{controllerName}/{actionName}/list";
		int navigationId = GetNavigationList(memoryCache).FirstOrDefault((Navigation x) => x.NavUrl.Equals(url))?.Id ?? 0;
		canView = (canCreate = (canEdit = (canDelete = false)));
		if (navigationId != 0)
		{
			int viewPermissionId = GetPermissionList(memoryCache).FirstOrDefault((Permission x) => x.ManageType.Equals("view", StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
			int createPermissionId = GetPermissionList(memoryCache).FirstOrDefault((Permission x) => x.ManageType.Equals("create", StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
			int editPermissionId = GetPermissionList(memoryCache).FirstOrDefault((Permission x) => x.ManageType.Equals("edit", StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
			int deletePermissionId = GetPermissionList(memoryCache).FirstOrDefault((Permission x) => x.ManageType.Equals("delete", StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
			canView = GetRolePermissionList(memoryCache).Exists((Rolepermission x) => roleIdList.Contains(x.RoleId) && x.PermissionId == viewPermissionId && x.ColumnId == navigationId && x.TableName.Equals("Navigation", StringComparison.OrdinalIgnoreCase));
			canCreate = GetRolePermissionList(memoryCache).Exists((Rolepermission x) => roleIdList.Contains(x.RoleId) && x.PermissionId == createPermissionId && x.ColumnId == navigationId && x.TableName.Equals("Navigation", StringComparison.OrdinalIgnoreCase));
			canEdit = GetRolePermissionList(memoryCache).Exists((Rolepermission x) => roleIdList.Contains(x.RoleId) && x.PermissionId == editPermissionId && x.ColumnId == navigationId && x.TableName.Equals("Navigation", StringComparison.OrdinalIgnoreCase));
			canDelete = GetRolePermissionList(memoryCache).Exists((Rolepermission x) => roleIdList.Contains(x.RoleId) && x.PermissionId == deletePermissionId && x.ColumnId == navigationId && x.TableName.Equals("Navigation", StringComparison.OrdinalIgnoreCase));
		}
	}

	public static void UpdateEntityListWithMultiLanguage<T>(IDbConnection connection, List<T> entities, string language, List<string> keyList) where T : class
	{
		if (entities == null || !entities.Any())
		{
			return;
		}
		List<int> ids = entities.Select((T e) => (int)e.GetType().GetProperty("Id").GetValue(e)).ToList();
		List<Multilanguage> multiLanguageList = QarBaseController.GetMultilanguageList(connection, typeof(T).Name, ids, null, language).ToList();
		foreach (T entity in entities)
		{
			foreach (string key in keyList)
			{
				Multilanguage multiLanguageItem = multiLanguageList.FirstOrDefault((Multilanguage x) => x.ColumnId == (int)entity.GetType().GetProperty("Id").GetValue(entity) && x.ColumnName.Equals(key, StringComparison.OrdinalIgnoreCase));
				if (multiLanguageItem != null && !string.IsNullOrEmpty(multiLanguageItem.ColumnValue))
				{
					PropertyInfo propertyInfo = entity.GetType().GetProperty(key, BindingFlags.Instance | BindingFlags.Public);
					if (propertyInfo != null && propertyInfo.CanWrite)
					{
						propertyInfo.SetValue(entity, multiLanguageItem.ColumnValue, null);
					}
				}
			}
		}
	}
}
