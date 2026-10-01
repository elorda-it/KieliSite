namespace MODEL;

/// <summary>News item or article. Either BodyHtml (own page) or LinkUrl (card opens that link).</summary>
public class Article
{
	public int Id { get; set; }

	public int CategoryId { get; set; }

	public string Slug { get; set; }

	public string Title { get; set; }

	public string Excerpt { get; set; }

	public string BodyHtml { get; set; }

	public string LinkUrl { get; set; }

	public string CoverImageUrl { get; set; }

	public string CoverTone { get; set; }

	public string CoverIcon { get; set; }

	public string DateText { get; set; }

	public int PublishTime { get; set; }

	public int ReadMinutes { get; set; }

	public string SourceName { get; set; }

	public string SourceUrl { get; set; }

	/// <summary>The «Жаңалық көздері» row that collected this item (0 = written in the admin).</summary>
	public int NewsSourceId { get; set; }

	public string AuthorName { get; set; }

	/// <summary>"tab:item" pre-selected when the reader presses the article's «Құжатты тексерту» button.</summary>
	public string ServiceKey { get; set; }

	public byte IsImportant { get; set; }

	public byte IsPublished { get; set; }

	public string SeoDescription { get; set; }

	public int ViewCount { get; set; }

	public int DisplayOrder { get; set; }

	public int AddTime { get; set; }

	public int UpdateTime { get; set; }

	public byte QStatus { get; set; }
}
