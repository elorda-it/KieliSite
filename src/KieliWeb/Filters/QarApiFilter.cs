using System.Linq;
using System.Threading.Tasks;
using COMMON;
using MODEL.FormatModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KieliWeb.Filters;

public class QarApiFilter : IAsyncActionFilter, IFilterMetadata
{
	public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
	{
		bool isApi = context.HttpContext.Request.Headers.ContainsKey("X-Client-Platform");
		ActionExecutedContext executedContext = await next();
		if (!isApi)
		{
			return;
		}
		string language = context.HttpContext.Request.Headers["language"].FirstOrDefault() ?? "kz";
		language = (string.IsNullOrEmpty(language) ? "kz" : language);
		context.HttpContext.Items["language"] = language;
		if ((language == "latyn" || language == "tote") ? true : false)
		{
			if (executedContext.Result is JsonResult { Value: AjaxMsgModel jsonModel } jsonResult)
			{
				HtmlAgilityPackHelper.ConvertObjectCyrillicLetters(jsonModel.Data, language);
				AjaxMsgModel ajaxMsgModel = jsonModel;
				string message = ((language == "latyn") ? Cyrl2LatynHelper.Cyrl2Latyn(jsonModel.Message) : ((!(language == "tote")) ? jsonModel.Message : Cyrl2ToteHelper.Cyrl2Tote(jsonModel.Message)));
				ajaxMsgModel.Message = message;
				jsonResult.Value = jsonModel;
			}
			else if (executedContext.Result is ObjectResult { Value: AjaxMsgModel model } objectResult)
			{
				HtmlAgilityPackHelper.ConvertObjectCyrillicLetters(model.Data, language);
				AjaxMsgModel ajaxMsgModel2 = model;
				string message = ((language == "latyn") ? Cyrl2LatynHelper.Cyrl2Latyn(model.Message) : ((!(language == "tote")) ? model.Message : Cyrl2ToteHelper.Cyrl2Tote(model.Message)));
				ajaxMsgModel2.Message = message;
				objectResult.Value = model;
			}
		}
	}
}
