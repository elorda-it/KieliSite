namespace MODEL;

/// <summary>One concrete service inside a tab; Code (AUD-02 ...) is stored on every request.</summary>
public class Serviceitem
{
	public int Id { get; set; }

	public int TabId { get; set; }

	public string Slug { get; set; }

	public string Code { get; set; }

	public string Name { get; set; }

	public string Icon { get; set; }

	public string Title { get; set; }

	public string Dir { get; set; }

	public string Who { get; set; }

	public string BringJson { get; set; }

	public string Note { get; set; }

	public string LinkUrl { get; set; }

	public string LinkText { get; set; }

	public string Cta { get; set; }

	public int DisplayOrder { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public byte QStatus { get; set; }
}
