using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.Definition.Models;

namespace IVO.CMS.Web.Mvc
{
    public sealed class RenderBlobResult : ActionResult
    {
        private RenderingSystemContext renderer;
        private CanonicalBlobPath path;

        public RenderBlobResult(RenderingSystemContext renderer, CanonicalBlobPath path)
        {
            this.renderer = renderer;
            this.path = path;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            var rsp = context.HttpContext.Response;

            var eblobTask = renderer.RenderBlobAsync(path);
            eblobTask.RunSynchronously();

            if (eblobTask.Result.HasErrors)
            {
                rsp.ContentType = "application/xhtml+xml";
                rsp.ContentEncoding = Encoding.UTF8;
                rsp.StatusCode = 500;

                // Render the errors:
                rsp.Output.WriteLine("<!DOCTYPE html>");
                rsp.Output.WriteLine();
                rsp.Output.WriteLine("<html><head><title>CMS Errors</title></head>");
                // TODO: inline styling!
                rsp.Output.WriteLine("<body>");
                rsp.Output.WriteLine("<ul>");
                foreach (var err in eblobTask.Result.Errors)
                    rsp.Output.WriteLine("<li>{0}: {1}</li>", err.GetType().FullName, System.Web.HttpUtility.HtmlEncode(err.Message));
                rsp.Output.WriteLine("</ul>");
                rsp.Output.WriteLine("</body></html>");
                return;
            }

            // Render the blob:
            rsp.ContentType = "application/xhtml+xml";
            rsp.ContentEncoding = Encoding.UTF8;
            rsp.StatusCode = 200;

            string text = (string)eblobTask.Result.Value;
            rsp.Output.Write(text);
        }
    }
}
