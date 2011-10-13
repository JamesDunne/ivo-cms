using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
using IVO.CMS.API.Models;
using IVO.Definition.Models;
using IVO.CMS.Web.Internal.Mvc;
using IVO.Definition.Errors;
using System.Diagnostics;

namespace IVO.CMS.API.Controllers
{
    public class RenderController : TaskAsyncController
    {
        #region Private implementation

        private CMSContext cms;

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            this.cms = new CMSContext(new DirectoryInfo(Server.MapPath("~/ivo/")));

            base.OnActionExecuting(filterContext);
        }

        private JsonResult ErrorJson<T>(Errorable<T> errored)
        {
            return Json(new { errors = errored.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        #endregion

        [HttpGet]
        [ActionName("renderByTree")]
        [ValidateInput(false)]
        public async Task<ActionResult> RenderBlob(Errorable<TreeBlobPath> epath, DateTimeOffset? viewDate)
        {
            Debug.Assert(epath != null);
            if (epath.HasErrors) return ErrorJson(epath);
            var path = epath.Value;

            // Get the stream for the blob by its path:
            var eblob = await cms.tpsbrepo.GetBlobByTreePath(epath.Value);
            if (eblob.HasErrors) return ErrorJson(eblob);

            TreePathStreamedBlob blob = eblob.Value;
            if (blob == null) return new HttpNotFoundResult(String.Format("A blob could not be found off tree {0} by path '{0}'", path.RootTreeID.ToString(), path.Path.ToString()));

            // Render the blob:
            var ehtml = await cms.GetContentEngine(viewDate).RenderBlob(blob);
            if (ehtml.HasErrors) return ErrorJson(ehtml);

            var html = ehtml.Value;

            // HTML5 output:
            return Content((string)html, "application/xhtml+xml", Encoding.UTF8);
        }
    }
}
