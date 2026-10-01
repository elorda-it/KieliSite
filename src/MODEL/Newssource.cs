namespace MODEL;

/// <summary>A news feed (RSS or Atom) read every hour by Setup/NewsCollector.cs; its matching items become news cards.</summary>
public class Newssource
{
	public int Id { get; set; }

	/// <summary>Shown on the card as the source («Дереккөз: …»).</summary>
	public string Name { get; set; }

	public string SiteUrl { get; set; }

	public string FeedUrl { get; set; }

	/// <summary>Rules separated by commas or new lines; "a + b" needs both words. Empty = every item of the feed.</summary>
	public string Keywords { get; set; }

	public string ExcludeWords { get; set; }

	public int CategoryId { get; set; }

	/// <summary>1 = the card is shown at once, 0 = it is added hidden for an editor to check.</summary>
	public byte AutoPublish { get; set; }

	public int MaxPerRun { get; set; }

	public byte IsEnabled { get; set; }

	/// <summary>Editor's note, e.g. what the publisher's terms allow.</summary>
	public string Note { get; set; }

	public int LastCheckTime { get; set; }

	public string LastStatus { get; set; }

	/// <summary>The feed's ETag and Last-Modified, sent back so an unchanged feed is not downloaded again.</summary>
	public string Etag { get; set; }

	public string LastModified { get; set; }

	public int AddedCount { get; set; }

	public int DisplayOrder { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public byte QStatus { get; set; }
}
