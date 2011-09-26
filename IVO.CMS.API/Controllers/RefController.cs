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
    [JsonHandleError]
    public class RefController : TaskAsyncController
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
        [ActionName("getByName")]
        public async Task<ActionResult> GetRefByName(RefName refName)
        {
            if (refName == null) return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var erf = await cms.rfrepo.GetRefByName(refName);
            if (erf.HasErrors) return Json(new { errors = erf.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            Ref rf = erf.Value;
            return Json(new { @ref = rf.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getRefs")]
        public Task<ActionResult> GetRefs()
        {
            throw new NotImplementedException();
        }

        [HttpPost]
        [ActionName("create")]
        public async Task<ActionResult> Create(RefRequest rfj)
        {
            if (rfj == null) return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            // Map from the JSON RefModel:
            var erf = rfj.FromJSON();
            if (erf.HasErrors) return Json(new { errors = erf.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            Ref rf = erf.Value;

            // Persist the commit:
            var eprf = await cms.rfrepo.PersistRef(rf);
            if (eprf.HasErrors) return Json(new { errors = eprf.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            Ref prf = eprf.Value;

            // Return the ref model as JSON again:
            return Json(new { @ref = prf.ToJSON() }, JsonRequestBehavior.AllowGet);
        }
    }
}
