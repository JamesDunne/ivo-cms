using System.IO;
using System.Web.Mvc;
using IVO.CMS.Web.Internal.Mvc;
using IVO.Definition.Errors;
using IVO.CMS.API.Models;

namespace IVO.CMS.Web.API.Code
{
    public class CMSTaskAsyncController : TaskAsyncController
    {
        protected ISystemContext cms;

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            this.cms = new FileSystemContext(new DirectoryInfo(Server.MapPath("~/ivo/")));

            base.OnActionExecuting(filterContext);
        }

        protected JsonResult ErrorJson<T>(Errorable<T> errored)
        {
            return Json(new { errors = errored.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);
        }
    }
}