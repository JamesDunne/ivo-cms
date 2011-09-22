using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
using IVO.CMS.API.Models;
using IVO.CMS.Web.Mvc;
using IVO.Definition.Models;

namespace IVO.CMS.API.Controllers
{
    [JsonHandleError]
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
            if (refName == null) return Json(new { success = false });

            var cm = await cms.cmrepo.GetCommitByRefName(refName);

            return Json(new { @ref = cm.Item1.ToJSON(), commit = cm.Item2.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getByTag")]
        public async Task<ActionResult> GetCommitByTagName(TagName tagName)
        {
            if (tagName == null) return Json(new { success = false });

            var cm = await cms.cmrepo.GetCommitByTagName(tagName);

            return Json(new { tag = cm.Item1.ToJSON(), commit = cm.Item2.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getTree")]
        public async Task<ActionResult> GetCommitTree(CommitID id, int depth = 10)
        {
            var cmtr = await cms.cmrepo.GetCommitTree(id, depth);

            return Json(new {
                rootid = cmtr.Item1.ToString(),
                commits = cmtr.Item2.Values
                    .Select(cm => cm.ToJSON())
                    .ToArray(cmtr.Item2.Count)
            });
        }

        [HttpGet]
        [ActionName("getTreeByTag")]
        public async Task<ActionResult> GetCommitTree(TagName tagName, int depth = 10)
        {
            if (tagName == null) return Json(new { success = false });

            var cmtr = await cms.cmrepo.GetCommitTreeByTagName(tagName, depth);

            return Json(new
            {
                tag = cmtr.Item1.ToJSON(),
                root_commitid = cmtr.Item2.ToString(),
                commits = cmtr.Item3.Values
                    .Select(cm => cm.ToJSON())
                    .ToArray(cmtr.Item3.Count)
            });
        }

        [HttpGet]
        [ActionName("getTreeByRef")]
        public async Task<ActionResult> GetCommitTree(RefName refName, int depth = 10)
        {
            if (refName == null) return Json(new { success = false });

            var cmtr = await cms.cmrepo.GetCommitTreeByRefName(refName, depth);

            return Json(new
            {
                @ref = cmtr.Item1.ToJSON(),
                root_commitid = cmtr.Item2.ToString(),
                commits = cmtr.Item3.Values
                    .Select(cm => cm.ToJSON())
                    .ToArray(cmtr.Item3.Count)
            });
        }

        [HttpPost]
        [ActionName("create")]
        public async Task<ActionResult> Create(RefName refName, CommitModel cmj)
        {
            if (cmj == null) return Json(new { success = false });
            if (refName == null) return Json(new { success = false });

            // First get the ref and its CommitID, if it exists:
            var rf = await cms.rfrepo.GetRefByName(refName);

            // Map from the JSON CommitModel:
            Commit.Builder cb = cmj.FromJSON();
            
            // Add the ref's CommitID as the parent, if the ref exists:
            if ((rf != null) && (cb.Parents.Count == 0))
            {
                cb.Parents.Add(rf.CommitID);
            }

            // Persist the commit:
            var pcm = await cms.cmrepo.PersistCommit(cb);

            // Persist the ref with this new CommitID:
            Ref.Builder rfb = new Ref.Builder(refName, pcm.ID);
            rf = await cms.rfrepo.PersistRef(rfb);

            // Return the commit model as JSON again:
            return Json(new { @ref = rf.ToJSON(), commit = pcm.ToJSON() }, JsonRequestBehavior.AllowGet);
        }
    }
}
