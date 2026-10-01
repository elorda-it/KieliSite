namespace MODEL;

/// <summary>Reading passage of a variant; paragraphs separated by a blank line.</summary>
public class Kaztestpassage
{
	public int Id { get; set; }

	public int VariantId { get; set; }

	public int PassageNo { get; set; }

	public string BodyText { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public byte QStatus { get; set; }
}
