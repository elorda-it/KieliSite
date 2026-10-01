namespace MODEL;

/// <summary>ҚАЗТЕСТ practice variant (1-нұсқа, 2-нұсқа, ...).</summary>
public class Kaztestvariant
{
	public int Id { get; set; }

	public string Title { get; set; }

	public byte IsPublished { get; set; }

	public int DisplayOrder { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public byte QStatus { get; set; }
}
