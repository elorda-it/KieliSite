namespace MODEL;

/// <summary>Listening recording of a variant: what is said (one "Name: words" line per turn) and the audio file.</summary>
public class Kaztestaudio
{
	public int Id { get; set; }

	public int VariantId { get; set; }

	public int AudioNo { get; set; }

	/// <summary>Admin-only name ("Диалог: Базардағы кездесу"); never shown in the test, it would give answers away.</summary>
	public string Title { get; set; }

	/// <summary>Who reads it, for whoever records the audio.</summary>
	public string Voices { get; set; }

	public string Script { get; set; }

	public string AudioUrl { get; set; }

	/// <summary>Who or what made the audio, shown under the player (a voice actor, or the synthetic voice and its licence).</summary>
	public string Credit { get; set; }

	public string CreditUrl { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public byte QStatus { get; set; }
}
