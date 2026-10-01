using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using MODEL;

namespace KieliWeb.Setup;

/// <summary>
/// The clickable map of the 17 regions (Setup/kzmap.svg, from the prototype). Each shape carries
/// data-region = region.mapId; its spoken label follows the region's name and count in the admin.
/// </summary>
public static class KzMap
{
	private static readonly Lazy<string> Source = new Lazy<string>(() =>
	{
		using Stream stream = typeof(KzMap).Assembly.GetManifestResourceStream("kzmap.svg");
		using StreamReader reader = new StreamReader(stream);
		return reader.ReadToEnd();
	});

	private static readonly Regex Shape = new Regex("(<[a-z]+ class=\"rg[^\"]*\" data-region=\"(\\d+)\"[^>]*?aria-label=\")([^\"]*)(\")", RegexOptions.Compiled);

	public static string Svg(IEnumerable<Region> regions, Func<Region, string> label)
	{
		Dictionary<int, Region> byMap = regions.GroupBy(r => r.MapId).ToDictionary(g => g.Key, g => g.First());
		return Shape.Replace(Source.Value, m =>
		{
			if (!byMap.TryGetValue(int.Parse(m.Groups[2].Value), out Region r))
			{
				return m.Value;
			}
			return m.Groups[1].Value + WebUtility.HtmlEncode(r.Name + ", " + label(r)) + m.Groups[4].Value;
		});
	}
}
