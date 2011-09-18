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
            if (blobs.Length == 0)
                return new EmptyResult();

            return new StreamedBlobResult(blobs[0]);
        }

        [HttpGet]
        [ActionName("getByPath")]
        public async Task<ActionResult> GetBlobByPath(TreeID root, CanonicalBlobPath path)
        {
            var blob = await cms.tpsbrepo.GetBlobByTreePath(new TreeBlobPath(root, path));

            return new StreamedBlobResult(blob.StreamedBlob);
        }

        [HttpGet]
        [ActionName("render")]
        public async Task<ActionResult> RenderBlob(TreeID root, CanonicalBlobPath path, DateTimeOffset? viewDate)
        {
            TreeBlobPath tbp = new TreeBlobPath(root, path);

            // Get the stream for the blob by its path:
            var blob = await cms.tpsbrepo.GetBlobByTreePath(tbp);

            // TODO: streaming output!
            var html = await cms.GetContentEngine(viewDate).RenderBlob(blob);

            // HTML5 output:
            return Content((string)html, "application/xhtml+xml", Encoding.UTF8);
        }

        [HttpPost]
        [ActionName("create")]
        public async Task<ActionResult> CreateBlob(string contents)
        {
            PersistingBlob pbl = new PersistingBlob(Request.InputStream);

            // Persist the blob from the input stream:
            var blobs = await cms.blrepo.PersistBlobs(pbl);

            // Return the BlobID:
            return Json(new { id = blobs[0].ID.ToString() });
        }
    }
}
