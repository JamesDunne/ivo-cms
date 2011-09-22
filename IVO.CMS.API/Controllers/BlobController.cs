using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
using IVO.CMS.API.Models;
using IVO.CMS.Web.Mvc;
using IVO.Definition.Containers;
using IVO.Definition.Models;
using System.Text;

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
        public async Task<ActionResult> GetBlob(BlobID id)
        {
            var blobs = await cms.blrepo.GetBlobs(id);
            if (blobs.Length == 0) return new HttpNotFoundResult(String.Format("A blob could not be found by id {0}", id.ToString()));

            return new StreamedBlobResult(blobs[0]);
        }

        [HttpGet]
        [ActionName("getByPath")]
        public async Task<ActionResult> GetBlobByPath(TreeBlobPath rootedPath)
        {
            var blob = await cms.tpsbrepo.GetBlobByTreePath(rootedPath);
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
            var blobs = await cms.blrepo.PersistBlobs(pbl);

            // Return the BlobID:
            return Json(new { id = blobs[0].ID.ToString() });
        }

        [HttpPost]
        [ActionName("validate")]
        [JsonHandleError]
        public ActionResult ValidateBlob()
        {
            // Validate the blob's contents:
            var vr = cms.GetContentEngine().ValidateBlob(Request.InputStream);

            // Return the validation results:
            return Json(new {
                successful = (vr == null),
                error = (vr == null) ? (object)null : new { message = vr.Message, lineNumber = vr.LineNumber, linePosition = vr.LinePosition }
            });
        }
    }
}
