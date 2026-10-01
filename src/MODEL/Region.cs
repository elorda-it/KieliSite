namespace MODEL;

/// <summary>Region of Kazakhstan; MapId is the id of its shape in the SVG atlas.</summary>
public class Region
{
	public int Id { get; set; }

	public int MapId { get; set; }

	public string Name { get; set; }

	/// <summary>Label on the region buttons (e.g. «Ақмола» for «Ақмола облысы»).</summary>
	public string ShortName { get; set; }

	public int PlaceCount { get; set; }

	public byte IsCity { get; set; }

	public int DisplayOrder { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public byte QStatus { get; set; }
}
