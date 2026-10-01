namespace MODEL;

/// <summary>Page texts and images: one row per block (see KieliWeb/Setup/BlockRegistry.cs).</summary>
public class Pageblock
{
	public int Id { get; set; }

	public string BlockKey { get; set; }

	public string DataJson { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public byte QStatus { get; set; }
}
