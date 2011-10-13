using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
using IVO.CMS.API.Models;
using IVO.CMS.Web.API.Code;
using IVO.CMS.Web.Internal.Mvc;
using IVO.Definition.Errors;
using IVO.Definition.Models;

namespace IVO.CMS.API.Controllers
{
    [JsonHandleError]
    public class RefController : CMSTaskAsyncController
    {
        [HttpGet]
        [ActionName("getByName")]
        public async Task<ActionResult> GetRefByName(Errorable<RefName> erefName)
        {
            if (erefName.HasErrors) return ErrorJson(erefName);

            var erf = await cms.rfrepo.GetRefByName(erefName.Value);
            if (erf.HasErrors) return ErrorJson(erf);

            Ref rf = erf.Value;
            return Json(new { @ref = rf.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getAll")]
        public Task<ActionResult> GetAllRefs()
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
