namespace MODEL.FormatModels;

public class AjaxMsgModel
{
	public string Status { get; set; }

	public string Message { get; set; }

	public string BackUrl { get; set; }

	public object Data { get; set; }
}
