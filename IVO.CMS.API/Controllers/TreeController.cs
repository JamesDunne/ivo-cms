using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
using IVO.CMS.Web.Mvc;
using IVO.Definition.Models;
using IVO.Definition.Containers;

namespace IVO.CMS.API.Controllers
{
    public class TreeController : TaskAsyncController
    {
        private CMSContext cms;

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            this.cms = new CMSContext(new DirectoryInfo(Server.MapPath("~/ivo/")));

            base.OnActionExecuting(filterContext);
        }

        [HttpGet]
        [ActionName("get")]
        public async Task<ActionResult> GetTreeByID(TreeID id)
        {
            var tr = await cms.trrepo.GetTree(id);

            return Json(new { tr }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ActionName("create")]
        public async Task<ActionResult> CreateTree(TreeModel tm)
        {
            // TODO: model-binding for TreeModel.
            var tr = await cms.trrepo.PersistTree(tm.root, tm.trees);

            return Json(new { tr });
        }
    }
}
