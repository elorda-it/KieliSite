using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using COMMON;
using Dapper;
using DBHelper;
using MODEL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using KieliWeb.Attributes;
using KieliWeb.Setup;
using KieliWeb.Setup.Cms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using SkiaSharp;

namespace KieliWeb.Controllers;

/// <summary>
/// Admin «Беттер мен мәтіндер»: every text, image and link of the site pages, as page blocks
/// (definitions in Setup/BlockRegistry*.cs, values in the pageblock table). Also the image
/// and audio uploads used by all admin forms.
/// </summary>
[Authorize(Roles = "Admin")]
public class ContentController : QarBaseController
{
	private const long MaxImageBytes = 10L * 1024 * 1024;

	private const int MaxImageWidth = 2400;

	private const long MaxAudioBytes = 25L * 1024 * 1024;

	/// <summary>Where each page of the site is, for «Сайтта көру».</summary>
	private static readonly Dictionary<string, string> PageUrls = new Dictionary<string, string>
	{
		["site"] = "/", ["home"] = "/", ["author"] = "/kz/author", ["services"] = "/kz/services", ["news"] = "/kz/news",
		["kazakhstan"] = "/kz/kazakhstan", ["kaztest"] = "/kz/kaztest"
	};

	public ContentController(IMemoryCache memoryCache, IWebHostEnvironment environment)
		: base(memoryCache, environment)
	{
	}

	public IActionResult Block(string query)
	{
		query = (query ?? string.Empty).Trim().ToLower();
		ViewData["query"] = query;
		ViewData["cms"] = true;
		switch (query)
		{
		case "list":
			ViewData["title"] = "Бет блоктары";
			ViewData["pageUrls"] = PageUrls;
			return View("~/Views/Console/Content/Blocks.cshtml");
		case "edit":
		{
			BlockDef def = BlockRegistry.Find(GetStringQueryParam("key"));
			if (def == null)
			{
				return Redirect($"/{CurrentLanguage}/content/block/list");
			}
			Pageblock row = EnsureRow(def);
			ViewData["title"] = def.Title;
			string lang = EditLanguage();
			JObject values = ContentStore.BaseBlock(_memoryCache, def.Key);
			bool translatable = def.Fields.Any(f => f.Translatable);
			string editUrl = $"/{CurrentLanguage}/content/block/edit?key={def.Key}";
			List<LanguageTab> tabs = !translatable ? new List<LanguageTab>() : SiteLanguages.All.Select(l => new LanguageTab(l.Culture, l.Name,
				SiteLanguages.IsBase(l.Culture) ? 100 : TranslationStore.Percent(FieldValues.Coverage(def.Fields, values, ContentStore.BlockTranslation(_memoryCache, def.Key, l.Culture))),
				SiteLanguages.IsBase(l.Culture) ? editUrl : editUrl + "&lang=" + l.Culture, l.Culture == lang)).ToList();
			bool translate = translatable && !SiteLanguages.IsBase(lang);
			string url = PageUrls.TryGetValue(def.Page, out string pageUrl) ? pageUrl : "/";
			return View("~/Views/Console/Cms/Edit.cshtml", new CmsEditModel
			{
				Group = BlockRegistry.PageTitle(def.Page),
				ListTitle = "Бет блоктары",
				ListUrl = $"/{CurrentLanguage}/content/block/list#page-{def.Page}",
				Heading = BlockRegistry.PageTitle(def.Page) + " · " + def.Title + (translate ? " · " + SiteLanguages.Get(lang).Name : string.Empty),
				Help = def.Help,
				FormAction = $"/{CurrentLanguage}/Content/Block",
				RowId = row.Id,
				Key = def.Key,
				Fields = def.Fields,
				Values = translate ? TranslationStore.Align(def.Fields, values, ContentStore.BlockTranslation(_memoryCache, def.Key, lang)) : values,
				BaseValues = translate ? values : null,
				Lang = translate ? lang : SiteLanguages.Base,
				Tabs = tabs,
				PublicUrl = SiteLanguages.Localize(url, translate ? lang : SiteLanguages.Base),
				ResetUrl = $"/{CurrentLanguage}/Content/SetBlockStatus"
			});
		}
		default:
			return Redirect($"/{CurrentLanguage}/content/block/list");
		}
	}

