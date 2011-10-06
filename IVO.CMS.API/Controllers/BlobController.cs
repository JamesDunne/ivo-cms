using System;
using System.IO;
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
using System.Diagnostics;

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
        public async Task<ActionResult> GetBlob(Errorable<BlobID.Partial> id)
        {
            if (id.HasErrors) return ErrorJson(id);

            var eid = await cms.blrepo.ResolvePartialID(id.Value);
            if (eid.HasErrors) return ErrorJson(eid);

            var eblob = await cms.blrepo.GetBlob(eid.Value);
            if (eblob.HasErrors) return ErrorJson(eblob);

            return new StreamedBlobResult(eblob.Value);
        }

        [HttpGet]
        [ActionName("getByPath")]
        [JsonHandleError]
        public async Task<ActionResult> GetBlobByPath(Errorable<TreeBlobPath> rootedPath)
        {
            if (rootedPath.HasErrors) return ErrorJson(rootedPath);

            var eblob = await cms.tpsbrepo.GetBlobByTreePath(rootedPath.Value);
            if (eblob.HasErrors) return ErrorJson(eblob);

            TreePathStreamedBlob blob = eblob.Value;
            Debug.Assert(blob != null);

            return new StreamedBlobResult(blob.StreamedBlob);
        }

        [HttpPost]
        [ActionName("create")]
        [JsonHandleError]
        public async Task<ActionResult> CreateBlob(Errorable<TreeBlobPath> path, Errorable<StageName> stage = null)
        {
            if (path.HasErrors) return ErrorJson(path);
            if (stage != null && stage.HasErrors) return ErrorJson(stage);

            PersistingBlob pbl = new PersistingBlob(Request.InputStream);

            // Persist the blob from the input stream:
            var eblob = await cms.blrepo.PersistBlob(pbl);
            if (eblob.HasErrors) return ErrorJson(eblob);

            // Now update the given root TreeID:
            // TODO:
            // 1) get minimal set of tree nodes recursively from root to leaf where the BlobID should be updated
            // 2) update leaf tree node with new blob information
            //    add or update blob
            //    add new tree nodes as appropriate
            // 3) update each parent node with new TreeID of child
            // 4) persist all affected tree nodes
            // optional 5) update stage with new root TreeID
            // 6) return new root TreeID along with new BlobID

            // Return the BlobID:
            return Json(new { id = eblob.Value.ID.ToString() });
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
        public async Task<ActionResult> CompareBlobs(Errorable<BlobID.Partial> id, Errorable<BlobID.Partial> against)
        {
            if (id.HasErrors || against.HasErrors) return Json(new { errors = (id.Errors + against.Errors).ToJSON() }, JsonRequestBehavior.AllowGet);

            // Resolve the partial IDs:
            var eids = await cms.blrepo.ResolvePartialIDs(id.Value, against.Value);
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
