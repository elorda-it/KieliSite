using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using COMMON;
using Dapper;
using DBHelper;
using MODEL;
using MODEL.FormatModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using static KieliWeb.Setup.Cms.CmsText;

namespace KieliWeb.Controllers;

/// <summary>Admin «Өтінімдер»: the «Кеңес алу» requests sent from the site, with their attached documents.</summary>
[Authorize(Roles = "Admin")]
public class LeadController : QarBaseController
{
	public static readonly (byte Value, string Label, string Tone)[] Statuses =
	{
		(0, "Жаңа", "danger"), (1, "Жұмыста", "warning"), (2, "Аяқталды", "success"), (3, "Жабылды", "secondary")
	};

	private static readonly Dictionary<string, string> CountryNames = new Dictionary<string, string> { ["cn"] = "Қытайда", ["kz"] = "Қазақстанда", ["other"] = "Басқа елде" };

	private readonly IConfiguration _configuration;

	public LeadController(IMemoryCache memoryCache, IWebHostEnvironment environment, IConfiguration configuration)
		: base(memoryCache, environment)
	{
		_configuration = configuration;
	}

	public static string StatusBadge(byte status)
	{
		(byte _, string label, string tone) = Statuses.FirstOrDefault(s => s.Value == status);
		return Badge(label ?? "?", tone ?? "secondary");
	}

	public static string Country(string code) => CountryNames.TryGetValue(code ?? string.Empty, out string name) ? name : "—";

	public IActionResult Inbox(string query)
	{
		query = (query ?? string.Empty).Trim().ToLower();
		ViewData["query"] = query;
		ViewData["cms"] = true;
		ViewData["title"] = "Кеңес өтінімдері";
		switch (query)
		{
		case "list":
			using (IDbConnection connection = Utilities.GetOpenConnection())
			{
				ViewData["newCount"] = connection.ExecuteScalar<int>("select count(1) from consultrequest where qStatus = 0 and status = 0");
			}
			return View("~/Views/Console/Lead/Inbox.cshtml");
		case "edit":
		{
			using IDbConnection connection = Utilities.GetOpenConnection();
			Consultrequest request = connection.GetList<Consultrequest>("where qStatus = 0 and id = @id", new { id = GetIntQueryParam("id") }).FirstOrDefault();
			if (request == null)
			{
				return Redirect($"/{CurrentLanguage}/lead/inbox/list");
			}
			ViewData["files"] = connection.GetList<Consultfile>("where qStatus = 0 and requestId = @id order by id", new { id = request.Id }).ToList();
			return View("~/Views/Console/Lead/Detail.cshtml", request);
		}
		case "file":
		{
			// documents are only shown to administrators who may see the requests
			if (!CanViewRequests())
			{
				return Forbid();
			}
			using IDbConnection connection = Utilities.GetOpenConnection();
			Consultfile file = connection.GetList<Consultfile>("where qStatus = 0 and id = @id", new { id = GetIntQueryParam("id") }).FirstOrDefault();
			if (file == null)
			{
				return NotFound();
			}
			string root = Path.GetFullPath(ConsultController.FileRoot(_configuration, _environment));
			string path = Path.GetFullPath(Path.Combine(root, file.StoredName));
			if (!path.StartsWith(root, StringComparison.Ordinal) || !System.IO.File.Exists(path))
			{
				return NotFound();
			}
			Response.Headers["X-Content-Type-Options"] = "nosniff";
			bool inline = file.ContentType.StartsWith("image/") || file.ContentType == "application/pdf";
			return PhysicalFile(path, file.ContentType, inline ? null : file.FileName);
		}
		default:
			return Redirect($"/{CurrentLanguage}/lead/inbox/list");
		}
	}

	private bool CanViewRequests()
	{
		return Convert.ToBoolean(ViewData["canView"] ?? false);
	}

