using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using IVO.Definition.Models;

namespace IVO.CMS.API.Code
{
    public sealed class StreamedBlobResult : ActionResult
    {
        private IStreamedBlob blob;
        public StreamedBlobResult(IStreamedBlob blob)
        {
            this.blob = blob;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            if (blob == null)
            {
                context.HttpContext.Response.End();
                return;
            }

            var rsp = context.HttpContext.Response;
            rsp.BufferOutput = false;
            rsp.ContentEncoding = Encoding.UTF8;
            rsp.ContentType = "application/xhtml+xml";

            // TODO: this kinda sucks; I'd like to see truly async streaming via MVC.
            blob.ReadStream(sr => { sr.CopyTo(rsp.OutputStream); });
        }
    }
}
