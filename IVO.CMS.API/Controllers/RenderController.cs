using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
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
        [ActionName("render")]
        [ValidateInput(false)]
        public async Task<ActionResult> RenderBlob(TreeBlobPath rootedPath, DateTimeOffset? viewDate)
        {
            // Get the stream for the blob by its path:
            var blob = await cms.tpsbrepo.GetBlobByTreePath(rootedPath);
            if (blob == null) return new HttpNotFoundResult(String.Format("A blob could not be found off tree {0} by path '{0}'", rootedPath.RootTreeID.ToString(), rootedPath.Path.ToString()));

            // TODO: streaming output!
            var html = await cms.GetContentEngine(viewDate).RenderBlob(blob);

            // HTML5 output:
            return Content((string)html, "application/xhtml+xml", Encoding.UTF8);
        }
    }
}
