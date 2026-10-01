namespace MODEL;

public class Navigation
{
	public int Id { get; set; }

	public int NavigationTypeId { get; set; }

	public int ParentId { get; set; }

	public string NavTitle { get; set; }

	public string NavUrl { get; set; }

	public string Target { get; set; }

	public byte HasIcon { get; set; }

	public string Icon { get; set; }

	public string Description { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public int DisplayOrder { get; set; }

	public byte NoChild { get; set; }

	public byte IsLock { get; set; }

	public byte QStatus { get; set; }
}
