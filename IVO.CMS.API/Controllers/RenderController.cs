using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
using IVO.CMS.API.Models;
using IVO.Definition.Models;
using IVO.CMS.Web.Mvc;

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

        #endregion

        [HttpGet]
        [ActionName("renderByTree")]
        [ValidateInput(false)]
        public async Task<ActionResult> RenderBlob(TreeBlobPath rootedPath, DateTimeOffset? viewDate)
        {
            // Get the stream for the blob by its path:
            var eblob = await cms.tpsbrepo.GetBlobByTreePath(rootedPath);
            if (eblob.HasErrors) return Json(new { errors = eblob.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            TreePathStreamedBlob blob = eblob.Value;
            if (blob == null) return new HttpNotFoundResult(String.Format("A blob could not be found off tree {0} by path '{0}'", rootedPath.RootTreeID.ToString(), rootedPath.Path.ToString()));

            // TODO: streaming output!
            var ehtml = await cms.GetContentEngine(viewDate).RenderBlob(blob);
            if (ehtml.HasErrors) return Json(new { errors = ehtml.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            var html = ehtml.Value;

            // HTML5 output:
            return Content((string)html, "application/xhtml+xml", Encoding.UTF8);
        }
    }
}
