using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
using IVO.CMS.API.Models;
using IVO.CMS.Web.Mvc;
using IVO.Definition.Models;
using IVO.Definition.Containers;

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
            var ecm = await cms.cmrepo.GetCommit(id);
            if (ecm.HasErrors) return Json(new { errors = ecm.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            Commit cm = ecm.Value;

            return Json(new { commit = cm.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getByRef")]
        public async Task<ActionResult> GetCommitByRefName(RefName refName)
        {
            if (refName == null) return Json(new { success = false });

            var ecm = await cms.cmrepo.GetCommitByRefName(refName);
            if (ecm.HasErrors) return Json(new { errors = ecm.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            var cm = ecm.Value;

            return Json(new { @ref = cm.Item1.ToJSON(), commit = cm.Item2.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getByTag")]
        public async Task<ActionResult> GetCommitByTagName(TagName tagName)
        {
            if (tagName == null) return Json(new { success = false });

            var ecm = await cms.cmrepo.GetCommitByTagName(tagName);
            if (ecm.HasErrors) return Json(new { errors = ecm.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            var cm = ecm.Value;

            return Json(new { tag = cm.Item1.ToJSON(), commit = cm.Item2.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        private CommitTreeResponse toJSON(CommitID id, ImmutableContainer<CommitID, ICommit> commits)
        {
            ICommit cm;
            if (!commits.TryGetValue(id, out cm)) return null;

            return new CommitTreeResponse()
            {
                id = cm.ID.ToString(),
                treeid = cm.TreeID.ToString(),
                committer = cm.Committer.ToString(),
                date_committed = JSONTranslateExtensions.FromDate(cm.DateCommitted),
                parents_retrieved = cm.IsComplete,
                message = cm.Message,
                parents = cm.Parents.SelectAsArray(cmid => toJSON(cmid, commits))
            };
        }

        [HttpGet]
        [ActionName("getTree")]
        public async Task<ActionResult> GetCommitTree(CommitID id, int depth = 10)
        {
            var ecmtr = await cms.cmrepo.GetCommitTree(id, depth);
            if (ecmtr.HasErrors) return Json(new { errors = ecmtr.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            CommitTree cmtr = ecmtr.Value;

            return Json(new { depth = depth, commit_tree = toJSON(cmtr.RootID, cmtr.Commits) }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getTreeByTag")]
        public async Task<ActionResult> GetCommitTree(TagName tagName, int depth = 10)
        {
            if (tagName == null) return Json(new { success = false });

            var ecmtr = await cms.cmrepo.GetCommitTreeByTagName(tagName, depth);
            if (ecmtr.HasErrors) return Json(new { errors = ecmtr.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            Tuple<Tag, CommitTree> cmtr = ecmtr.Value;

            return Json(new { tag = cmtr.Item1.ToJSON(), depth = depth, commit_tree = toJSON(cmtr.Item2.RootID, cmtr.Item2.Commits) }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getTreeByRef")]
        public async Task<ActionResult> GetCommitTree(RefName refName, int depth = 10)
        {
            if (refName == null) return Json(new { success = false });

            var ecmtr = await cms.cmrepo.GetCommitTreeByRefName(refName, depth);
            if (ecmtr.HasErrors) return Json(new { errors = ecmtr.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            Tuple<Ref, CommitTree> cmtr = ecmtr.Value;

            return Json(new { @ref = cmtr.Item1.ToJSON(), depth = depth, commit_tree = toJSON(cmtr.Item2.RootID, cmtr.Item2.Commits) }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ActionName("create")]
        public async Task<ActionResult> Create(RefName refName, CommitRequest cmj)
        {
            if (cmj == null) return Json(new { success = false });
            if (refName == null) return Json(new { success = false });

            // First get the ref and its CommitID, if it exists:
            var erf = await cms.rfrepo.GetRefByName(refName);
            if (erf.HasErrors) return Json(new { errors = erf.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            // Map from the JSON CommitModel:
            var ecb = cmj.FromJSON();
            if (ecb.HasErrors) return Json(new { errors = ecb.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            Commit.Builder cb = ecb.Value;
            Ref rf = erf.Value;
            
            // Add the ref's CommitID as the parent, if the ref exists:
            if ((rf != null) && (cb.Parents.Count == 0))
            {
                cb.Parents.Add(rf.CommitID);
            }

            // Persist the commit:
            var epcm = await cms.cmrepo.PersistCommit(cb);
            if (epcm.HasErrors) return Json(new { errors = epcm.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            Commit pcm = epcm.Value;

            // Persist the ref with this new CommitID:
            Ref.Builder rfb = new Ref.Builder(refName, pcm.ID);
            erf = await cms.rfrepo.PersistRef(rfb);
            if (erf.HasErrors) return Json(new { errors = erf.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            // Return the commit model as JSON again:
            return Json(new { @ref = rf.ToJSON(), commit = pcm.ToJSON() }, JsonRequestBehavior.AllowGet);
        }
    }
}
