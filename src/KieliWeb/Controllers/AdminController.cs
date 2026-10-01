using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Principal;
using COMMON;
using DBHelper;
using Dapper;
using MODEL;
using MODEL.FormatModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using KieliWeb.Attributes;
using KieliWeb.Caches;
using Serilog;

namespace KieliWeb.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : QarBaseController
{
	public AdminController(IMemoryCache memoryCache, IWebHostEnvironment environment)
		: base(memoryCache, environment)
	{
	}

	[AllowAnonymous]
	public IActionResult Login()
	{
		IIdentity identity = base.HttpContext.User.Identity;
		if (identity != null && identity.IsAuthenticated)
		{
			return Redirect("/" + base.CurrentLanguage + "/admin/profile");
		}
		return View($"~/Views/Console/{base.ControllerName}/{base.ActionName}.cshtml");
	}

	[HttpPost]
	[AllowAnonymous]
	public IActionResult Login(string email, string password, string remember)
	{
		if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(email = email.Trim()))
		{
			return MessageHelper.RedirectAjax(T("ls_Peye"), "error", "", "email");
		}
		if (!RegexHelper.IsEmail(email = email.Trim().ToLower()))
		{
			return MessageHelper.RedirectAjax(T("ls_Peavea"), "error", "", "email");
		}
		if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(password = password.Trim()))
		{
			return MessageHelper.RedirectAjax(T("ls_Peyp"), "error", "", "password");
		}
		using IDbConnection connection = Utilities.GetOpenConnection();
		try
		{
			Admin admin = connection.GetList<Admin>("where qStatus = 0 and email = @email", new { email }).FirstOrDefault();
			if (admin == null)
			{
				return MessageHelper.RedirectAjax(T("ls_Weop"), "error", "", "");
			}
			Adminloginerrorlog errorLog = connection.GetList<Adminloginerrorlog>("where adminId = @adminId", new
			{
				adminId = admin.Id
			}).FirstOrDefault();
			DateTime currentTime = DateTime.Now;
			DateTime lastErrorTime = ((errorLog != null) ? UnixTimeHelper.UnixTimeToDateTime(errorLog.LastErrorTime) : currentTime);
			TimeSpan ts = new TimeSpan(currentTime.Ticks - lastErrorTime.Ticks);
			if (errorLog != null && errorLog.ErrorCount >= 5 && ts.TotalHours < 2.0)
			{
				return MessageHelper.RedirectAjax("Құпия сөз терудің сәтсіз әрекеті көп болғандықтан жүйеге кіру 2 сағатқа бұғатталды ", "error", "", null);
			}
			password = MD5Helper.PasswordMd5Encrypt(password);
			if (!admin.Password.Equals(password))
			{
				return MessageHelper.RedirectAjax(T("ls_Weop"), "error", "", "");
			}
			List<int> roleIdList = connection.Query<int>("select roleId from adminrole where qStatus = 0 and adminId = @adminId", new
			{
				adminId = admin.Id
			}).ToList();
			List<Role> roleList = (from x in QarCache.GetRoleList(_memoryCache, base.CurrentLanguage)
				where roleIdList.Contains(x.Id)
				select x).ToList();
			SaveLoginInfoToCookie(admin.Email, admin.Name, admin.Id, roleList.Select((Role x) => x.Id).ToList(), string.Join(" | ", roleList.Select((Role x) => x.Name).ToArray()), admin.IsSuper == 1, admin.AvatarUrl, admin.SkinName);
			if (errorLog != null)
			{
				errorLog.ErrorCount = 0;
				connection.Update(errorLog);
			}
			return MessageHelper.RedirectAjax(T("ls_Loginsuccessfully"), "success", "/" + base.CurrentLanguage + "/admin/profile", null);
		}
		catch (Exception exception)
		{
			Log.Error(exception, "Login");
			return MessageHelper.RedirectAjax("Сайт қателыгі, басқарушымен хабарласыңыз! ", "error", "", null);
		}
	}

	public IActionResult Signout()
	{
		string reason = GetStringQueryParam("reason");
		if (!string.IsNullOrEmpty(reason))
		{
			return Redirect("/" + base.CurrentLanguage.ToLower() + "/admin/login");
		}
		QarSingleton.GetInstance().RemoveReLoginAdmin(GetAdminId());
		int cookieExpireTime = UnixTimeHelper.ConvertToUnixTime(DateTime.Now.AddDays(-1 * base.ExpireDayCount));
		using (IDbConnection connection = Utilities.GetOpenConnection())
		{
			Admin admin = connection.GetList<Admin>("where relogin = 1 and id = @adminId", new
			{
				adminId = GetAdminId()
			}).FirstOrDefault();
			if (admin != null && admin.UpdateTime < cookieExpireTime)
			{
				admin.ReLogin = 0;
				connection.Update(admin);
			}
		}
		base.HttpContext.SignOutAsync("Cookies");
		return Redirect("/" + base.CurrentLanguage.ToLower() + "/admin/login");
	}

	public IActionResult Profile(string query)
	{
		using (IDbConnection connection = Utilities.GetOpenConnection())
		{
			int adminId = GetAdminId();
			base.ViewData["admin"] = connection.GetList<Admin>("where qStatus = 0 and id = @adminId", new { adminId }).FirstOrDefault();
		}
		return View($"~/Views/Console/{base.ControllerName}/{base.ActionName}.cshtml");
	}

	[NoRole]
	[HttpPost]
	public IActionResult ProfileInfo(Admin item)
	{
		if (item.Phone == null)
		{
			item.Phone = string.Empty;
		}
		if (string.IsNullOrWhiteSpace(item.Name))
		{
			return MessageHelper.RedirectAjax(T("ls_Peyn"), "error", "", "name");
		}
		if (string.IsNullOrWhiteSpace(item.Email))
		{
			return MessageHelper.RedirectAjax(T("ls_Peye"), "error", "", "email");
		}
		string email = (item.Email = item.Email.Trim().ToLower());
		if (!RegexHelper.IsEmail(email))
		{
			return MessageHelper.RedirectAjax(T("ls_Peavea"), "error", "", "email");
		}
		string phoneNumber = string.Empty;
		if (!string.IsNullOrWhiteSpace(item.Phone) && RegexHelper.IsPhoneNumber(item.Phone, out phoneNumber))
		{
			return MessageHelper.RedirectAjax(T("ls_Petcpn"), "error", "", "phone");
		}
		int currentTime = UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
		using (IDbConnection connection = Utilities.GetOpenConnection())
		{
			object queryObj = new
			{
				adminId = GetAdminId(),
				email = item.Email
			};
			if (connection.RecordCount<Admin>("where qStatus = 0 and id <> @adminId and email = @email", queryObj) > 0)
			{
				return MessageHelper.RedirectAjax(T("ls_Peavea"), "error", "", "email");
			}
			Admin admin = connection.GetList<Admin>("where qStatus = 0 and id = @adminId", queryObj).FirstOrDefault();
			if (admin == null)
			{
				return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", "");
			}
			admin.Email = item.Email;
			admin.Name = item.Name;
			admin.Phone = phoneNumber;
			admin.ReLogin = 1;
			admin.UpdateTime = currentTime;
			if (connection.Update(admin) > 0)
			{
				List<int> roleIds = base.HttpContext.User.Identity.RoleIds();
				string roleNames = base.HttpContext.User.Identity.RoleNames();
				SaveLoginInfoToCookie(admin.Email, admin.Name, admin.Id, roleIds, roleNames, admin.IsSuper == 1, admin.AvatarUrl, admin.SkinName);
				return MessageHelper.RedirectAjax(T("ls_Updatesuccessfully"), "success", "/" + base.CurrentLanguage + "/admin/profile", "");
			}
		}
		return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", "");
	}

	[NoRole]
	[HttpPost]
	public IActionResult ChangePassword(string oldPassword, string newPassword, string confirmPassword)
	{
		if (string.IsNullOrEmpty(oldPassword))
		{
			return MessageHelper.RedirectAjax(T("ls_Peyp"), "error", "", "oldPassword");
		}
		if (string.IsNullOrEmpty(newPassword))
		{
			return MessageHelper.RedirectAjax(T("ls_Peanp"), "error", "", "newPassword");
		}
		if (newPassword.Length < 6 || newPassword.Length > 20)
		{
			return MessageHelper.RedirectAjax(T("ls_Pmcbmamc").Replace("{min}", "6").Replace("{max}", "20"), "error", "", "newPassword");
		}
		if (string.IsNullOrEmpty(confirmPassword))
		{
			return MessageHelper.RedirectAjax(T("ls_Confirmnewpassword"), "error", "", "confirmPassword");
		}
		if (!newPassword.Equals(confirmPassword))
		{
			return MessageHelper.RedirectAjax(T("ls_Confirmnewpassword"), "error", "", "confirmPassword");
		}
		int currentTime = UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
		using (IDbConnection connection = Utilities.GetOpenConnection())
		{
			object queryObj = new
			{
				adminId = GetAdminId()
			};
			Admin admin = connection.GetList<Admin>("where qStatus = 0 and id = @adminId", queryObj).FirstOrDefault();
			if (admin == null)
			{
				return MessageHelper.RedirectAjax(T("ls_Idoiiw"), "error", "", "");
			}
			oldPassword = MD5Helper.PasswordMd5Encrypt(oldPassword);
			if (!admin.Password.Equals(oldPassword))
			{
				return MessageHelper.RedirectAjax(T("ls_Opii"), "error", "", "oldPassword");
			}
			admin.Password = MD5Helper.PasswordMd5Encrypt(newPassword);
			admin.UpdateTime = currentTime;
			if (connection.Update(admin) > 0)
			{
				return MessageHelper.RedirectAjax(T("ls_Passwordchangedsuccessfully"), "success", "/" + base.CurrentLanguage + "/admin/profile", "");
			}
		}
		return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", "");
	}

	[NoRole]
	[HttpPost]
	public IActionResult SaveSkin(string skinName)
	{
		skinName = (string.IsNullOrWhiteSpace(skinName) ? "default" : skinName);
		string[] skins = new string[3] { "dark", "light", "default" };
		if (!skins.Contains(skinName))
		{
			return MessageHelper.RedirectAjax(T("ls_Idoiiw"), "error", "", "");
		}
		int currentTime = UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
		using (IDbConnection connection = Utilities.GetOpenConnection())
		{
			object queryObj = new
			{
				adminId = GetAdminId()
			};
			Admin admin = connection.GetList<Admin>("where qStatus = 0 and id = @adminId", queryObj).FirstOrDefault();
			if (admin == null)
			{
				return MessageHelper.RedirectAjax(T("ls_Idoiiw"), "error", "", "");
			}
			admin.SkinName = skinName;
			admin.UpdateTime = currentTime;
			if (connection.Update(admin) > 0)
			{
				List<int> roleIds = base.HttpContext.User.Identity.RoleIds();
				string roleNames = base.HttpContext.User.Identity.RoleNames();
				SaveLoginInfoToCookie(admin.Email, admin.Name, admin.Id, roleIds, roleNames, admin.IsSuper == 1, admin.AvatarUrl, admin.SkinName);
				return MessageHelper.RedirectAjax(T("ls_Updatesuccessfully"), "success", "", "");
			}
		}
		return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", "");
	}

	public IActionResult Role(string query)
	{
		query = (query ?? string.Empty).Trim().ToLower();
		base.ViewData["query"] = query;
		base.ViewData["title"] = T("ls_Administratortype");
		switch (query)
		{
		case "create":
		{
			using (IDbConnection connection2 = Utilities.GetOpenConnection())
			{
				base.ViewData["permissionGroupList"] = (from x in connection2.GetList<Permission>("where qStatus = 0")
					group x by x.TableName).ToList();
				base.ViewData["allNavigationList"] = QarCache.GetNavigationList(_memoryCache);
			}
			return View($"~/Views/Console/{base.ControllerName}/{base.ActionName}/CreateOrEdit.cshtml");
		}
		case "edit":
		{
			int roleId = GetIntQueryParam("id");
			if (roleId <= 0)
			{
				return Redirect($"/{base.CurrentLanguage}/{base.ControllerName.ToLower()}/{base.ActionName.ToLower()}/list");
			}
			using (IDbConnection connection = Utilities.GetOpenConnection())
			{
				Role role = connection.GetList<Role>("where qStatus = 0 and id = @roleId ", new { roleId }).FirstOrDefault();
				if (role == null)
				{
					return Redirect($"/{base.CurrentLanguage}/{base.ControllerName.ToLower()}/{base.ActionName.ToLower()}/list");
				}
				base.ViewData["role"] = role;
				base.ViewData["multiLanguageList"] = QarBaseController.GetMultilanguageList(connection, "Role", new List<int> { role.Id });
				base.ViewData["permissionGroupList"] = (from x in connection.GetList<Permission>("where qStatus = 0")
					group x by x.TableName).ToList();
				base.ViewData["allNavigationList"] = QarCache.GetNavigationList(_memoryCache);
				base.ViewData["rolePermissionList"] = connection.GetList<Rolepermission>("where qStatus = 0 and roleId = @roleId", new
				{
					roleId = role.Id
				}).ToList();
			}
			return View($"~/Views/Console/{base.ControllerName}/{base.ActionName}/CreateOrEdit.cshtml");
		}
		case "list":
			return View($"~/Views/Console/{base.ControllerName}/{base.ActionName}/List.cshtml");
		default:
			return Redirect($"/{base.CurrentLanguage}/{base.ControllerName.ToLower()}/{base.ActionName.ToLower()}/list");
		}
	}

	[HttpPost]
	public IActionResult Role(Role item, string multiLanguageJson, string permissionJson)
	{
		if (string.IsNullOrEmpty(item.Name))
		{
			return MessageHelper.RedirectAjax(T("ls_Tfir"), "error", "", "name");
		}
		List<Multilanguage> multiLanguageList;
		try
		{
			multiLanguageList = JsonHelper.DeserializeObject<List<Multilanguage>>(multiLanguageJson);
		}
		catch (Exception exception)
		{
			Log.Error(exception, base.ActionName);
			return MessageHelper.RedirectAjax(T("ls_Edjd"), "error", "", "multiLanguageJson");
		}
		List<Rolepermission> rolePermissionList;
		try
		{
			rolePermissionList = JsonHelper.DeserializeObject<List<Rolepermission>>(permissionJson);
		}
		catch (Exception exception2)
		{
			Log.Error(exception2, base.ActionName);
			return MessageHelper.RedirectAjax(T("ls_Edjd"), "error", "", "");
		}
		if (item.Description == null)
		{
			item.Description = string.Empty;
		}
		int currentTime = UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
		using (IDbConnection connection = Utilities.GetOpenConnection())
		{
			if (item.Id == 0)
			{
				int? res = connection.Insert(new Role
				{
					Name = item.Name,
					Description = item.Description,
					AddTime = currentTime,
					UpdateTime = currentTime,
					QStatus = 0
				});
				if (res > 0)
				{
					SaveRolePermissionList(connection, res.Value, rolePermissionList);
					SaveMultilanguageList(connection, multiLanguageList, "Role", res.Value);
					QarCache.ClearCache(_memoryCache, "GetRoleList");
					QarCache.ClearCache(_memoryCache, "GetRolePermissionList");
					QarCache.ClearCache(_memoryCache, $"{"GetNavigationIdListByRoleId"}_{res.Value}");
					return MessageHelper.RedirectAjax(T("ls_Addedsuccessfully"), "success", $"/{base.CurrentLanguage}/{base.ControllerName.ToLower()}/{base.ActionName.ToLower()}/edit?id={res}", "");
				}
			}
			else
			{
				Role role = connection.GetList<Role>("where qStatus = 0 and id = @id", new
				{
					id = item.Id
				}).FirstOrDefault();
				if (role == null)
				{
					return MessageHelper.RedirectAjax(T("ls_Idoiiw"), "error", "", "");
				}
				role.Name = item.Name;
				role.Description = item.Description;
				role.UpdateTime = currentTime;
				int? res = connection.Update(role);
				if (res > 0)
				{
					SaveRolePermissionList(connection, role.Id, rolePermissionList);
					SaveMultilanguageList(connection, multiLanguageList, "Role", role.Id);
					QarCache.ClearCache(_memoryCache, "GetRoleList");
					QarCache.ClearCache(_memoryCache, "GetRolePermissionList");
					QarCache.ClearCache(_memoryCache, $"{"GetNavigationIdListByRoleId"}_{role.Id}");
					IEnumerable<int> adminRoleIdList = connection.Query<int>("select adminId from adminrole where qStatus = 0 and roleId = @roleId", new
					{
						roleId = role.Id
					});
					IEnumerable<Admin> adminList = connection.GetList<Admin>("where qStatus = 0 and id in (" + string.Join(",", adminRoleIdList) + ")");
					if (adminList != null)
					{
						foreach (Admin admin in adminList)
						{
							QarSingleton.GetInstance().AddReLoginAdmin(admin.Id, admin.UpdateTime);
						}
					}
					return MessageHelper.RedirectAjax(T("ls_Updatesuccessfully"), "success", $"/{base.CurrentLanguage}/{base.ControllerName.ToLower()}/{base.ActionName.ToLower()}/edit?id={role.Id}", "");
				}
			}
		}
		return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", "");
	}

	[HttpPost]
	public IActionResult GetRoleList(ApiUnifiedModel model)
	{
		int start = ((model.Start > 0) ? model.Start : 0);
		int length = ((model.Length > 0) ? model.Length : 10);
		string keyword = (model.Keyword ?? string.Empty).Trim();
		using IDbConnection connection = Utilities.GetOpenConnection();
		string querySql = " from role where qStatus = 0 ";
		object queryObj = new
		{
			keyword = "%" + keyword + "%"
		};
		string orderSql = "";
		if (!string.IsNullOrEmpty(keyword))
		{
			querySql += " and (name like @keyword)";
		}
		List<DataTableOrderModel> orderList = model.OrderList;
		if (orderList != null && orderList.Count > 0)
		{
			foreach (DataTableOrderModel item in model.OrderList)
			{
				int column = item.Column;
				if (column == 3)
				{
					orderSql = orderSql + (string.IsNullOrEmpty(orderSql) ? "" : ",") + " addTime " + item.Dir;
				}
			}
		}
		if (string.IsNullOrEmpty(orderSql))
		{
			orderSql = " addTime desc ";
		}
		int total = connection.Query<int>("select count(1) " + querySql, queryObj).FirstOrDefault();
		int totalPage = ((total % length == 0) ? (total / length) : (total / length + 1));
		List<Role> roleList = connection.Query<Role>("select * " + querySql + " order by " + orderSql + $" limit {start} , {length}", queryObj).ToList();
		QarCache.GetLanguageList(_memoryCache);
		var dataList = roleList.Select((Role x) => new
		{
			Id = x.Id,
			Name = x.Name,
			Description = x.Description,
			AddTime = UnixTimeHelper.UnixTimeToDateTime(x.AddTime).ToString("dd/MM/yyyy HH:mm")
		}).ToList();
		return MessageHelper.RedirectAjax(T("ls_Searchsuccessful"), "success", "", new { start, length, keyword, total, totalPage, dataList });
	}

	[HttpPost]
	public IActionResult SetRoleStatus(string manageType, List<int> idList)
	{
		manageType = (manageType ?? string.Empty).Trim().ToLower();
		if (idList == null || !idList.Any())
		{
			return MessageHelper.RedirectAjax(T("ls_Calo"), "error", "", null);
		}
		int currentTime = UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
		if (manageType == "delete")
		{
			using (IDbConnection connection = Utilities.GetOpenConnection())
			{
				using IDbTransaction tran = connection.BeginTransaction();
				try
				{
					List<Role> roleList = connection.GetList<Role>("where qStatus = 0 and id in (" + string.Join(",", idList) + ")").ToList();
					foreach (Role role in roleList)
					{
						role.QStatus = 1;
						role.UpdateTime = currentTime;
						connection.Update(role);
					}
					tran.Commit();
					QarCache.ClearCache(_memoryCache, "GetRoleList");
					QarCache.ClearCache(_memoryCache, "GetRolePermissionList");
					foreach (Role role2 in roleList)
					{
						QarCache.ClearCache(_memoryCache, $"{"GetNavigationIdListByRoleId"}_{role2.Id}");
					}
					return MessageHelper.RedirectAjax(T("ls_Deletedsuccessfully"), "success", "", "");
				}
				catch (Exception exception)
				{
					Log.Error(exception, base.ActionName);
					tran.Rollback();
					return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", "");
				}
			}
		}
		return MessageHelper.RedirectAjax(T("ls_Managetypeerror"), "error", "", null);
	}

	public IActionResult Person(string query)
	{
		query = (query ?? string.Empty).Trim().ToLower();
		base.ViewData["query"] = query;
		base.ViewData["title"] = T("ls_Administrators");
		switch (query)
		{
		case "create":
		{
			using (IDbConnection connection2 = Utilities.GetOpenConnection())
			{
				base.ViewData["roleList"] = connection2.GetList<Role>("where qStatus = 0").ToList();
			}
			return View($"~/Views/Console/{base.ControllerName}/{base.ActionName}/CreateOrEdit.cshtml");
		}
		case "edit":
		{
			int adminId = GetIntQueryParam("id");
			if (adminId <= 0)
			{
				return Redirect($"/{base.CurrentLanguage}/{base.ControllerName.ToLower()}/{base.ActionName.ToLower()}/list");
			}
			using (IDbConnection connection = Utilities.GetOpenConnection())
			{
				Admin admin = connection.GetList<Admin>("where qStatus = 0 and id = @adminId ", new { adminId }).FirstOrDefault();
				if (admin == null)
				{
					return Redirect($"/{base.CurrentLanguage}/{base.ControllerName.ToLower()}/{base.ActionName.ToLower()}/list");
				}
				base.ViewData["admin"] = admin;
				base.ViewData["roleList"] = connection.GetList<Role>("where qStatus = 0").ToList();
				base.ViewData["adminRoleList"] = connection.GetList<Adminrole>("where qStatus = 0 and adminId = @adminId ", new
				{
					adminId = admin.Id
				}).ToList();
			}
			return View($"~/Views/Console/{base.ControllerName}/{base.ActionName}/CreateOrEdit.cshtml");
		}
		case "list":
			return View($"~/Views/Console/{base.ControllerName}/{base.ActionName}/List.cshtml");
		default:
			return Redirect($"/{base.CurrentLanguage}/{base.ControllerName.ToLower()}/{base.ActionName.ToLower()}/list");
		}
	}

	[HttpPost]
	public IActionResult Person(Admin item, List<int> roleIdList)
	{
		if (item.Description == null)
		{
			item.Description = string.Empty;
		}
		if (!roleIdList.Any())
		{
			return MessageHelper.RedirectAjax(T("ls_Calo"), "error", "", "roleIdList");
		}
		if (string.IsNullOrEmpty(item.Name))
		{
			return MessageHelper.RedirectAjax(T("ls_Tfir"), "error", "", "name");
		}
		if (string.IsNullOrEmpty(item.Email))
		{
			return MessageHelper.RedirectAjax(T("ls_Tfir"), "error", "", "email");
		}
		if (item.Id == 0 && string.IsNullOrEmpty(item.Password))
		{
			return MessageHelper.RedirectAjax(T("ls_Peyp"), "error", "", "password");
		}
		if (!string.IsNullOrEmpty(item.Password) && (item.Password.Length < 6 || item.Password.Length > 20))
		{
			return MessageHelper.RedirectAjax(T("ls_Pmcbmamc").Replace("{min}", "6").Replace("{max}", "20"), "error", "", "password");
		}
		string phoneNumber = string.Empty;
		if (!string.IsNullOrEmpty(item.Phone) && !RegexHelper.IsPhoneNumber(item.Phone, out phoneNumber))
		{
			return MessageHelper.RedirectAjax(T("ls_Petcpn"), "error", "", "phone");
		}
		item.Phone = phoneNumber;
		int currentTime = UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
		using (IDbConnection connection = Utilities.GetOpenConnection())
		{
			if (item.Id == 0)
			{
				using IDbTransaction tran = connection.BeginTransaction();
				try
				{
					if (connection.RecordCount<Admin>("where qStatus = 0 and email = @email", new
					{
						email = item.Email
					}) > 0)
					{
						return MessageHelper.RedirectAjax(T("ls_Eaiar"), "error", "", "email");
					}
					int? res = connection.Insert(new Admin
					{
						Name = item.Name,
						Description = item.Description,
						IsSuper = 0,
						AvatarUrl = "",
						Email = item.Email,
						Password = MD5Helper.PasswordMd5Encrypt(item.Password),
						HiddenColumnJson = "",
						Phone = item.Phone,
						ReLogin = 0,
						SkinName = "light",
						AddTime = currentTime,
						UpdateTime = currentTime,
						QStatus = 0
					});
					if (res > 0)
					{
						foreach (int roleId in roleIdList)
						{
							connection.Insert(new Adminrole
							{
								RoleId = roleId,
								AdminId = res.Value,
								AddTime = currentTime,
								UpdateTime = currentTime,
								QStatus = 0
							});
						}
						tran.Commit();
						return MessageHelper.RedirectAjax(T("ls_Addedsuccessfully"), "success", $"/{base.CurrentLanguage}/{base.ControllerName.ToLower()}/{base.ActionName.ToLower()}/edit?id={res}", "");
					}
				}
				catch (Exception exception)
				{
					tran.Rollback();
					Log.Error(exception, base.ActionName);
					return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", "");
				}
			}
			else
			{
				using IDbTransaction tran2 = connection.BeginTransaction();
				try
				{
					Admin admin = connection.GetList<Admin>("where qStatus = 0 and id = @id", new
					{
						id = item.Id
					}).FirstOrDefault();
					if (admin == null)
					{
						return MessageHelper.RedirectAjax(T("ls_Idoiiw"), "error", "", "");
					}
					if (admin.IsSuper == 1 && admin.Id != GetAdminId())
					{
						return MessageHelper.RedirectAjax(T("ls_Accessdenied"), "error", "", "");
					}
					admin.Name = item.Name;
					if (connection.RecordCount<Admin>("where qStatus = 0 and id <> @adminId and email = @email", new
					{
						adminId = admin.Id,
						email = item.Email
					}) > 0)
					{
						return MessageHelper.RedirectAjax(T("ls_Eaiar"), "error", "", "email");
					}
					if (!string.IsNullOrEmpty(item.Password))
					{
						admin.Password = MD5Helper.PasswordMd5Encrypt(item.Password);
					}
					admin.Email = item.Email;
					admin.Description = item.Description;
					admin.Phone = item.Phone;
					admin.ReLogin = 1;
					admin.UpdateTime = currentTime;
					if (new int?(connection.Update(admin)) > 0)
					{
						List<Adminrole> adminRoleList = connection.GetList<Adminrole>("where qStatus = 0 and adminId = @adminId ", new
						{
							adminId = admin.Id
						}).ToList();
						foreach (Adminrole adminRole in adminRoleList)
						{
							if (!roleIdList.Contains(adminRole.RoleId))
							{
								adminRole.QStatus = 1;
								adminRole.UpdateTime = currentTime;
								connection.Update(adminRole);
							}
						}
						foreach (int roleId2 in roleIdList)
						{
							if (!adminRoleList.Exists((Adminrole x) => x.RoleId == roleId2))
							{
								Adminrole adminRole2 = connection.GetList<Adminrole>("where adminId = @adminId and roleId = @roleId ", new
								{
									adminId = admin.Id,
									roleId = roleId2
								}).FirstOrDefault();
								if (adminRole2 != null)
								{
									adminRole2.QStatus = 0;
									adminRole2.UpdateTime = currentTime;
									connection.Update(adminRole2);
									continue;
								}
								connection.Insert(new Adminrole
								{
									RoleId = roleId2,
									AdminId = admin.Id,
									AddTime = currentTime,
									UpdateTime = currentTime,
									QStatus = 0
								});
							}
						}
						tran2.Commit();
						QarSingleton.GetInstance().AddReLoginAdmin(admin.Id, admin.UpdateTime);
						return MessageHelper.RedirectAjax(T("ls_Updatesuccessfully"), "success", $"/{base.CurrentLanguage}/{base.ControllerName.ToLower()}/{base.ActionName.ToLower()}/edit?id={admin.Id}", "");
					}
				}
				catch (Exception exception2)
				{
					tran2.Rollback();
					Log.Error(exception2, base.ActionName);
					return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", "");
				}
			}
		}
		return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", "");
	}

	[HttpPost]
	public IActionResult GetPersonList(ApiUnifiedModel model)
	{
		int start = ((model.Start > 0) ? model.Start : 0);
		int length = ((model.Length > 0) ? model.Length : 10);
		string keyword = (model.Keyword ?? string.Empty).Trim();
		using IDbConnection connection = Utilities.GetOpenConnection();
		string querySql = " from admin where qStatus = 0 and isSuper <>  1 ";
		object queryObj = new
		{
			keyword = "%" + keyword + "%"
		};
		string orderSql = "";
		if (!string.IsNullOrEmpty(keyword))
		{
			querySql += " and (name like @keyword)";
		}
		List<DataTableOrderModel> orderList = model.OrderList;
		if (orderList != null && orderList.Count > 0)
		{
			foreach (DataTableOrderModel item in model.OrderList)
			{
				int column = item.Column;
				if (column == 4)
				{
					orderSql = orderSql + (string.IsNullOrEmpty(orderSql) ? "" : ",") + " addTime " + item.Dir;
				}
			}
		}
		if (string.IsNullOrEmpty(orderSql))
		{
			orderSql = " addTime desc ";
		}
		int total = connection.Query<int>("select count(1) " + querySql, queryObj).FirstOrDefault();
		int totalPage = ((total % length == 0) ? (total / length) : (total / length + 1));
		List<Admin> adminList = connection.Query<Admin>("select * " + querySql + " order by " + orderSql + $" limit {start} , {length}", queryObj).ToList();
		List<Role> roleList = QarCache.GetRoleList(_memoryCache, base.CurrentLanguage);
		List<object> dataList = new List<object>();
		if (adminList.Count > 0)
		{
			List<Adminrole> adminRoleList = connection.GetList<Adminrole>("where qStatus = 0 and adminId in (" + string.Join(",", adminList.Select((Admin x) => x.Id).ToArray()) + ")").ToList();
			foreach (Admin admin in adminList)
			{
				List<Role> currentRoleList = roleList.Where((Role r) => adminRoleList.Exists((Adminrole ar) => ar.AdminId == admin.Id && ar.RoleId == r.Id)).ToList();
				dataList.Add(new
				{
					Id = admin.Id,
					Name = admin.Name,
					Email = admin.Email,
					IsSuper = admin.IsSuper,
					Role = string.Join(", ", currentRoleList.Select((Role r) => r.Name).ToArray()),
					AddTime = UnixTimeHelper.UnixTimeToDateTime(admin.AddTime).ToString("dd/MM/yyyy HH:mm")
				});
			}
		}
		return MessageHelper.RedirectAjax(T("ls_Searchsuccessful"), "success", "", new { start, length, keyword, total, totalPage, dataList });
	}

	[HttpPost]
	public IActionResult SetPersonStatus(string manageType, List<int> idList)
	{
		manageType = (manageType ?? string.Empty).Trim().ToLower();
		if (idList == null || idList.Count == 0)
		{
			return MessageHelper.RedirectAjax(T("ls_Calo"), "error", "", null);
		}
		int currentTime = UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
		if (manageType == "delete")
		{
			using (IDbConnection connection = Utilities.GetOpenConnection())
			{
				using IDbTransaction tran = connection.BeginTransaction();
				try
				{
					List<Admin> adminList = connection.GetList<Admin>("where qStatus = 0 and id in (" + string.Join(",", idList) + ")").ToList();
					foreach (Admin admin in adminList)
					{
						if (admin.IsSuper != 1)
						{
							admin.QStatus = 1;
							admin.UpdateTime = currentTime;
							connection.Update(admin);
						}
					}
					tran.Commit();
					return MessageHelper.RedirectAjax(T("ls_Deletedsuccessfully"), "success", "", "");
				}
				catch (Exception exception)
				{
					Log.Error(exception, base.ActionName);
					tran.Rollback();
					return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", "");
				}
			}
		}
		return MessageHelper.RedirectAjax(T("ls_Managetypeerror"), "error", "", null);
	}
}
