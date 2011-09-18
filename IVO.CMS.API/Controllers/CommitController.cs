using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
using IVO.CMS.Web.Mvc;
using IVO.Definition.Models;
using IVO.CMS.API.Models;

namespace IVO.CMS.API.Controllers
{
    public class CommitController : TaskAsyncController
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
        [ActionName("getByID")]
        public async Task<ActionResult> GetCommitByID(CommitID id)
        {
            var cm = await cms.cmrepo.GetCommit(id);

            return Json(new { commit = cm.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getByRef")]
        public async Task<ActionResult> GetCommitByRefName(RefName refName)
        {
            if (refName == null) return new EmptyResult();

            var cm = await cms.cmrepo.GetCommitByRefName(refName);

            return Json(new { @ref = cm.Item1.ToJSON(), commit = cm.Item2.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getByTag")]
        public async Task<ActionResult> GetCommitByTagName(TagName tagName)
        {
            if (tagName == null) return new EmptyResult();

            var cm = await cms.cmrepo.GetCommitByTagName(tagName);

            return Json(new { tag = cm.Item1.ToJSON(), commit = cm.Item2.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ActionName("create")]
        public async Task<ActionResult> Create(CommitModel cmj)
        {
            if (cmj == null) return new EmptyResult();

            // Map from the JSON CommitModel:
            Commit cm = cmj.FromJSON();

            // Persist the commit:
            var pcm = await cms.cmrepo.PersistCommit(cm);

            // Return the commit model as JSON again:
            return Json(new { commit = pcm.ToJSON() }, JsonRequestBehavior.AllowGet);
        }
    }
}
