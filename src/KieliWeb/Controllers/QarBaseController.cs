using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Claims;
using COMMON;
using DBHelper;
using Dapper;
using MODEL;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using KieliWeb.Attributes;
using KieliWeb.Caches;
using Serilog;

namespace KieliWeb.Controllers;

public class QarBaseController : Controller
{
	protected readonly IWebHostEnvironment _environment;

	protected readonly IMemoryCache _memoryCache;

	protected readonly string[] _imageFileExtensions = new string[7] { ".jpg", ".png", ".gif", ".jpeg", ".webp", ".avif", ".svg" };

	protected int ExpireDayCount => 7;

	public string CurrentLanguage => (base.ViewData["language"] ?? "kz") as string;

	public string ControllerName => (base.ViewData["controllerName"] ?? string.Empty) as string;

	public string ActionName => (base.ViewData["actionName"] ?? string.Empty) as string;

	public string CurrentTheme => QarSingleton.GetInstance().GetSiteTheme();

	public string NoImage => "/" + CurrentLanguage + "/QarBase/GenerateRatioImage?w=160&h=90";

	public List<Admin> UserList => (base.ViewData["userList"] ?? new List<Admin>()) as List<Admin>;

	protected IEnumerable<string> ImageFileExtensions => _imageFileExtensions;

	public QarBaseController(IMemoryCache memoryCache, IWebHostEnvironment environment)
	{
		_memoryCache = memoryCache;
		_environment = environment;
	}

	public string T(string localKey)
	{
		if (string.IsNullOrWhiteSpace(localKey))
		{
			return localKey;
		}
		return QarCache.GetLanguageValue(_memoryCache, localKey, CurrentLanguage);
	}

	public void SaveLoginInfoToCookie(string email, string realName, int adminId, List<int> roleIdList, string roleNames, bool isSuperAdmin, string avatarUrl, string skinName)
	{
		ClaimsIdentity identity = new ClaimsIdentity("AccountLogin");
		identity.AddClaim(new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", email));
		identity.AddClaim(new Claim("RealName", realName));
		identity.AddClaim(new Claim("AdminId", adminId.ToString()));
		identity.AddClaim(new Claim("RoleIds", string.Join(",", roleIdList)));
		identity.AddClaim(new Claim("RoleNames", roleNames));
		identity.AddClaim(new Claim("IsSuperAdmin", isSuperAdmin ? "1" : "0"));
		identity.AddClaim(new Claim("AvatarUrl", avatarUrl ?? string.Empty));
		identity.AddClaim(new Claim("SkinName", skinName ?? "light"));
		identity.AddClaim(new Claim("LoginTime", UnixTimeHelper.ConvertToUnixTime(DateTime.Now).ToString()));
		identity.AddClaim(new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "Admin"));
		ClaimsPrincipal principal = new ClaimsPrincipal(identity);
		AuthenticationProperties authProperties = new AuthenticationProperties
		{
			ExpiresUtc = DateTimeOffset.UtcNow.AddDays(ExpireDayCount)
		};
		base.HttpContext.SignInAsync("Cookies", principal, authProperties);
	}

	public int GetAdminId()
	{
		return Convert.ToInt32(base.ViewData["adminId"] ?? ((object)0));
	}

	public bool IsSuperAdmin()
	{
		return base.HttpContext.User.Identity.IsSuperAdmin();
	}

	public static List<Multilanguage> GetMultilanguageList(IDbConnection connection, string tableName, List<int> columnIdList, List<string> columnNameList = null, string language = "")
	{
		if (columnIdList == null || columnIdList.Count == 0)
		{
			return new List<Multilanguage>();
		}
		tableName = tableName.Trim().ToLower();
		string columnIdArrIn = "(" + string.Join(",", columnIdList.ToArray()) + ")";
		string querySql = "where qStatus = 0 and columnId in " + columnIdArrIn + " and tableName = @tableName ";
		object queryObj = new { tableName, language };
		if (!string.IsNullOrEmpty(language))
		{
			querySql += " and language = @language ";
		}
		if (columnNameList != null && columnNameList.Count > 0)
		{
			string columnNameArrIn = "(" + string.Join(",", columnNameList.Select((string x) => "'" + x + "'").ToArray()) + ")";
			querySql = querySql + " and columnName in " + columnNameArrIn + " ";
		}
		return connection.GetList<Multilanguage>(querySql, queryObj).ToList();
	}