	[HttpPost]
	public IActionResult Block(int id, string key, string dataJson, string lang)
	{
		BlockDef def = BlockRegistry.Find(key);
		if (def == null)
		{
			return MessageHelper.RedirectAjax(T("ls_Idoiiw"), "error", "", null);
		}
		JObject posted;
		try
		{
			posted = JObject.Parse(string.IsNullOrWhiteSpace(dataJson) ? "{}" : dataJson);
		}
		catch (JsonException)
		{
			return MessageHelper.RedirectAjax("Форма деректері оқылмады. Бетті жаңартып, қайталаңыз.", "error", "", null);
		}
		using IDbConnection connection = Utilities.GetOpenConnection();
		Pageblock row = connection.GetList<Pageblock>("where qStatus = 0 and id = @id and blockKey = @key", new { id, key = def.Key }).FirstOrDefault();
		if (row == null)
		{
			return MessageHelper.RedirectAjax(T("ls_Idoiiw"), "error", "", null);
		}
		lang = (lang ?? string.Empty).Trim().ToLowerInvariant();
		if (SiteLanguages.Translations.Any(l => l.Culture == lang))
		{
			// a translation: only its texts are stored; images, links and the rows themselves stay the Kazakh ones
			JObject translation = FieldValues.CleanTranslation(def.Fields, posted);
			TranslationStore.Save(connection, "pageblock", row.Id, "dataJson", lang, translation.HasValues ? translation.ToString(Formatting.None) : null);
			ContentStore.Clear(_memoryCache);
			return MessageHelper.RedirectAjax("Аударма сақталды — сайтта бірден көрінеді", "success", "", null);
		}
		List<string> errors = new List<string>();
		JObject clean = FieldValues.Clean(def.Fields, posted, errors);
		if (errors.Count > 0)
		{
			return MessageHelper.RedirectAjax(string.Join(" ", errors.Take(3)), "error", "", null);
		}
		row.DataJson = clean.ToString(Formatting.None);
		row.UpdateTime = UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
		connection.Update(row);
		ContentStore.Clear(_memoryCache);
		return MessageHelper.RedirectAjax("Сақталды — сайтта бірден көрінеді", "success", "", null);
	}

	/// <summary>«Бастапқы мәтінге қайтару»: the block gets the prototype values back (needs the delete permission).</summary>
	[HttpPost]
	public IActionResult SetBlockStatus(string manageType, List<int> idList)
	{
		if (manageType != "reset" || idList == null || idList.Count == 0)
		{
			return MessageHelper.RedirectAjax(T("ls_Managetypeerror"), "error", "", null);
		}
		using IDbConnection connection = Utilities.GetOpenConnection();
		foreach (Pageblock row in connection.GetList<Pageblock>("where qStatus = 0 and id in @idList", new { idList }))
		{
			row.DataJson = ContentStore.DefaultBlock(row.BlockKey).ToString(Formatting.None);
			row.UpdateTime = UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
			connection.Update(row);
		}
		ContentStore.Clear(_memoryCache);
		return MessageHelper.RedirectAjax("Бастапқы мәтін қайтарылды", "success", "", null);
	}

	private string EditLanguage()
	{
		string lang = (Request.Query["lang"].ToString() ?? string.Empty).Trim().ToLowerInvariant();
		return SiteLanguages.Translations.Any(l => l.Culture == lang) ? lang : SiteLanguages.Base;
	}

	private Pageblock EnsureRow(BlockDef def)
	{
		using IDbConnection connection = Utilities.GetOpenConnection();
		Pageblock row = connection.GetList<Pageblock>("where qStatus = 0 and blockKey = @key", new { key = def.Key }).FirstOrDefault();
		if (row != null)
		{
			return row;
		}
		int now = UnixTimeHelper.ConvertToUnixTime(DateTime.Now);
		row = new Pageblock { BlockKey = def.Key, DataJson = ContentStore.DefaultBlock(def.Key).ToString(Formatting.None), AddTime = now, UpdateTime = now, QStatus = 0 };
		row.Id = connection.Insert(row).GetValueOrDefault();
		return row;
	}