	[HttpPost]
	public IActionResult Inbox(int id, byte status, string staffNote)
	{
		if (!Statuses.Any(s => s.Value == status))
		{
			return MessageHelper.RedirectAjax("Күйін таңдаңыз.", "error", "", "status");
		}
		using IDbConnection connection = Utilities.GetOpenConnection();
		Consultrequest request = connection.GetList<Consultrequest>("where qStatus = 0 and id = @id", new { id }).FirstOrDefault();
		if (request == null)
		{
			return MessageHelper.RedirectAjax(T("ls_Idoiiw"), "error", "", null);
		}
		request.Status = status;
		request.StaffNote = (staffNote ?? string.Empty).Trim();
		if (request.StaffNote.Length > 4000)
		{
			request.StaffNote = request.StaffNote.Substring(0, 4000);
		}
		request.UpdateTime = UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
		connection.Update(request);
		return MessageHelper.RedirectAjax("Сақталды", "success", "", null);
	}

	[HttpPost]
	public IActionResult GetInboxList(ApiUnifiedModel model)
	{
		int start = Math.Max(0, model.Start);
		int length = model.Length > 0 && model.Length <= 200 ? model.Length : 25;
		string keyword = (model.Keyword ?? string.Empty).Trim();
		using IDbConnection connection = Utilities.GetOpenConnection();
		DynamicParameters args = new DynamicParameters();
		string where = " from consultrequest where qStatus = 0 ";
		if (keyword.Length > 0)
		{
			where += " and (name like @keyword or contact like @keyword or message like @keyword or code like @keyword or serviceTitle like @keyword) ";
			args.Add("keyword", "%" + keyword + "%");
		}
		if (int.TryParse(GetStringQueryParam("filter"), out int status) && status >= 0 && status <= 3 && GetStringQueryParam("filter").Length > 0)
		{
			where += " and status = @status ";
			args.Add("status", status);
		}
		int total = connection.ExecuteScalar<int>("select count(1)" + where, args);
		List<Consultrequest> rows = connection.Query<Consultrequest>("select *" + where + $" order by id desc limit {start}, {length}", args).ToList();
		Dictionary<int, int> files = rows.Count == 0 ? new Dictionary<int, int>()
			: connection.Query<(int, int)>("select requestId, count(1) from consultfile where qStatus = 0 and requestId in @ids group by requestId", new { ids = rows.Select(r => r.Id) }).ToDictionary(x => x.Item1, x => x.Item2);
		var dataList = rows.Select(r => new
		{
			id = r.Id,
			c0 = $"<b>{E(ConsultController.Number(r.Id))}</b><br><small class=\"text-muted\">{E(UnixTimeHelper.UnixTimeToDateTime(r.AddTime).ToString("dd.MM.yyyy HH:mm"))}</small>",
			c1 = $"<b>{E(r.Name)}</b><br><span class=\"text-muted\">{E(r.Contact)}</span>",
			c2 = (string.IsNullOrWhiteSpace(r.Code) ? string.Empty : $"<code>{E(r.Code)}</code> ") + Short(r.ServiceTitle, 60),
			c3 = E(Country(r.Country)) + $"<br><small class=\"text-muted\">{E(KieliWeb.Setup.SiteLanguages.Get(r.Language).Name)}</small>",
			c4 = files.TryGetValue(r.Id, out int n) ? $"<i class=\"ti ti-paperclip\"></i> {n}" : string.Empty,
			c5 = StatusBadge(r.Status)
		}).ToList();
		return MessageHelper.RedirectAjax(T("ls_Searchsuccessful"), "success", "", new { start, length, keyword, total, dataList });
	}

	[HttpPost]
	public IActionResult SetInboxStatus(List<int> idList)
	{
		idList = (idList ?? new List<int>()).Where(x => x > 0).ToList();
		if (idList.Count == 0)
		{
			return MessageHelper.RedirectAjax("Өшіретін өтінімдерді белгілеңіз.", "error", "", null);
		}
		using IDbConnection connection = Utilities.GetOpenConnection();
		connection.Execute("update consultrequest set qStatus = 1, updateTime = @now where id in @idList", new { now = UnixTimeHelper.ConvertToUnixTime(DateTime.Now), idList });
		return MessageHelper.RedirectAjax(T("ls_Deletedsuccessfully"), "success", "", null);
	}
}
