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
using IVO.Definition.Errors;

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

        private JsonResult ErrorJson<T>(Errorable<T> errored)
        {
            return Json(new { errors = errored.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        #endregion

        [HttpGet]
        [ActionName("getByID")]
        public async Task<ActionResult> GetCommitByID(Errorable<CommitID.Partial> id)
        {
            if (id.HasErrors) return ErrorJson(id);

            var eid = await cms.cmrepo.ResolvePartialID(id.Value);
            if (eid.HasErrors) return ErrorJson(eid);

            var ecm = await cms.cmrepo.GetCommit(eid.Value);
            if (ecm.HasErrors) return ErrorJson(ecm);

            Commit cm = ecm.Value;

            return Json(new { commit = cm.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getByRef")]
        public async Task<ActionResult> GetCommitByRefName(Errorable<RefName> refName)
        {
            if (refName.HasErrors) return ErrorJson(refName);

            var ecm = await cms.cmrepo.GetCommitByRefName(refName.Value);
            if (ecm.HasErrors) return ErrorJson(ecm);

            var cm = ecm.Value;

            return Json(new { @ref = cm.Item1.ToJSON(), commit = cm.Item2.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getByTag")]
        public async Task<ActionResult> GetCommitByTagName(Errorable<TagName> tagName)
        {
            if (tagName.HasErrors) return ErrorJson(tagName);

            var ecm = await cms.cmrepo.GetCommitByTagName(tagName.Value);
            if (ecm.HasErrors) return ErrorJson(ecm);

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
        public async Task<ActionResult> GetCommitTree(Errorable<CommitID.Partial> id, int depth = 10)
        {
            if (id.HasErrors) return ErrorJson(id);

            // Attempt to resolve the partial ID:
            var eid = await cms.cmrepo.ResolvePartialID(id.Value);
            if (eid.HasErrors) return ErrorJson(eid);
            
            var ecmtr = await cms.cmrepo.GetCommitTree(eid.Value, depth);
            if (ecmtr.HasErrors) return ErrorJson(ecmtr);

            CommitTree cmtr = ecmtr.Value;

            return Json(new { depth = depth, commit_tree = toJSON(cmtr.RootID, cmtr.Commits) }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getTreeByTag")]
        public async Task<ActionResult> GetCommitTree(Errorable<TagName> tagName, int depth = 10)
        {
            if (tagName.HasErrors) return ErrorJson(tagName);

            var ecmtr = await cms.cmrepo.GetCommitTreeByTagName(tagName.Value, depth);
            if (ecmtr.HasErrors) return ErrorJson(ecmtr);

            Tuple<Tag, CommitTree> cmtr = ecmtr.Value;

            return Json(new { tag = cmtr.Item1.ToJSON(), depth = depth, commit_tree = toJSON(cmtr.Item2.RootID, cmtr.Item2.Commits) }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getTreeByRef")]
        public async Task<ActionResult> GetCommitTree(Errorable<RefName> refName, int depth = 10)
        {
            if (refName.HasErrors) return ErrorJson(refName);

            var ecmtr = await cms.cmrepo.GetCommitTreeByRefName(refName.Value, depth);
            if (ecmtr.HasErrors) return ErrorJson(ecmtr);

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
            if (erf.HasErrors) return ErrorJson(erf);

            // Map from the JSON CommitModel:
            var ecb = cmj.FromJSON();
            if (ecb.HasErrors) return ErrorJson(ecb);

            Commit.Builder cb = ecb.Value;
            Ref rf = erf.Value;
            
            // Add the ref's CommitID as the parent, if the ref exists:
            if ((rf != null) && (cb.Parents.Count == 0))
            {
                cb.Parents.Add(rf.CommitID);
            }

            // Persist the commit:
            var epcm = await cms.cmrepo.PersistCommit(cb);
            if (epcm.HasErrors) return ErrorJson(epcm);

            Commit pcm = epcm.Value;

            // Persist the ref with this new CommitID:
            Ref.Builder rfb = new Ref.Builder(refName, pcm.ID);
            erf = await cms.rfrepo.PersistRef(rfb);
            if (erf.HasErrors) return ErrorJson(erf);

            // Return the commit model as JSON again:
            return Json(new { @ref = rf.ToJSON(), commit = pcm.ToJSON() }, JsonRequestBehavior.AllowGet);
        }
    }
}