	public bool SaveMultilanguageList(IDbConnection connection, List<Multilanguage> multiLanguageList, string tableName, int columnId)
	{
		tableName = tableName.Trim().ToLower();
		foreach (Multilanguage item in multiLanguageList)
		{
			item.Language = (item.Language ?? string.Empty).Trim().ToLower();
			if (string.IsNullOrEmpty(item.ColumnName))
			{
				continue;
			}
			string value = (item.ColumnName = item.ColumnName.Trim().ToLower());
			if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(item.Language))
			{
				continue;
			}
			Multilanguage multiLanguage = connection.GetList<Multilanguage>("where qStatus = 0 and language = @language and columnId = @columnId and tableName = @tableName and columnName = @columnName", new
			{
				language = item.Language,
				columnId = columnId,
				tableName = tableName,
				columnName = item.ColumnName
			}).FirstOrDefault();
			item.ColumnValue = (item.ColumnValue ?? string.Empty).Trim();
			if (multiLanguage != null)
			{
				if (string.IsNullOrEmpty(item.ColumnValue))
				{
					multiLanguage.QStatus = 1;
				}
				else
				{
					multiLanguage.ColumnValue = item.ColumnValue;
				}
				connection.Update(multiLanguage);
			}
			else if (!string.IsNullOrEmpty(item.ColumnValue))
			{
				connection.Insert(new Multilanguage
				{
					TableName = tableName,
					Language = item.Language,
					ColumnId = columnId,
					ColumnName = item.ColumnName,
					ColumnValue = item.ColumnValue,
					QStatus = 0
				});
			}
		}
		return false;
	}

	public Additionalcontent AdditionalContent(IDbConnection connection, string additionalType)
	{
		int currentTime = UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
		Additionalcontent additionalContent = connection.GetList<Additionalcontent>("where qStatus = 0 and additionalType = @additionalType ", new { additionalType }).FirstOrDefault();
		if (additionalContent == null)
		{
			additionalContent = new Additionalcontent
			{
				AdditionalType = additionalType,
				Title = "",
				ShortDescription = "",
				FullDescription = "",
				BackgroundImageUrl = "",
				BackgroundColor = "",
				Color = "",
				IconUrl = "",
				VideoEmbed = "",
				DisplayOrder = 0u,
				AddTime = currentTime,
				UpdateTime = currentTime,
				QStatus = 0
			};
			additionalContent.Id = connection.Insert(additionalContent).GetValueOrDefault();
		}
		base.ViewData["multiLanguageList"] = GetMultilanguageList(connection, "Additionalcontent", new List<int> { additionalContent.Id });
		return additionalContent;
	}

	[NoRole]
	[HttpPost]
	public IActionResult AdditionalContent(Additionalcontent item, IFormFile backgroundImage, string showFields, string multiLanguageJson)
	{
		List<string> showFieldList = (showFields ?? string.Empty).Split(",").ToList();
		foreach (string item2 in showFieldList)
		{
			switch (item2)
			{
			case "BackgroundImageUrl":
				if (string.IsNullOrEmpty(item.BackgroundImageUrl) && backgroundImage == null)
				{
					return MessageHelper.RedirectAjax(T("ls_Chooseaimage"), "error", "", null);
				}
				break;
			case "Title":
				if (string.IsNullOrWhiteSpace(item.Title))
				{
					return MessageHelper.RedirectAjax(T("ls_Tfir"), "error", "", "title");
				}
				break;
			case "ShortDescription":
				if (string.IsNullOrWhiteSpace(item.ShortDescription))
				{
					return MessageHelper.RedirectAjax(T("ls_Tfir"), "error", "", "shortDescription");
				}
				break;
			}
		}
		List<Multilanguage> multiLanguageList;
		try
		{
			multiLanguageList = JsonHelper.DeserializeObject<List<Multilanguage>>(multiLanguageJson);
		}
		catch (Exception exception)
		{
			Log.Error(exception, "AdditionalContent");
			return MessageHelper.RedirectAjax(T("ls_Edjd"), "error", "", "multiLanguageJson");
		}
		if (backgroundImage != null)
		{
			if (!backgroundImage.ContentType.Contains("image") || !_imageFileExtensions.Any((string value) => backgroundImage.FileName.EndsWith(value, StringComparison.OrdinalIgnoreCase)))
			{
				return MessageHelper.RedirectAjax(T("ls_Tiiions"), "error", "", null);
			}
			string tempKey = DateTime.Now.ToString("yyyyMMddHHmmssfff");
			string fileFormat = Path.GetExtension(backgroundImage.FileName).ToLower();
			item.BackgroundImageUrl = "/uploads/images/" + tempKey + fileFormat;
			string absolutePath = _environment.WebRootPath + item.BackgroundImageUrl;
			if (!Directory.Exists(Path.GetDirectoryName(absolutePath)))
			{
				Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
			}
			using FileStream stream = System.IO.File.OpenWrite(absolutePath);
			backgroundImage.CopyTo(stream);
		}
		else
		{
			item.BackgroundImageUrl = string.Empty;
		}
		using (IDbConnection connection = Utilities.GetOpenConnection())
		{
			Additionalcontent additionalContent = connection.GetList<Additionalcontent>("where qStatus = 0 and id  = @id", new
			{
				id = item.Id
			}).FirstOrDefault();
			if (additionalContent == null)
			{
				return MessageHelper.RedirectAjax(T("ls_Idoiiw"), "error", "", null);
			}
			if (!string.IsNullOrWhiteSpace(item.Title))
			{
				additionalContent.Title = item.Title;
			}
			if (!string.IsNullOrWhiteSpace(item.ShortDescription))
			{
				additionalContent.ShortDescription = item.ShortDescription;
			}
			if (!string.IsNullOrWhiteSpace(item.FullDescription))
			{
				additionalContent.FullDescription = item.FullDescription;
			}
			additionalContent.BackgroundColor = item.BackgroundColor ?? string.Empty;
			additionalContent.Color = item.Color ?? string.Empty;
			additionalContent.IconUrl = item.IconUrl ?? string.Empty;
			additionalContent.VideoEmbed = item.VideoEmbed ?? string.Empty;
			additionalContent.UpdateTime = UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
			if (!string.IsNullOrEmpty(item.BackgroundImageUrl))
			{
				additionalContent.BackgroundImageUrl = item.BackgroundImageUrl;
			}
			if (connection.Update(additionalContent) > 0)
			{
				SaveMultilanguageList(connection, multiLanguageList, "Additionalcontent", additionalContent.Id);
				QarCache.ClearCache(_memoryCache, "GetAdditionalContentList");
				return MessageHelper.RedirectAjax(T("ls_Updatesuccessfully"), "success", "", null);
			}
		}
		return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", null);
	}

	protected int GetIntQueryParam(string paramName, int defaultValue = 0)
	{
		string param = GetStringQueryParam(paramName, string.Empty);
		if (!int.TryParse(param, out var result))
		{
			return defaultValue;
		}
		return result;
	}

	protected string GetStringQueryParam(string paramName, string defaultValue = "")
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
				return defaultValue;
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

	public IActionResult GenerateRatioImage(float w, float h)
	{
		string fallbackPath = _environment.WebRootPath + "/images/no-picture.png";
		if (System.IO.File.Exists(fallbackPath))
		{
			return PhysicalFile(fallbackPath, "image/png");
		}
		return NotFound();
	}

	public void SaveRolePermissionList(IDbConnection connection, int roleId, List<Rolepermission> rolePermissionList)
	{
		if (roleId <= 0)
		{
			return;
		}
		List<Rolepermission> currentRolePermissionList = connection.GetList<Rolepermission>("where roleId = @roleId", new { roleId }).ToList();
		int currentTime = UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
		foreach (Rolepermission current in currentRolePermissionList)
		{
			if (rolePermissionList.Exists((Rolepermission x) => x.TableName.Equals(current.TableName) && x.PermissionId == current.PermissionId && x.ColumnId == current.ColumnId))
			{
				if (current.QStatus != 0)
				{
					current.QStatus = 0;
					current.UpdateTime = currentTime;
					connection.Update(current);
				}
			}
			else if (current.QStatus != 1)
			{
				current.QStatus = 1;
				current.UpdateTime = currentTime;
				connection.Update(current);
			}
		}
		foreach (Rolepermission rolePermission in rolePermissionList)
		{
			if (!currentRolePermissionList.Exists((Rolepermission x) => x.TableName.Equals(rolePermission.TableName) && x.PermissionId == rolePermission.PermissionId && x.ColumnId == rolePermission.ColumnId))
			{
				rolePermission.RoleId = roleId;
				rolePermission.AddTime = currentTime;
				rolePermission.UpdateTime = currentTime;
				rolePermission.QStatus = 0;
				connection.Insert(rolePermission);
			}
		}
	}
}
