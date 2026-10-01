namespace MODEL;

/// <summary>UNESCO / intangible heritage item.</summary>
public class Heritage
{
	public int Id { get; set; }

	public string Name { get; set; }

	public string Kind { get; set; }

	public string Year { get; set; }

	public string ImageUrl { get; set; }

	public string LinkUrl { get; set; }

	public int DisplayOrder { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public byte QStatus { get; set; }
}
