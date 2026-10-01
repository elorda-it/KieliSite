namespace MODEL;

public class Rolepermission
{
	public int Id { get; set; }

	public int RoleId { get; set; }

	public int PermissionId { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public sbyte QStatus { get; set; }

	public string TableName { get; set; }

	public int ColumnId { get; set; }
}
