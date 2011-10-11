using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using DiffPlex;
using DiffPlex.DiffBuilder;
using IVO.CMS.API.Code;
using IVO.CMS.API.Models;
using IVO.CMS.Web.Mvc;
using IVO.Definition.Errors;
using IVO.Definition.Models;
using IVO.Definition.Containers;

namespace IVO.CMS.API.Controllers
{
    public class BlobController : TaskAsyncController
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
        [ActionName("get")]
        [JsonHandleError]
        public async Task<ActionResult> GetBlob(Errorable<BlobID.Partial> epid)
        {
            if (epid.HasErrors) return ErrorJson(epid);

            var eid = await cms.blrepo.ResolvePartialID(epid.Value);
            if (eid.HasErrors) return ErrorJson(eid);

            var eblob = await cms.blrepo.GetBlob(eid.Value);
            if (eblob.HasErrors) return ErrorJson(eblob);

            return new StreamedBlobResult(eblob.Value);
        }

        [HttpGet]
        [ActionName("getByPath")]
        [JsonHandleError]
        public async Task<ActionResult> GetBlobByPath(Errorable<TreeBlobPath> epath)
        {
            if (epath.HasErrors) return ErrorJson(epath);

            var eblob = await cms.tpsbrepo.GetBlobByTreePath(epath.Value);
            if (eblob.HasErrors) return ErrorJson(eblob);

            TreePathStreamedBlob blob = eblob.Value;
            Debug.Assert(blob != null);

            return new StreamedBlobResult(blob.StreamedBlob);
        }

        [HttpPost]
        [ActionName("create")]
        [JsonHandleError]
        public async Task<ActionResult> CreateBlob(Errorable<TreeBlobPath> epath, Errorable<StageName> estage)
        {
            Debug.Assert(epath != null);
            //if (path == null) return Json(new { errors = new[] { new { message = "path required" } } }, JsonRequestBehavior.AllowGet);
            if (epath.HasErrors) return ErrorJson(epath);
            if (estage != null && estage.HasErrors) return ErrorJson(estage);

            PersistingBlob pbl = new PersistingBlob(Request.InputStream);

            // Persist the blob from the input stream:
            var eblob = await cms.blrepo.PersistBlob(pbl);
            if (eblob.HasErrors) return ErrorJson(eblob);
            var blob = eblob.Value;

            // Now update the given root TreeID:
            var path = epath.Value;

            // Persist the new blob's effect on the Tree:
            var eptr = await cms.trrepo.PersistTreeNodesByBlobPaths(path.RootTreeID, new CanonicalBlobIDPath[] { new CanonicalBlobIDPath(path.Path, blob.ID) });
            if (eptr.HasErrors) return ErrorJson(eptr);

            TreeID newRootTreeID = eptr.Value.RootID;

            // optional 5) update stage with new root TreeID
            if (estage != null)
            {
                var epst = await cms.strepo.PersistStage(new Stage.Builder(estage.Value, newRootTreeID));
                if (epst.HasErrors) return ErrorJson(epst);
            }

            // Return the new information:
            return Json(new { blobid = blob.ID.ToString(), treeid = newRootTreeID.ToString() });
        }

        [HttpPost]
        [ActionName("validate")]
        [JsonHandleError]
        public ActionResult ValidateBlob()
        {
            // Validate the blob's contents:
            var vr = cms.GetContentEngine().ValidateBlob(Request.InputStream);

            // Return the validation results:
            return Json(new
            {
                successful = (vr == null),
                error = (vr == null) ? (object)null : new { message = vr.Message, lineNumber = vr.LineNumber, linePosition = vr.LinePosition }
            });
        }

        [HttpGet]
        [ActionName("compare")]
        [JsonHandleError]
        public async Task<ActionResult> CompareBlobs(Errorable<BlobID.Partial> epida, Errorable<BlobID.Partial> epidb)
        {
            if (epida.HasErrors || epidb.HasErrors) return Json(new { errors = (epida.Errors + epidb.Errors).ToJSON() }, JsonRequestBehavior.AllowGet);

            // Resolve the partial IDs:
            var eids = await cms.blrepo.ResolvePartialIDs(epida.Value, epidb.Value);
            if (eids[0].HasErrors || eids[1].HasErrors) return Json(new { errors = (eids[0].Errors + eids[1].Errors).ToJSON() }, JsonRequestBehavior.AllowGet);

            BlobID idA = eids[0].Value;
            BlobID idB = eids[1].Value;

            // Get the Blobs:
            var ebls = await cms.blrepo.GetBlobs(idA, idB);
            if (ebls[0].HasErrors || ebls[1].HasErrors) return Json(new { errors = (ebls[0].Errors + ebls[1].Errors).ToJSON() }, JsonRequestBehavior.AllowGet);

            IStreamedBlob blA = ebls[0].Value, blB = ebls[1].Value;

            // Stream in both blobs' contents to string values:
            var etextA = await blA.ReadStreamAsync<string>(async st => { using (var sr = new StreamReader(st, Encoding.UTF8)) return (Errorable<string>)await sr.ReadToEndAsync(); });
            if (etextA.HasErrors) return ErrorJson(etextA);
            
            var etextB = await blB.ReadStreamAsync<string>(async st => { using (var sr = new StreamReader(st, Encoding.UTF8)) return (Errorable<string>)await sr.ReadToEndAsync(); });
            if (etextB.HasErrors) return ErrorJson(etextB);

            // TODO: update to a better diff engine that supports merging...

            // Create a diff engine:
            IDiffer differ = new Differ();
            ISideBySideDiffBuilder builder = new SideBySideDiffBuilder(differ);

            // Run the diff engine to get the output model:
            // NOTE: I would prefer it to read in via a Stream but perhaps that is not possible given the algorithm implemented.
            var sxs = builder.BuildDiffModel(etextA.Value, etextB.Value);

            // Return the result as JSON:
            return Json(sxs.ToJSON(), JsonRequestBehavior.AllowGet);
        }
    }
}
