namespace MODEL;

/// <summary>File attached to a request (stored outside wwwroot).</summary>
public class Consultfile
{
	public int Id { get; set; }

	public int RequestId { get; set; }

	public string FileName { get; set; }

	public string StoredName { get; set; }

	public string ContentType { get; set; }

	public int FileSize { get; set; }

	public int AddTime { get; set; }

	public byte QStatus { get; set; }
}
