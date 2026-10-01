using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MODEL;
using Serilog;
using SkiaSharp;

namespace COMMON;

public static class HtmlAgilityPackHelper
{
	private static readonly string[] Base64ImagePrefixs = new string[7] { "data:image/png;base64,", "data:image/jpeg;base64,", "data:image/gif;base64,", "data:image/svg+xml;base64,", "data:image/bmp;base64,", "data:image/x-icon;base64,", "data:image/webp;base64," };

	private static readonly Regex CyrillicRegex = new Regex("[А-Яа-яӘәҒғҚқҢңӨөҰұҮүҺһІіЁё]+", RegexOptions.Compiled);

	public static string ConvertHtmlTextNode(string html, string language, string userAgent, string qUrl)
	{
		HtmlDocument document = new HtmlDocument();
		document.LoadHtml(html);
		document.DocumentNode.SelectSingleNode("/html")?.SetAttributeValue("lang", "kk");
		HtmlNodeCollection staticTextNodes = document.DocumentNode.SelectNodes("//*[contains(@rel,'qar-static-text')]/text()[normalize-space(.) != '']");
		HtmlNodeCollection scriptTextNodes = document.DocumentNode.SelectNodes("//script/text()[normalize-space(.) != '']");
		HtmlNodeCollection textNodes = document.DocumentNode.SelectNodes("//text()[normalize-space(.) != '']");
		if (textNodes != null)
		{
			foreach (HtmlNode node in (IEnumerable<HtmlNode>)textNodes)
			{
				if ((staticTextNodes != null && staticTextNodes.Contains(node)) || (scriptTextNodes != null && scriptTextNodes.Contains(node)))
				{
					continue;
				}
				string innerHtml = WebUtility.HtmlDecode(node.InnerHtml);
				if (!(language == "tote"))
				{
					if (language == "latyn")
					{
						node.InnerHtml = Cyrl2LatynHelper.Cyrl2Latyn(innerHtml);
					}
				}
				else
				{
					node.InnerHtml = Cyrl2ToteHelper.Cyrl2Tote(innerHtml);
				}
			}
		}
		HtmlNodeCollection inputNodes = document.DocumentNode.SelectNodes("//input[contains(@type,'text')]|//textarea");
		if (inputNodes != null)
		{
			foreach (HtmlNode node2 in (IEnumerable<HtmlNode>)inputNodes)
			{
				string placeholder = ((node2.Attributes["placeholder"] != null) ? node2.Attributes["placeholder"].Value : string.Empty);
				if (string.IsNullOrEmpty(placeholder) || string.IsNullOrEmpty(placeholder = placeholder.Trim()))
				{
					continue;
				}
				if (!(language == "tote"))
				{
					if (language == "latyn")
					{
						placeholder = Cyrl2LatynHelper.Cyrl2Latyn(placeholder);
					}
				}
				else
				{
					placeholder = Cyrl2ToteHelper.Cyrl2Tote(placeholder);
				}
				node2.SetAttributeValue("placeholder", placeholder);
			}
		}
		HtmlNodeCollection textareaNodes = document.DocumentNode.SelectNodes("//textarea");
		if (textareaNodes != null)
		{
			foreach (HtmlNode node3 in (IEnumerable<HtmlNode>)textareaNodes)
			{
				string innerHtml2 = WebUtility.HtmlDecode(node3.InnerHtml);
				if (!(language == "tote"))
				{
					if (language == "latyn")
					{
						node3.InnerHtml = Cyrl2LatynHelper.Cyrl2Latyn(innerHtml2);
					}
				}
				else
				{
					node3.InnerHtml = Cyrl2ToteHelper.Cyrl2Tote(innerHtml2);
				}
			}
		}
		HtmlNodeCollection metaNodes = document.DocumentNode.SelectNodes("//meta[contains(@name,'keywords')]|//meta[contains(@name,'description')]|//meta[contains(@name,'title')]|//meta[contains(@name,'site_name')]\n                                                                                   |//meta[contains(@property,'description')]|//meta[contains(@property,'title')]|//meta[contains(@property,'site_name')]");
		if (metaNodes != null)
		{
			foreach (HtmlNode node4 in (IEnumerable<HtmlNode>)metaNodes)
			{
				string content = ((node4.Attributes["content"] != null) ? node4.Attributes["content"].Value : string.Empty);
				if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(content = content.Trim()))
				{
					continue;
				}
				if (!(language == "tote"))
				{
					if (language == "latyn")
					{
						content = Cyrl2LatynHelper.Cyrl2Latyn(content);
					}
				}
				else
				{
					content = Cyrl2ToteHelper.Cyrl2Tote(content);
				}
				node4.SetAttributeValue("content", content);
			}
		}
		HtmlNodeCollection imgNodes = document.DocumentNode.SelectNodes("//img");
		if (imgNodes != null)
		{
			foreach (HtmlNode node5 in (IEnumerable<HtmlNode>)imgNodes)
			{
				string alt = ((node5.Attributes["alt"] != null) ? node5.Attributes["alt"].Value : string.Empty);
				string dataCopyright = ((node5.Attributes["data-copyright"] != null) ? node5.Attributes["data-copyright"].Value : string.Empty);
				if (!(language == "tote"))
				{
					if (language == "latyn")
					{
						alt = Cyrl2LatynHelper.Cyrl2Latyn(alt);
						dataCopyright = Cyrl2LatynHelper.Cyrl2Latyn(dataCopyright);
					}
				}
				else
				{
					alt = Cyrl2ToteHelper.Cyrl2Tote(alt);
					dataCopyright = Cyrl2ToteHelper.Cyrl2Tote(dataCopyright);
				}
				node5.SetAttributeValue("alt", alt);
				if (!string.IsNullOrEmpty(dataCopyright))
				{
					node5.SetAttributeValue("data-copyright", dataCopyright);
				}
			}
		}
		HtmlNodeCollection staticANodes = document.DocumentNode.SelectNodes("//a[contains(@rel,'ankui-static-text')]");
		HtmlNodeCollection aNodes = document.DocumentNode.SelectNodes("//a");
		if (aNodes != null)
		{
			foreach (HtmlNode node6 in (IEnumerable<HtmlNode>)aNodes)
			{
				if (staticANodes == null || !staticANodes.Contains(node6))
				{
					string href = ((node6.Attributes["href"] != null) ? node6.Attributes["href"].Value : string.Empty);
					if (!string.IsNullOrEmpty(href) && !string.IsNullOrEmpty(href = href.Trim()) && href.Substring(0, 1).Equals("/"))
					{
						node6.SetAttributeValue("href", qUrl + href);
					}
				}
			}
		}
		return document.DocumentNode.OuterHtml;
	}

	public static void ConvertObjectCyrillicLetters(object obj, string language)
	{
		bool flag = obj == null;
		if (!flag)
		{
			bool flag2 = ((language == "latyn" || language == "tote") ? true : false);
			flag = !flag2;
		}
		if (!flag)
		{
			HashSet<object> visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
			ConvertRecursive(obj, language, visited);
		}
	}

	private static void ConvertRecursive(object obj, string lang, HashSet<object> visited)
	{
		if (obj == null || !visited.Add(obj))
		{
			return;
		}
		if (obj is IEnumerable e && !(obj is string))
		{
			foreach (object item in e)
			{
				ConvertRecursive(item, lang, visited);
			}
			return;
		}
		Type type = obj.GetType();
		bool isAnonymous = Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), inherit: false) && type.IsGenericType && type.Name.Contains("AnonymousType");
		PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
		foreach (PropertyInfo p in properties)
		{
			if (p.GetIndexParameters().Length != 0 || !p.CanRead)
			{
				continue;
			}
			object raw;
			try
			{
				raw = p.GetValue(obj);
			}
			catch (TargetParameterCountException)
			{
				continue;
			}
			if (raw == null)
			{
				continue;
			}
			if (!(raw is string s) || !CyrillicRegex.IsMatch(s))
			{
				if (!raw.GetType().IsPrimitive && !raw.GetType().IsEnum && !raw.GetType().IsValueType)
				{
					ConvertRecursive(raw, lang, visited);
				}
				continue;
			}
			string text = ((lang == "latyn") ? Cyrl2LatynHelper.Cyrl2Latyn(s) : ((!(lang == "tote")) ? s : Cyrl2ToteHelper.Cyrl2Tote(s)));
			string converted = text;
			if (!isAnonymous && p.CanWrite)
			{
				try
				{
					p.SetValue(obj, converted);
				}
				catch
				{
					goto IL_014f;
				}
				continue;
			}
			goto IL_014f;
			IL_014f:
			if (!isAnonymous)
			{
				FieldInfo fld = type.GetField("<" + p.Name + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
				if ((object)fld != null && !fld.IsInitOnly && fld.FieldType == typeof(string))
				{
					fld.SetValue(obj, converted);
				}
			}
		}
	}

	public static string GetHtmlBoyInnerHtml(string html)
	{
		if (string.IsNullOrWhiteSpace(html))
		{
			return string.Empty;
		}
		HtmlDocument htmlDocument = new HtmlDocument();
		htmlDocument.LoadHtml(html);
		HtmlNode bodyNode = htmlDocument.DocumentNode.SelectSingleNode("//body");
		if (bodyNode != null)
		{
			return bodyNode.InnerHtml;
		}
		return html;
	}

	public static bool HtmlContentIsEmpty(string html)
	{
		HtmlDocument htmlDocument = new HtmlDocument();
		htmlDocument.LoadHtml("<body>" + html + "</body>");
		string innerText = htmlDocument.DocumentNode.SelectSingleNode("//body").InnerText;
		if (!string.IsNullOrEmpty(innerText))
		{
			return string.IsNullOrEmpty(innerText.Trim());
		}
		return true;
	}

	public static string GetShortDescription(string fullDescription, int length = 200)
	{
		if (string.IsNullOrWhiteSpace(fullDescription))
		{
			return string.Empty;
		}
		try
		{
			HtmlDocument htmlDocument = new HtmlDocument();
			htmlDocument.LoadHtml(fullDescription);
			HtmlNode shortNode = htmlDocument.DocumentNode.ChildNodes.FirstOrDefault();
			string shortDescription = WebUtility.HtmlDecode(shortNode?.InnerText ?? string.Empty).Trim();
			while (string.IsNullOrWhiteSpace(shortDescription) || shortDescription.Length < 20)
			{
				shortNode = shortNode?.NextSibling;
				if (shortNode == null)
				{
					break;
				}
				shortDescription = shortNode.InnerText ?? string.Empty;
				shortDescription = WebUtility.HtmlDecode(shortDescription).Trim();
			}
			if (shortDescription.Length > length)
			{
				shortDescription = shortDescription.Substring(0, length - 3);
				int lastWhitespaceIndex = shortDescription.LastIndexOf(" ", StringComparison.Ordinal);
				if (lastWhitespaceIndex > 0)
				{
					shortDescription = shortDescription.Substring(0, lastWhitespaceIndex);
				}
				if (new string[9] { ",", "?", "!", ":", ".", " ", "\"", "%", "'" }.Any((string x) => x.Equals(shortDescription[shortDescription.Length - 1])))
				{
					string text = shortDescription;
					shortDescription = text.Substring(0, text.Length - 2);
				}
				shortDescription += "...";
			}
			return shortDescription;
		}
		catch
		{
			return string.Empty;
		}
	}

	public static List<string> GetMediaPathList(string fullDescription)
	{
		List<string> mediaPathList = new List<string>();
		if (string.IsNullOrWhiteSpace(fullDescription))
		{
			return mediaPathList;
		}
		try
		{
			HtmlDocument htmlDocument = new HtmlDocument();
			htmlDocument.LoadHtml(fullDescription);
			HtmlNodeCollection imgNodes = htmlDocument.DocumentNode.SelectNodes("//img");
			if (imgNodes != null)
			{
				foreach (HtmlNode imgNode in (IEnumerable<HtmlNode>)imgNodes)
				{
					string src = ((imgNode.Attributes["src"] != null) ? imgNode.Attributes["src"].Value : string.Empty);
					if (!string.IsNullOrEmpty(src))
					{
						mediaPathList.Add(src);
					}
				}
			}
			HtmlNodeCollection videoNodes = htmlDocument.DocumentNode.SelectNodes("//video");
			if (videoNodes != null)
			{
				foreach (HtmlNode videoNode in (IEnumerable<HtmlNode>)videoNodes)
				{
					string src2 = ((videoNode.Attributes["src"] != null) ? videoNode.Attributes["src"].Value : string.Empty);
					if (!string.IsNullOrEmpty(src2))
					{
						mediaPathList.Add(src2);
					}
					HtmlNode videoNodeSourceNode = videoNode.SelectSingleNode("./source");
					if (videoNodeSourceNode != null)
					{
						src2 = ((videoNodeSourceNode.Attributes["src"] != null) ? videoNodeSourceNode.Attributes["src"].Value : string.Empty);
						if (!string.IsNullOrEmpty(src2))
						{
							mediaPathList.Add(src2);
						}
					}
				}
			}
			HtmlNodeCollection audioNodes = htmlDocument.DocumentNode.SelectNodes("//audio");
			if (audioNodes != null)
			{
				foreach (HtmlNode audioNode in (IEnumerable<HtmlNode>)audioNodes)
				{
					string src3 = ((audioNode.Attributes["src"] != null) ? audioNode.Attributes["src"].Value : string.Empty);
					if (!string.IsNullOrEmpty(src3))
					{
						mediaPathList.Add(src3);
					}
					HtmlNode audioNodeSourceNode = audioNode.SelectSingleNode("./source");
					if (audioNodeSourceNode != null)
					{
						src3 = ((audioNodeSourceNode.Attributes["src"] != null) ? audioNodeSourceNode.Attributes["src"].Value : string.Empty);
						if (!string.IsNullOrEmpty(src3))
						{
							mediaPathList.Add(src3);
						}
					}
				}
			}
			HtmlNodeCollection pdfNodes = htmlDocument.DocumentNode.SelectNodes("//a[@data-pdf]");
			if (pdfNodes != null)
			{
				foreach (HtmlNode pdfNode in (IEnumerable<HtmlNode>)pdfNodes)
				{
					string href = ((pdfNode.Attributes["href"] != null) ? pdfNode.Attributes["href"].Value : string.Empty);
					if (!string.IsNullOrEmpty(href))
					{
						mediaPathList.Add(href);
					}
				}
			}
			return mediaPathList;
		}
		catch
		{
			return mediaPathList;
		}
	}

	public static bool IsIframe(string embedCode)
	{
		HtmlDocument htmlDocument = new HtmlDocument();
		htmlDocument.LoadHtml(embedCode);
		return htmlDocument.DocumentNode.SelectSingleNode("//iframe") != null;
	}

	public static bool CheckSocialEmbedCode(string embedCode)
	{
		string[] allowDomains = new string[15]
		{
			"telegram.org", "www.telegram.org", "instagram.com", "www.instagram.com", "facebook.com", "www.facebook.com", "youtube.com", "www.youtube.com", "tiktok.com", "www.tiktok.com",
			"www.x.com", "x.com", "www.twitter.com", "twitter.com", "platform.twitter.com"
		};
		HtmlDocument htmlDocument = new HtmlDocument();
		htmlDocument.LoadHtml(embedCode);
		HtmlNodeCollection scriptNodes = htmlDocument.DocumentNode.SelectNodes("//script");
		if (scriptNodes != null && scriptNodes.Count > 0)
		{
			foreach (HtmlNode scriptNode in (IEnumerable<HtmlNode>)scriptNodes)
			{
				string urlString = ((scriptNode.Attributes["src"] != null) ? scriptNode.Attributes["src"].Value : string.Empty);
				urlString = (urlString.StartsWith("//") ? ("https:" + urlString) : urlString);
				if (!urlString.StartsWith("http://") && !urlString.StartsWith("https://"))
				{
					urlString = "https://" + urlString;
				}
				string host = new Uri(urlString).Host.ToLower();
				if (!allowDomains.Contains(host))
				{
					return false;
				}
				if (!string.IsNullOrWhiteSpace(scriptNode.InnerHtml ?? string.Empty))
				{
					return false;
				}
			}
		}
		return true;
	}

	private static async Task<string> GetHtmlWebAsync(string url)
	{
		using HttpResponseMessage response = await new HttpClient().GetAsync(url);
		using HttpContent content = response.Content;
		return await content.ReadAsStringAsync();
	}

	public static async Task<Admin> DownloadAdminAvatar(Admin admin, string siteUrl, string directoryPath)
	{
		if (admin == null)
		{
			return null;
		}
		List<string> savedFilePathList = new List<string>();
		try
		{
			if (!string.IsNullOrWhiteSpace(admin.AvatarUrl))
			{
				try
				{
					string size = string.Empty;
					string fileName = Path.GetFileNameWithoutExtension(admin.AvatarUrl);
					if (fileName.EndsWith("_big", StringComparison.OrdinalIgnoreCase))
					{
						size = "_big.";
					}
					else if (fileName.EndsWith("_middle", StringComparison.OrdinalIgnoreCase))
					{
						size = "_middle.";
					}
					else if (fileName.EndsWith("_small", StringComparison.OrdinalIgnoreCase))
					{
						size = "_small.";
					}
					if (!string.IsNullOrWhiteSpace(size))
					{
						string bigPath = PathHelper.Combine(directoryPath, admin.AvatarUrl.Replace(size, "_big."));
						string middlePath = PathHelper.Combine(directoryPath, admin.AvatarUrl.Replace(size, "_middle."));
						string smallPath = PathHelper.Combine(directoryPath, admin.AvatarUrl.Replace(size, "_small."));
						savedFilePathList.Add(bigPath);
						savedFilePathList.Add(middlePath);
						savedFilePathList.Add(smallPath);
						await DownloadFileAsync(GetFullUrl(admin.AvatarUrl.Replace(size, "_big."), siteUrl), bigPath);
						await DownloadFileAsync(GetFullUrl(admin.AvatarUrl.Replace(size, "_middle."), siteUrl), middlePath);
						await DownloadFileAsync(GetFullUrl(admin.AvatarUrl.Replace(size, "_small."), siteUrl), smallPath);
					}
					else
					{
						string absPath = PathHelper.Combine(directoryPath, admin.AvatarUrl);
						savedFilePathList.Add(absPath);
						await DownloadFileAsync(GetFullUrl(admin.AvatarUrl, siteUrl), absPath);
					}
				}
				catch (Exception)
				{
					admin.QStatus = 6;
				}
			}
			return admin;
		}
		catch (Exception exception)
		{
			Log.Error(exception, "DownloadAdminAvatar");
			foreach (string filePath in savedFilePathList)
			{
				if (File.Exists(filePath))
				{
					File.Delete(filePath);
				}
			}
			return null;
		}
		finally
		{
			if (admin.QStatus == 6)
			{
				foreach (string filePath2 in savedFilePathList)
				{
					if (File.Exists(filePath2))
					{
						File.Delete(filePath2);
					}
				}
			}
		}
	}

	private static async Task DownloadFileAsync(string imageUrl, string savePath)
	{
		if (savePath.Contains("http", StringComparison.OrdinalIgnoreCase) || File.Exists(savePath))
		{
			return;
		}
		if (!Directory.Exists(Path.GetDirectoryName(savePath)))
		{
			Directory.CreateDirectory(Path.GetDirectoryName(savePath) ?? string.Empty);
		}
		using HttpClient httpClient = new HttpClient();
		HttpResponseMessage obj = await httpClient.GetAsync(imageUrl);
		obj.EnsureSuccessStatusCode();
		await File.WriteAllBytesAsync(savePath, await obj.Content.ReadAsByteArrayAsync());
	}

	private static string GetFullUrl(string url, string siteUrl)
	{
		url = (url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : (siteUrl + url));
		string[] uArr = url.Split("://");
		string newUrl = "";
		for (int i = 0; i < uArr.Length; i++)
		{
			newUrl = newUrl + uArr[i].Replace("//", "/") + ((i == uArr.Length - 1) ? "" : "://");
		}
		return newUrl;
	}

	private static void SaveImageFromBase64(string base64String, string filePath)
	{
		FileHelper.EnsureDir(filePath);
		string[] base64ImagePrefixs = Base64ImagePrefixs;
		foreach (string base64ImagePrefix in base64ImagePrefixs)
		{
			base64String = base64String.Replace(base64ImagePrefix, "");
		}
		using MemoryStream ms = new MemoryStream(Convert.FromBase64String(base64String));
		using SKBitmap bitmap = SKBitmap.Decode(ms);
		using SKImage image = SKImage.FromBitmap(bitmap);
		using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
		using FileStream stream = File.OpenWrite(filePath);
		data.SaveTo(stream);
	}

	public static bool HasVideo(string html)
	{
		HtmlDocument htmlDocument = new HtmlDocument();
		htmlDocument.LoadHtml(html);
		HtmlNode iframeNode = htmlDocument.DocumentNode.SelectSingleNode("//iframe");
		return ((iframeNode != null) ? iframeNode.GetAttributeValue("src", string.Empty) : string.Empty).Contains("youtube");
	}

	private static string GetYoutubeUrl(string html)
	{
		if (string.IsNullOrWhiteSpace(html))
		{
			return string.Empty;
		}
		HtmlDocument htmlDocument = new HtmlDocument();
		htmlDocument.LoadHtml(html);
		HtmlNode iframeNode = htmlDocument.DocumentNode.SelectSingleNode("//iframe");
		if (iframeNode == null)
		{
			return string.Empty;
		}
		return iframeNode.GetAttributeValue("src", string.Empty);
	}

	public static string ExtractVideoId(string html)
	{
		try
		{
			string[] segments = new Uri(GetYoutubeUrl(html)).Segments;
			if (segments.Length < 2 || segments[1] != "embed/")
			{
				return null;
			}
			return segments[2].Split('?')[0];
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}
}
