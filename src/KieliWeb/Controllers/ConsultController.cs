using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using COMMON;
using Dapper;
using DBHelper;
using MODEL;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using KieliWeb.Setup;
using Serilog;

namespace KieliWeb.Controllers;

/// <summary>
/// «Кеңес алу» requests from the public site (the drawer and the contact form on the home page).
/// Saved to consultrequest / consultfile; staff read them in the admin under «Өтінімдер».
/// Attached files are kept outside wwwroot and are only served to signed-in administrators.
/// </summary>
[ApiController]
[Route("api/consult")]
public class ConsultController : ControllerBase
{
	public const int MaxFiles = 5;

	public const long MaxFileBytes = 10L * 1024 * 1024;

	private static readonly string[] Countries = { "cn", "kz", "other" };

	private readonly IMemoryCache _memoryCache;

	private readonly IWebHostEnvironment _environment;

	private readonly IConfiguration _configuration;

	public ConsultController(IMemoryCache memoryCache, IWebHostEnvironment environment, IConfiguration configuration)
	{
		_memoryCache = memoryCache;
		_environment = environment;
		_configuration = configuration;
	}

	/// <summary>Folder for the attached documents (Site:PrivateDataPath, default App_Data next to the app).</summary>
	public static string FileRoot(IConfiguration configuration, IWebHostEnvironment environment)
	{
		string root = configuration["Site:PrivateDataPath"];
		if (string.IsNullOrWhiteSpace(root))
		{
			root = "App_Data";
		}
		return Path.Combine(Path.IsPathRooted(root) ? root : Path.Combine(environment.ContentRootPath, root), "consult");
	}

	[HttpPost]
	[RequestSizeLimit(MaxFiles * MaxFileBytes + 1024 * 1024)]
	[RequestFormLimits(MultipartBodyLengthLimit = MaxFiles * MaxFileBytes + 1024 * 1024)]
	public IActionResult Post()
	{
		if (!Request.HasFormContentType)
		{
			return Fail(Ui("errFormat", SiteLanguages.Base));
		}
		IFormCollection form = Request.Form;
		// the visitor's site language: messages back are in it, and staff see it in the inbox
		string lang = SiteLanguages.All.Any(l => l.Culture == (string)form["lang"]) ? (string)form["lang"] : SiteLanguages.Base;
		string ip = ClientIp();
		// bots fill every field, people never see this one: pretend all went well
		if (!string.IsNullOrWhiteSpace(form["website"]))
		{
			return Ok(new { status = "success", message = string.Empty, data = new { number = "BL-" + Random.Shared.Next(10000, 99999) } });
		}
		if (TooMany(ip))
		{
			return Fail(Ui("errTooOften", lang));
		}
		string name = Clean(form["name"], 100);
		string contact = Clean(form["contact"], 100);
		string message = Clean(form["msg"], 3000, multiline: true);
		if (name.Length == 0)
		{
			string nameError = Ui("nameError", lang);
			return Fail(nameError.EndsWith('.') || nameError.EndsWith('。') ? nameError : nameError + (lang == "zh-cn" ? "。" : "."));
		}
		if (contact.Length < 3)
		{
			return Fail(Ui("errContact", lang));
		}
		string consent = form["consent"];
		if (consent != "1" && consent != "on")
		{
			return Fail(Ui("errConsent", lang));
		}
		string country = Countries.Contains((string)form["country"]) ? (string)form["country"] : string.Empty;
		(string serviceKey, string code, string serviceTitle) = ResolveService(form["service"]);

		List<IFormFile> files = form.Files.Where(f => f.Length > 0).ToList();
		if (files.Count > MaxFiles)
		{
			return Fail(Ui("errFileCount", lang, ("n", MaxFiles)));
		}
		foreach (IFormFile file in files)
		{
			if (file.Length > MaxFileBytes)
			{
				return Fail(Ui("errFileSize", lang, ("file", Path.GetFileName(file.FileName))));
			}
			if (FileKind(file) == null)
			{
				return Fail(Ui("errFileType", lang, ("file", Path.GetFileName(file.FileName))));
			}
		}

		int now = UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
		List<string> written = new List<string>();
		try
		{
			using IDbConnection connection = Utilities.GetOpenConnection();
			using IDbTransaction tran = connection.BeginTransaction();
			int requestId = connection.Insert(new Consultrequest
			{
				Name = name,
				Contact = contact,
				Country = country,
				Language = lang,
				ServiceKey = serviceKey,
				Code = code,
				ServiceTitle = serviceTitle,
				Message = message,
				Source = Clean(form["source"], 200),
				Status = 0,
				StaffNote = string.Empty,
				Ip = ip,
				UserAgent = Clean(Request.Headers.UserAgent.ToString(), 255),
				AddTime = now,
				UpdateTime = now,
				QStatus = 0
			}, tran).GetValueOrDefault();
			string folder = Path.Combine(FileRoot(_configuration, _environment), DateTime.Now.ToString("yyyyMM"));
			Directory.CreateDirectory(folder);
			foreach (IFormFile file in files)
			{
				string stored = Path.Combine(DateTime.Now.ToString("yyyyMM"), Guid.NewGuid().ToString("N") + FileKind(file).Value.Extension);
				string path = Path.Combine(FileRoot(_configuration, _environment), stored);
				using (FileStream stream = System.IO.File.Create(path))
				{
					file.CopyTo(stream);
				}
				written.Add(path);
				connection.Insert(new Consultfile
				{
					RequestId = requestId,
					FileName = Clean(Path.GetFileName(file.FileName), 200),
					StoredName = stored.Replace('\\', '/'),
					ContentType = FileKind(file).Value.ContentType,
					FileSize = (int)file.Length,
					AddTime = now,
					QStatus = 0
				}, tran);
			}
			tran.Commit();
			return Ok(new { status = "success", message = Ui("saved", lang), data = new { number = Number(requestId) } });
		}
		catch (Exception exception)
		{
			Log.Error(exception, "Consult request");
			foreach (string path in written)
			{
				try { System.IO.File.Delete(path); } catch (IOException) { }
			}
			return Fail(Ui("errSave", lang));
		}
	}

