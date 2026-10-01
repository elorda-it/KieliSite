using System.Collections.Generic;

namespace MODEL.FormatModels;

public class ApiUnifiedModel
{
	public uint Id { get; set; }

	public string Keyword { get; set; }

	public string DateTimeStart { get; set; }

	public string DateTimeEnd { get; set; }

	public int Start { get; set; }

	public int Length { get; set; }

	public List<DataTableOrderModel> OrderList { get; set; }

	public string jsonData { get; set; }

	public List<int> idList { get; set; }

	public string manageType { get; set; }

	public string contentType { get; set; }

	public int complate { get; set; }

	public string platform { get; set; }

	public string language { get; set; }
}
