namespace MODEL;

/// <summary>Request from the consultation / document forms.</summary>
public class Consultrequest
{
	public int Id { get; set; }

	public string Name { get; set; }

	public string Contact { get; set; }

	public string Country { get; set; }

	/// <summary>Site language the visitor wrote from (kz, ru, zh-cn, en, tr).</summary>
	public string Language { get; set; }

	public string ServiceKey { get; set; }

	public string Code { get; set; }

	public string ServiceTitle { get; set; }

	public string Message { get; set; }

	public string Source { get; set; }

	public byte Status { get; set; }

	public string StaffNote { get; set; }

	public string Ip { get; set; }

	public string UserAgent { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public byte QStatus { get; set; }
}
