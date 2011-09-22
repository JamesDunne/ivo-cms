using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace IVO.CMS.API.Code
{
    /// <summary>
    /// Handles exceptions by returning JSON objects containing the exception messages.
    /// </summary>
    public class JsonHandleErrorAttribute : HandleErrorAttribute
    {
        public override void OnException(ExceptionContext filterContext)
        {
            filterContext.ExceptionHandled = true;
            filterContext.HttpContext.Response.StatusCode = 500;

            // Set the result to render JSON:
            filterContext.Result = new JsonResult()
            {
                Data = new { success = false, exceptions = ToJSON(filterContext.Exception) },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                ContentType = "application/json"
            };

            base.OnException(filterContext);
        }

        private static object[] ToJSON(Exception ex)
        {
            AggregateException ag = ex as AggregateException;
            if (ag != null)
            {
                // For an AggregateException, send the array of InnerExceptions:
                object[][] nested = ag.InnerExceptions.ToArray(ag.InnerExceptions.Count).SelectAsArray(ix => ToJSON(ix));
                return (
                    from exs in nested
                    from cex in exs
                    select cex
                ).ToArray(nested.Sum(a => a.Length));
            }
            else
            {
                // Any other exception type:
                return new[] { new { type = ex.GetType().FullName, message = ex.Message, stackTrace = ex.StackTrace, ex.Source } };
            }
        }
    }
}