namespace MODEL;

public class Permission
{
	public int Id { get; set; }

	public string TableName { get; set; }

	public string LocalKey { get; set; }

	public string ManageType { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public sbyte QStatus { get; set; }
}
