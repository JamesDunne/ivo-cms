using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
using IVO.CMS.Web.Mvc;
using IVO.Definition.Models;
using IVO.CMS.API.Models;

namespace IVO.CMS.API.Controllers
{
    public class TagController : TaskAsyncController
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
        [ActionName("getByID")]
        public async Task<ActionResult> GetTagByID(TagID id)
        {
            var tg = await cms.tgrepo.GetTag(id);

            return Json(new { tag = tg.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getByName")]
        public async Task<ActionResult> GetTagByName(TagName tagName)
        {
            if (tagName == null) return new EmptyResult();

            var tg = await cms.tgrepo.GetTagByName(tagName);

            return Json(new { tag = tg.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ActionName("create")]
        public async Task<ActionResult> Create(TagModel tgj)
        {
            if (tgj == null) return new EmptyResult();

            // Map from the JSON TagModel:
            Tag tg = tgj.FromJSON();

            // Persist the commit:
            var ptg = await cms.tgrepo.PersistTag(tg);

            // Return the tag model as JSON again:
            return Json(new { tag = ptg.ToJSON() }, JsonRequestBehavior.AllowGet);
        }
    }
}
