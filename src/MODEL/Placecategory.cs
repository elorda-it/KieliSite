namespace MODEL;

/// <summary>Place category (Табиғат, Киелі орын, ...).</summary>
public class Placecategory
{
	public int Id { get; set; }

	public string Slug { get; set; }

	public string Name { get; set; }

	/// <summary>Label on the filter chips (e.g. «Киелі орындар»).</summary>
	public string PluralName { get; set; }

	public int DisplayOrder { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public byte QStatus { get; set; }
}
