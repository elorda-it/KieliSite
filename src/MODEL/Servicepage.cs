namespace MODEL;

/// <summary>Detail page such as «Ата жолы» картасы; sections live in DataJson.</summary>
public class Servicepage
{
	public int Id { get; set; }

	public string Slug { get; set; }

	public string Title { get; set; }

	public string SeoDescription { get; set; }

	public string DataJson { get; set; }

	public int DisplayOrder { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public byte QStatus { get; set; }
}
