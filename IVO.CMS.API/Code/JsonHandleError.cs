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

            object[] exs;
            if (filterContext.Exception is AggregateException)
            {
                // For an AggregateException, send the array of InnerExceptions:
                AggregateException ag = (AggregateException)filterContext.Exception;
                exs = ag.InnerExceptions.ToArray(ag.InnerExceptions.Count).SelectAsArray(ex => ToJSON(ex));
            }
            else
            {
                // Any other exception type:
                Exception ex = filterContext.Exception;
                exs = new[] { ToJSON(ex) };
            }

            // Set the result to render JSON:
            filterContext.Result = new JsonResult() {
                Data = new { success = false, exceptions = exs },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                ContentType = "application/json"
            };

            base.OnException(filterContext);
        }

        private static object ToJSON(Exception ex)
        {
            return new { type = ex.GetType().FullName, message = ex.Message, stackTrace = ex.StackTrace, ex.Source };
        }
    }
}