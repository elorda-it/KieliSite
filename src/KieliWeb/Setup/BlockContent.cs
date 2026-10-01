using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Html;
using Newtonsoft.Json.Linq;

namespace KieliWeb.Setup;

/// <summary>
/// Read access to one page block in the views:
/// <c>b["title"]</c> (text, encoded by Razor), <c>b.Raw("body")</c> (rich text),
/// <c>b.Lines("name")</c> (line breaks kept), <c>b.List("items")</c> (repeated rows).
/// Missing fields read as empty, so a view never breaks on a half-filled block.
/// </summary>
public class BlockContent
{
	private readonly JObject _data;

	public BlockContent(JObject data)
	{
		_data = data ?? new JObject();
	}

	public JObject Data => _data;

	public string this[string name]
	{
		get
		{
			if (!_data.TryGetValue(name, out JToken token) || token == null || token.Type == JTokenType.Null)
			{
				return string.Empty;
			}
			// culture-free: 43.35, not 43,35 on a Russian-locale server
			return token.Type == JTokenType.String ? (string)token : token.ToString(Newtonsoft.Json.Formatting.None);
		}
	}

	public bool Has(string name) => !string.IsNullOrWhiteSpace(this[name]);

	public bool Bool(string name)
	{
		string v = this[name].Trim().ToLowerInvariant();
		return v == "1" || v == "true" || v == "yes" || v == "on";
	}

	public int Int(string name, int fallback = 0) => int.TryParse(this[name], out int v) ? v : fallback;

	/// <summary>Rich-text field (edited with TinyMCE by administrators): written as-is.</summary>
	public IHtmlContent Raw(string name) => new HtmlString(this[name]);

	/// <summary>Plain text with its line breaks kept as &lt;br&gt;.</summary>
	public IHtmlContent Lines(string name)
	{
		string[] parts = this[name].Replace("\r\n", "\n").Split('\n');
		return new HtmlString(string.Join("<br>", parts.Select(WebUtility.HtmlEncode)));
	}

	/// <summary>Plain text split into paragraphs (blank line between them).</summary>
	public IReadOnlyList<string> Paragraphs(string name)
	{
		return this[name].Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
			.Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
	}

	public IReadOnlyList<BlockContent> List(string name)
	{
		if (!_data.TryGetValue(name, out JToken token) || !(token is JArray array))
		{
			return Array.Empty<BlockContent>();
		}
		return array.OfType<JObject>().Select(o => new BlockContent(o)).ToList();
	}
}
