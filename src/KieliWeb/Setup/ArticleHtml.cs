using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace KieliWeb.Setup;

/// <summary>
/// Prepares an article body written in the admin editor for the page: every &lt;h2&gt; gets an
/// id and becomes an entry of «Мазмұны», and the first paragraph gets the drop cap.
/// </summary>
public static class ArticleHtml
{
	public sealed class Result
	{
		public string Html { get; init; }

		public IReadOnlyList<(string Id, string Title)> Toc { get; init; }
	}

	public static Result Prepare(string html)
	{
		HtmlDocument doc = new HtmlDocument();
		doc.LoadHtml(html ?? string.Empty);
		List<(string, string)> toc = new List<(string, string)>();
		HashSet<string> used = new HashSet<string>();
		int n = 0;
		foreach (HtmlNode h2 in doc.DocumentNode.Descendants("h2").ToList())
		{
			n++;
			string id = h2.GetAttributeValue("id", string.Empty);
			if (string.IsNullOrWhiteSpace(id) || !Regex.IsMatch(id, "^[A-Za-z][A-Za-z0-9_-]*$") || used.Contains(id))
			{
				id = "s" + n;
				h2.SetAttributeValue("id", id);
			}
			used.Add(id);
			string title = WebUtility.HtmlDecode(h2.InnerText).Trim();
			if (title.Length > 0)
			{
				toc.Add((id, title));
			}
		}
		HtmlNode first = doc.DocumentNode.ChildNodes.FirstOrDefault(x => x.NodeType == HtmlNodeType.Element);
		if (first != null && first.Name == "p" && !doc.DocumentNode.Descendants("p").Any(p => p.HasClass("dropcap")))
		{
			first.AddClass("dropcap");
		}
		return new Result { Html = doc.DocumentNode.OuterHtml, Toc = toc };
	}
}
