﻿using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
using IVO.CMS.API.Models;
using IVO.CMS.Web.Mvc;
using IVO.Definition.Errors;
using IVO.Definition.Models;

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

        private JsonResult ErrorJson<T>(Errorable<T> errored)
        {
            return Json(new { errors = errored.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        #endregion

        [HttpGet]
        [ActionName("getByName")]
        public async Task<ActionResult> GetRefByName(Errorable<RefName> refName)
        {
            if (refName.HasErrors) return ErrorJson(refName);

            var erf = await cms.rfrepo.GetRefByName(refName.Value);
            if (erf.HasErrors) return ErrorJson(erf);

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
            if (erf.HasErrors) return ErrorJson(erf);

            Ref rf = erf.Value;

            // Persist the commit:
            var eprf = await cms.rfrepo.PersistRef(rf);
            if (eprf.HasErrors) return ErrorJson(eprf);

            Ref prf = eprf.Value;

            // Return the ref model as JSON again:
            return Json(new { @ref = prf.ToJSON() }, JsonRequestBehavior.AllowGet);
        }
    }
}
