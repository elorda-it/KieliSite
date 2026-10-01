namespace MODEL;

public class Multilanguage
{
	public int Id { get; set; }

	public string Language { get; set; }

	public string TableName { get; set; }

	public int ColumnId { get; set; }

	public string ColumnName { get; set; }

	public string ColumnValue { get; set; }

	public byte QStatus { get; set; }
}
