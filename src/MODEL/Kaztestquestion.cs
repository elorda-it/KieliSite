namespace MODEL;

/// <summary>ҚАЗТЕСТ question. Answer "" means the key is not known yet (not scored).</summary>
public class Kaztestquestion
{
	public int Id { get; set; }

	public int VariantId { get; set; }

	public int SectionNo { get; set; }

	public string SectionName { get; set; }

	public int QuestionNo { get; set; }

	public string QuestionText { get; set; }

	public string SubText { get; set; }

	public string OptionA { get; set; }

	public string OptionB { get; set; }

	public string OptionC { get; set; }

	public string OptionD { get; set; }

	public string Answer { get; set; }

	/// <summary>Old per-question audio link; the audio now comes from the recording (<see cref="AudioNo"/>).</summary>
	public string AudioUrl { get; set; }

	/// <summary>Listening questions: number of the recording (kaztestaudio.audioNo) of the same variant.</summary>
	public int AudioNo { get; set; }

	public int PassageNo { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public byte QStatus { get; set; }
}
