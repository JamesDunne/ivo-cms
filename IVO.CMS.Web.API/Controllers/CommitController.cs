using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
using IVO.CMS.API.Models;
using IVO.CMS.Web.Internal.Mvc;
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

        #endregion

        [HttpGet]
        [ActionName("getByID")]
        public async Task<ActionResult> GetCommitByID(Errorable<CommitID.Partial> epid)
        {
            if (epid.HasErrors) return ErrorJson(epid);

            var eid = await cms.cmrepo.ResolvePartialID(epid.Value);
            if (eid.HasErrors) return ErrorJson(eid);

            var ecm = await cms.cmrepo.GetCommit(eid.Value);
            if (ecm.HasErrors) return ErrorJson(ecm);

            Commit cm = ecm.Value;

            return Json(new { commit = cm.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getByRef")]
        public async Task<ActionResult> GetCommitByRefName(Errorable<RefName> erefName)
        {
            if (erefName.HasErrors) return ErrorJson(erefName);

            var ecm = await cms.cmrepo.GetCommitByRefName(erefName.Value);
            if (ecm.HasErrors) return ErrorJson(ecm);

            var cm = ecm.Value;

            return Json(new { @ref = cm.Item1.ToJSON(), commit = cm.Item2.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getByTag")]
        public async Task<ActionResult> GetCommitByTagName(Errorable<TagName> etagName)
        {
            if (etagName.HasErrors) return ErrorJson(etagName);

            var ecm = await cms.cmrepo.GetCommitByTagName(etagName.Value);
            if (ecm.HasErrors) return ErrorJson(ecm);

            var cm = ecm.Value;

            return Json(new { tag = cm.Item1.ToJSON(), commit = cm.Item2.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getTree")]
        public async Task<ActionResult> GetCommitTree(Errorable<CommitID.Partial> epid, int depth = 10)
        {
            if (epid.HasErrors) return ErrorJson(epid);

            // Attempt to resolve the partial ID:
            var eid = await cms.cmrepo.ResolvePartialID(epid.Value);
            if (eid.HasErrors) return ErrorJson(eid);
            
            var ecmtr = await cms.cmrepo.GetCommitTree(eid.Value, depth);
            if (ecmtr.HasErrors) return ErrorJson(ecmtr);

            CommitTree cmtr = ecmtr.Value;

            return Json(new { depth = depth, commit_tree = toJSON(cmtr.RootID, cmtr.Commits) }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getTreeByTag")]
        public async Task<ActionResult> GetCommitTree(Errorable<TagName> etagName, int depth = 10)
        {
            if (etagName.HasErrors) return ErrorJson(etagName);

            var ecmtr = await cms.cmrepo.GetCommitTreeByTagName(etagName.Value, depth);
            if (ecmtr.HasErrors) return ErrorJson(ecmtr);

            Tuple<Tag, CommitTree> cmtr = ecmtr.Value;

            return Json(new { tag = cmtr.Item1.ToJSON(), depth = depth, commit_tree = toJSON(cmtr.Item2.RootID, cmtr.Item2.Commits) }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getTreeByRef")]
        public async Task<ActionResult> GetCommitTree(Errorable<RefName> erefName, int depth = 10)
        {
            if (erefName.HasErrors) return ErrorJson(erefName);

            var ecmtr = await cms.cmrepo.GetCommitTreeByRefName(erefName.Value, depth);
            if (ecmtr.HasErrors) return ErrorJson(ecmtr);

            Tuple<Ref, CommitTree> cmtr = ecmtr.Value;

            return Json(new { @ref = cmtr.Item1.ToJSON(), depth = depth, commit_tree = toJSON(cmtr.Item2.RootID, cmtr.Item2.Commits) }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ActionName("create")]
        public async Task<ActionResult> Create(Errorable<RefName> erefName, CommitRequest cmj)
        {
            if (erefName.HasErrors) return ErrorJson(erefName);
            if (cmj == null) return Json(new { success = false });

            // First get the ref and its CommitID, if it exists:
            Ref rf;
            var erf = await cms.rfrepo.GetRefByName(erefName.Value);
            if (erf.HasErrors)
            {
                // Skip the RefNameDoesNotExistError error (should only be one - using All() makes sure that any other errors will fall out):
                if (!erf.Errors.All(err => err is RefNameDoesNotExistError))
                    return ErrorJson(erf);
                rf = null;
            }
            else rf = erf.Value;

            // Map from the JSON CommitModel:
            var ecb = cmj.FromJSON();
            if (ecb.HasErrors) return ErrorJson(ecb);

            Commit.Builder cb = ecb.Value;
            
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
            Ref.Builder rfb = new Ref.Builder(erefName.Value, pcm.ID);
            erf = await cms.rfrepo.PersistRef(rfb);
            if (erf.HasErrors) return ErrorJson(erf);

            rf = erf.Value;

            // Return the commit model as JSON again:
            return Json(new { @ref = rf.ToJSON(), commit = pcm.ToJSON() }, JsonRequestBehavior.AllowGet);
        }
    }
}
