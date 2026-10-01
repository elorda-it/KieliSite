namespace MODEL;

public class Adminloginerrorlog
{
	public uint Id { get; set; }

	public int AdminId { get; set; }

	public int LastErrorTime { get; set; }

	public string LastErrorIP { get; set; }

	public int ErrorCount { get; set; }
}