	/// <summary>Images for every admin form and the text editor: JPG, PNG, WEBP, GIF, AVIF up to 10 MB, wide photos scaled to 2400 px.</summary>
	[NoRole]
	[HttpPost]
	[RequestSizeLimit(MaxImageBytes + 1024 * 1024)]
	public IActionResult Upload(IFormFile file)
	{
		if (file == null || file.Length == 0)
		{
			return MessageHelper.RedirectAjax("Файл таңдалмады.", "error", "", null);
		}
		if (file.Length > MaxImageBytes)
		{
			return MessageHelper.RedirectAjax("Сурет 10 МБ-тан аспауы керек.", "error", "", null);
		}
		string ext = ImageExtension(file);
		if (ext == null)
		{
			return MessageHelper.RedirectAjax("Тек JPG, PNG, WEBP, GIF не AVIF суреті жүктеледі.", "error", "", null);
		}
		string relative = "/uploads/images/" + DateTime.Now.ToString("yyyyMM") + "/" + Guid.NewGuid().ToString("N").Substring(0, 16) + ext;
		string path = Path.Combine(_environment.WebRootPath, relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		try
		{
			if (!Downscale(file, ext, path))
			{
				using FileStream stream = System.IO.File.Create(path);
				file.CopyTo(stream);
			}
		}
		catch (Exception exception)
		{
			Log.Error(exception, "Image upload");
			return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", null);
		}
		return MessageHelper.RedirectAjax("Сурет жүктелді", "success", "", new { url = relative });
	}

	/// <summary>Audio for the ҚАЗТЕСТ listening recordings: MP3, M4A, OGG, WAV or WEBM up to 25 MB, stored as uploaded.</summary>
	[NoRole]
	[HttpPost]
	[RequestSizeLimit(MaxAudioBytes + 1024 * 1024)]
	public IActionResult UploadAudio(IFormFile file)
	{
		if (file == null || file.Length == 0)
		{
			return MessageHelper.RedirectAjax("Файл таңдалмады.", "error", "", null);
		}
		if (file.Length > MaxAudioBytes)
		{
			return MessageHelper.RedirectAjax("Аудио 25 МБ-тан аспауы керек. MP3 түрінде сақтап көріңіз.", "error", "", null);
		}
		string ext = AudioExtension(file);
		if (ext == null)
		{
			return MessageHelper.RedirectAjax("Тек MP3, M4A, OGG, WAV не WEBM аудиосы жүктеледі.", "error", "", null);
		}
		string relative = "/uploads/audio/" + DateTime.Now.ToString("yyyyMM") + "/" + Guid.NewGuid().ToString("N").Substring(0, 16) + ext;
		string path = Path.Combine(_environment.WebRootPath, relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		try
		{
			using FileStream stream = System.IO.File.Create(path);
			file.CopyTo(stream);
		}
		catch (Exception exception)
		{
			Log.Error(exception, "Audio upload");
			return MessageHelper.RedirectAjax(T("ls_Savefailed"), "error", "", null);
		}
		return MessageHelper.RedirectAjax("Аудио жүктелді", "success", "", new { url = relative });
	}

	/// <summary>The audio type from the file's first bytes.</summary>
	private static string AudioExtension(IFormFile file)
	{
		byte[] h = new byte[12];
		using (Stream s = file.OpenReadStream())
		{
			if (s.Read(h, 0, h.Length) < 12)
			{
				return null;
			}
		}
		// MP3: an ID3 tag, or straight away an MPEG audio frame
		if ((h[0] == 'I' && h[1] == 'D' && h[2] == '3') || (h[0] == 0xFF && (h[1] & 0xE0) == 0xE0))
		{
			return ".mp3";
		}
		if (h[4] == 'f' && h[5] == 't' && h[6] == 'y' && h[7] == 'p')
		{
			return ".m4a";
		}
		if (h[0] == 'O' && h[1] == 'g' && h[2] == 'g' && h[3] == 'S')
		{
			return ".ogg";
		}
		if (h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'A' && h[10] == 'V' && h[11] == 'E')
		{
			return ".wav";
		}
		if (h[0] == 0x1A && h[1] == 0x45 && h[2] == 0xDF && h[3] == 0xA3)
		{
			return ".webm";
		}
		return null;
	}

	/// <summary>Photos wider than 2400 px are scaled down (the site is read on phones, often in China).</summary>
	private static bool Downscale(IFormFile file, string ext, string path)
	{
		if (ext != ".jpg" && ext != ".png" && ext != ".webp")
		{
			return false;
		}
		try
		{
			using Stream input = file.OpenReadStream();
			using SKBitmap bitmap = SKBitmap.Decode(input);
			if (bitmap == null || bitmap.Width <= MaxImageWidth)
			{
				return false;
			}
			int height = (int)Math.Round(bitmap.Height * (double)MaxImageWidth / bitmap.Width);
			using SKBitmap scaled = bitmap.Resize(new SKImageInfo(MaxImageWidth, height), SKFilterQuality.High);
			if (scaled == null)
			{
				return false;
			}
			using SKImage image = SKImage.FromBitmap(scaled);
			SKEncodedImageFormat format = ext == ".png" ? SKEncodedImageFormat.Png : ext == ".webp" ? SKEncodedImageFormat.Webp : SKEncodedImageFormat.Jpeg;
			using SKData data = image.Encode(format, 85);
			if (data == null)
			{
				return false;
			}
			using FileStream output = System.IO.File.Create(path);
			data.SaveTo(output);
			return true;
		}
		catch (Exception exception) when (exception is DllNotFoundException or TypeInitializationException)
		{
			// no native Skia on this server: keep the original
			return false;
		}
	}

	/// <summary>The image type from the file's first bytes.</summary>
	private static string ImageExtension(IFormFile file)
	{
		byte[] h = new byte[12];
		using (Stream s = file.OpenReadStream())
		{
			if (s.Read(h, 0, h.Length) < 12)
			{
				return null;
			}
		}
		if (h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF)
		{
			return ".jpg";
		}
		if (h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47)
		{
			return ".png";
		}
		if (h[0] == 'G' && h[1] == 'I' && h[2] == 'F')
		{
			return ".gif";
		}
		if (h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P')
		{
			return ".webp";
		}
		if (h[4] == 'f' && h[5] == 't' && h[6] == 'y' && h[7] == 'p' && h[8] == 'a' && h[9] == 'v' && h[10] == 'i')
		{
			return ".avif";
		}
		return null;
	}
}
