namespace MODEL;

/// <summary>Place in the «Киелі» atlas.</summary>
public class Place
{
	public int Id { get; set; }

	public string Slug { get; set; }

	public int RegionId { get; set; }

	public int CategoryId { get; set; }

	public string Name { get; set; }

	public string Fact { get; set; }

	public string Lead { get; set; }

	public string BodyHtml { get; set; }

	public string FactsJson { get; set; }

	public string ImageUrl { get; set; }

	public string PanoJson { get; set; }

	public decimal Lat { get; set; }

	public decimal Lon { get; set; }

	public int LegacyId { get; set; }

	public byte IsFeatured { get; set; }

	public int DisplayOrder { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public byte QStatus { get; set; }
}