	public static string Number(int requestId) => "BL-" + requestId.ToString("D5");

	private string Ui(string key, string lang, params (string, object)[] values) => ContentStore.Ui(_memoryCache, "form." + key, lang, values);

	private IActionResult Fail(string message) => Ok(new { status = "error", message, data = (object)null });

	/// <summary>"tab:item" (or "tab:" for a general question) → the code and title staff see.</summary>
	private (string Key, string Code, string Title) ResolveService(string value)
	{
		string[] parts = (value ?? string.Empty).Split(':');
		ContentStore.TabWithItems tab = ContentStore.ServiceTabs(_memoryCache).FirstOrDefault(t => t.Tab.Slug == parts[0]);
		if (tab == null)
		{
			return (string.Empty, string.Empty, "Жалпы кеңес");
		}
		Serviceitem item = parts.Length > 1 ? tab.Items.FirstOrDefault(i => i.Slug == parts[1]) : null;
		return item == null
			? (tab.Tab.Slug + ":", tab.Tab.Code, tab.Tab.Name + " — жалпы кеңес")
			: (tab.Tab.Slug + ":" + item.Slug, item.Code, item.Title);
	}

	private bool TooMany(string ip)
	{
		string key = "consult_rate_" + ip;
		int count = _memoryCache.TryGetValue(key, out int n) ? n : 0;
		if (count >= 5)
		{
			return true;
		}
		_memoryCache.Set(key, count + 1, TimeSpan.FromMinutes(10));
		return false;
	}

	private string ClientIp()
	{
		string real = Request.Headers["X-Real-IP"].ToString();
		return Clean(string.IsNullOrWhiteSpace(real) ? HttpContext.Connection.RemoteIpAddress?.ToString() : real, 64);
	}

	private static string Clean(string value, int max, bool multiline = false)
	{
		string s = (value ?? string.Empty).Trim();
		s = new string(s.Where(c => !char.IsControl(c) || (multiline && (c == '\n' || c == '\r'))).ToArray());
		return s.Length > max ? s.Substring(0, max) : s;
	}

	/// <summary>The file type from its first bytes, not from its name: JPEG, PNG, WEBP, HEIC or PDF.</summary>
	private static (string Extension, string ContentType)? FileKind(IFormFile file)
	{
		byte[] head = new byte[12];
		using (Stream s = file.OpenReadStream())
		{
			if (s.Read(head, 0, head.Length) < 4)
			{
				return null;
			}
		}
		if (head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
		{
			return (".jpg", "image/jpeg");
		}
		if (head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47)
		{
			return (".png", "image/png");
		}
		if (head[0] == 0x25 && head[1] == 0x50 && head[2] == 0x44 && head[3] == 0x46)
		{
			return (".pdf", "application/pdf");
		}
		string riff = System.Text.Encoding.ASCII.GetString(head, 0, 4), webp = System.Text.Encoding.ASCII.GetString(head, 8, 4);
		if (riff == "RIFF" && webp == "WEBP")
		{
			return (".webp", "image/webp");
		}
		string ftyp = System.Text.Encoding.ASCII.GetString(head, 4, 8);
		if (ftyp.StartsWith("ftyp") && new[] { "heic", "heix", "mif1", "msf1", "hevc" }.Contains(ftyp.Substring(4)))
		{
			return (".heic", "image/heic");
		}
		return null;
	}
}
