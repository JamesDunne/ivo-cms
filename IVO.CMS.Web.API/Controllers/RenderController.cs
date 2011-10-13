using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
using IVO.CMS.API.Models;
using IVO.Definition.Models;
using IVO.CMS.Web.Internal.Mvc;
using IVO.Definition.Errors;
using System.Diagnostics;
using IVO.CMS.Web.API.Code;
using IVO.CMS.Web;

namespace IVO.CMS.API.Controllers
{
    public class RenderController : CMSTaskAsyncController
    {
        [HttpGet]
        [ActionName("renderByTree")]
        [ValidateInput(false)]
        public async Task<ActionResult> RenderBlob(Errorable<TreeBlobPath> epath, DateTimeOffset? viewDate)
        {
            Debug.Assert(epath != null);
            if (epath.HasErrors) return ErrorJson(epath);
            var path = epath.Value;

            // Get the stream for the blob by its path:
            var eblob = await cms.tpsbrepo.GetBlobByTreePath(epath.Value);
            if (eblob.HasErrors) return ErrorJson(eblob);

            TreePathStreamedBlob blob = eblob.Value;
            if (blob == null) return new HttpNotFoundResult(String.Format("A blob could not be found off tree {0} by path '{0}'", path.RootTreeID.ToString(), path.Path.ToString()));

            // Render the blob:
            var renderer = new RenderingSystemContext(cms, viewDate);
            var ehtml = await renderer.Engine.RenderBlob(blob);
            if (ehtml.HasErrors) return ErrorJson(ehtml);

            var html = ehtml.Value;

            // HTML5 output:
            return Content((string)html, "application/xhtml+xml", Encoding.UTF8);
        }
    }
}
