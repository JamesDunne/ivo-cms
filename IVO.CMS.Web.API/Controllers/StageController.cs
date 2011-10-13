using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
using IVO.CMS.API.Models;
using IVO.CMS.Web.Internal.Mvc;
using IVO.Definition.Errors;
using IVO.Definition.Models;

namespace IVO.CMS.API.Controllers
{
    [JsonHandleError]
    public class StageController : TaskAsyncController
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
        [ActionName("getByName")]
        public async Task<ActionResult> GetStageByName(Errorable<StageName> estageName)
        {
            if (estageName.HasErrors) return ErrorJson(estageName);

            var est = await cms.strepo.GetStageByName(estageName.Value);
            if (est.HasErrors) return ErrorJson(est);

            Stage st = est.Value;
            return Json(new { stage = st.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ActionName("create")]
        public async Task<ActionResult> Create(StageRequest stj)
        {
            if (stj == null) return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            // Map from the JSON RefModel:
            var est = stj.FromJSON();
            if (est.HasErrors) return ErrorJson(est);

            Stage st = est.Value;

            // Persist the commit:
            var epst = await cms.strepo.PersistStage(st);
            if (epst.HasErrors) return ErrorJson(epst);

            Stage pst = epst.Value;

            // Return the ref model as JSON again:
            return Json(new { stage = pst.ToJSON() }, JsonRequestBehavior.AllowGet);
        }
    }
}
