using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
using IVO.CMS.Web.Mvc;
using IVO.Definition.Models;

namespace IVO.CMS.API.Controllers
{
    public class CommitController : TaskAsyncController
    {
        private CMSContext cms;

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            this.cms = new CMSContext(new DirectoryInfo(Server.MapPath("~/ivo/")));

            base.OnActionExecuting(filterContext);
        }

        [HttpGet]
        [ActionName("refs")]
        public async Task<ActionResult> GetCommitByRefName(RefName id)
        {
            var cm = await cms.cmrepo.GetCommitByRefName(id);

            return Json(new { @ref = cm.Item1, commit = cm.Item2 }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("tags")]
        public async Task<ActionResult> GetCommitByTagName(TagName id)
        {
            var cm = await cms.cmrepo.GetCommitByTagName(id);

            return Json(new { tag = cm.Item1, commit = cm.Item2 }, JsonRequestBehavior.AllowGet);
        }
    }
}
