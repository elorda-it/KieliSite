namespace MODEL;

/// <summary>Service tab on the services page ("Аударма", "Нотариат", ...).</summary>
public class Servicetab
{
	public int Id { get; set; }

	public string Slug { get; set; }

	public string Code { get; set; }

	public string Name { get; set; }

	public string Icon { get; set; }

	public string Cta { get; set; }

	public string Visual { get; set; }

	public string StepsJson { get; set; }

	public string Footnote { get; set; }

	public int DisplayOrder { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public byte QStatus { get; set; }
}
