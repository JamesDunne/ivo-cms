﻿using System;
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

        #endregion

        [HttpGet]
        [ActionName("get")]
        [JsonHandleError]
        public async Task<ActionResult> GetBlob(Errorable<BlobID.Partial> id)
        {
            if (id.HasErrors) return Json(new { errors = id.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            var eid = await cms.blrepo.ResolvePartialID(id.Value);
            if (eid.HasErrors) return Json(new { errors = eid.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            var eblob = await cms.blrepo.GetBlob(eid.Value);
            if (eblob.HasErrors) return Json(new { errors = eblob.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            return new StreamedBlobResult(eblob.Value);
        }

        [HttpGet]
        [ActionName("getByPath")]
        [JsonHandleError]
        public async Task<ActionResult> GetBlobByPath(TreeBlobPath rootedPath)
        {
            var eblob = await cms.tpsbrepo.GetBlobByTreePath(rootedPath);
            if (eblob.HasErrors) return Json(new { errors = eblob.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            TreePathStreamedBlob blob = eblob.Value;
            if (blob == null) return new HttpNotFoundResult(String.Format("A blob could not be found off tree {0} by path '{0}'", rootedPath.RootTreeID.ToString(), rootedPath.Path.ToString()));

            return new StreamedBlobResult(blob.StreamedBlob);
        }

        [HttpPost]
        [ActionName("create")]
        [JsonHandleError]
        public async Task<ActionResult> CreateBlob()
        {
            PersistingBlob pbl = new PersistingBlob(Request.InputStream);

            // Persist the blob from the input stream:
            var eblob = await cms.blrepo.PersistBlob(pbl);
            if (eblob.HasErrors) return Json(new { errors = eblob.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

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

            // Stream in both blobs' contents to two string values:
            var etextA = await blA.ReadStreamAsync<string>(async st => { using (var sr = new StreamReader(st, Encoding.UTF8)) return (Errorable<string>)await sr.ReadToEndAsync(); });
            var etextB = await blB.ReadStreamAsync<string>(async st => { using (var sr = new StreamReader(st, Encoding.UTF8)) return (Errorable<string>)await sr.ReadToEndAsync(); });

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
