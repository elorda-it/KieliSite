using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using COMMON;
using DBHelper;
using Dapper;
using MODEL;
using MODEL.FormatModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using KieliWeb.Attributes;
using SkiaSharp;

namespace KieliWeb.Controllers;

[NoRole]
[Authorize(Roles = "Admin")]
public class ModalController : QarBaseController
{
	public ModalController(IMemoryCache memoryCache, IWebHostEnvironment environment)
		: base(memoryCache, environment)
	{
	}

	public IActionResult DateTimePicker()
	{
		base.ViewData["elementId"] = GetStringQueryParam("elementId", string.Empty);
		return View($"~/Views/Console/{base.ControllerName}/{base.ActionName}.cshtml");
	}

	public IActionResult Relogin()
	{
		base.ViewData["reloginReason"] = GetStringQueryParam("reloginReason");
		return View($"~/Views/Console/{base.ControllerName}/{base.ActionName}.cshtml");
	}

	public IActionResult UploadAvatar()
	{
		return View($"~/Views/Console/{base.ControllerName}/{base.ActionName}.cshtml");
	}

	[Authorize(Roles = "Admin,User")]
	[HttpPost]
	public IActionResult UploadAvatar(IFormFile fileAvatar, string cropInfoStr)
	{
		if (fileAvatar == null)
		{
			return MessageHelper.RedirectAjax(T("ls_Chooseaimage"), "error", "", null);
		}
		if (!fileAvatar.ContentType.Contains("image") || !base.ImageFileExtensions.Any((string item) => fileAvatar.FileName.EndsWith(item, StringComparison.OrdinalIgnoreCase)))
		{
			return MessageHelper.RedirectAjax(T("ls_Tiiions"), "error", "", null);
		}
		if (string.IsNullOrEmpty(cropInfoStr))
		{
			return MessageHelper.RedirectAjax(T("ls_Tiiii"), "error", "", null);
		}
		CropInfoModel cropInfoModel = JsonHelper.DeserializeObject<CropInfoModel>(cropInfoStr);
		if (cropInfoModel == null)
		{
			return MessageHelper.RedirectAjax(T("ls_Tiiii"), "error", "", null);
		}
		string tempKey = DateTime.Now.ToString("yyyyMMddHHmmssfff");
		string fileFormat = Path.GetExtension(fileAvatar.FileName).ToLower();
		if (fileFormat.Equals(".svg"))
		{
			return MessageHelper.RedirectAjax(T("ls_Tiiions"), "error", "", null);
		}
		string webRoot = _environment.WebRootPath + "/uploads/avatar/";
		using MemoryStream stream = new MemoryStream();
		fileAvatar.CopyTo(stream);
		byte[] fileBytes = stream.ToArray();
		SKBitmap uploadImage = SKBitmap.Decode(fileBytes);
		if (uploadImage.Width < 200 || uploadImage.Height < 200)
		{
			return MessageHelper.RedirectAjax(T("ls_Puaiwewhp").Replace("{width}", 200.ToString()).Replace("{height}", 200.ToString()), "error", "", null);
		}
		if (fileFormat.Equals(".jpeg"))
		{
			fileFormat = ".jpg";
		}
		ImgHandler.CutImage(webRoot, tempKey + fileFormat, stream, Convert.ToInt32(cropInfoModel.X), Convert.ToInt32(cropInfoModel.Y), Convert.ToInt32(cropInfoModel.Width), Convert.ToInt32(cropInfoModel.Height), cropInfoModel.Rotate);
		string relativePath = "/uploads/avatar/" + tempKey + "_big.webp";
		relativePath = relativePath.Replace("_big.", "_small.");
		using (IDbConnection connection = Utilities.GetOpenConnection())
		{
			if (base.HttpContext.User.Identity.Role().Equals("Admin", StringComparison.OrdinalIgnoreCase))
			{
				int adminId = GetAdminId();
				Admin admin = connection.GetList<Admin>("where qStatus = 0 and id = @adminId", new { adminId }).FirstOrDefault();
				if (admin == null)
				{
					return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", relativePath);
				}
				if (!string.IsNullOrEmpty(admin.AvatarUrl))
				{
					string avatarSmall = _environment.WebRootPath + admin.AvatarUrl;
					string avatarMiddle = _environment.WebRootPath + admin.AvatarUrl.Replace("_small.", "_middle.");
					string avatarBig = _environment.WebRootPath + admin.AvatarUrl.Replace("_small.", "_big.");
					if (System.IO.File.Exists(avatarSmall))
					{
						System.IO.File.Delete(avatarSmall);
					}
					if (System.IO.File.Exists(avatarMiddle))
					{
						System.IO.File.Delete(avatarMiddle);
					}
					if (System.IO.File.Exists(avatarBig))
					{
						System.IO.File.Delete(avatarBig);
					}
				}
				admin.AvatarUrl = relativePath;
				admin.UpdateTime = UnixTimeHelper.GetCurrentUnixTime();
				if (connection.Update(admin) > 0)
				{
					List<int> roleIds = base.HttpContext.User.Identity.RoleIds();
					string roleNames = base.HttpContext.User.Identity.RoleNames();
					SaveLoginInfoToCookie(admin.Email, admin.Name, admin.Id, roleIds, roleNames, admin.IsSuper == 1, admin.AvatarUrl, admin.SkinName);
					return MessageHelper.RedirectAjax(T("ls_Uploadedsuccessfully"), "success", "", relativePath.Replace("_small.", "_middle."));
				}
			}
		}
		return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", relativePath);
	}
}
