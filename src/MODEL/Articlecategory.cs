namespace MODEL;

/// <summary>News/article category (Жаңалықтар, Құжаттар, ...).</summary>
public class Articlecategory
{
	public int Id { get; set; }

	public string Slug { get; set; }

	public string Name { get; set; }

	public int DisplayOrder { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public byte QStatus { get; set; }
}
