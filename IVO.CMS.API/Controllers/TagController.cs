using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
using IVO.CMS.Web.Mvc;
using IVO.Definition.Models;
using IVO.CMS.API.Models;
using System.Collections.ObjectModel;

namespace IVO.CMS.API.Controllers
{
    [JsonHandleError]
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
            if (tg == null) return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            return Json(new { tag = tg.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getByName")]
        public async Task<ActionResult> GetTagByName(TagName tagName)
        {
            if (tagName == null) return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var tg = await cms.tgrepo.GetTagByName(tagName);
            if (tg == null) return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            return Json(new { tag = tg.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("query")]
        public async Task<ActionResult> Query(DateTimeOffset? from, DateTimeOffset? to, string name, string tagger, string[] ob, int? ps, int? pn)
        {
            // Convert the simple filtering criteria:
            TagQuery tq = new TagQuery(from, to, name, tagger);

            // Is ordering desired? Possibly paging?
            if (ob != null)
            {
                // Convert the JSON model of ordering:
                var orderBy = new ReadOnlyCollection<OrderByApplication<TagOrderBy>>(
                    ob.SelectAsArray(e => e.Split(':').With(spl => new OrderByApplication<TagOrderBy>(convertTagOrderBy(spl[0]), convertDirection(spl[1]))))
                );

                // Determine if paging is requested and is valid:
                if (ps.HasValue && pn.HasValue && ps.Value > 0 && pn > 0)
                {
                    // Paging looks valid:
                    var results = await cms.tgrepo.SearchTags(tq, orderBy, new PagingRequest(pn.Value, ps.Value));

                    return Json(new { results = results }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    // No paging or invalid paging parameters:
                    var results = await cms.tgrepo.SearchTags(tq, orderBy);

                    return Json(new { results = results }, JsonRequestBehavior.AllowGet);
                }
            }
            else
            {
                // No ordering or paging:
                var results = await cms.tgrepo.SearchTags(tq);

                return Json(new { results = results }, JsonRequestBehavior.AllowGet);
            }
        }

        private Definition.Models.OrderByDirection convertDirection(string orderByDirection)
        {
            switch (orderByDirection)
            {
                case "asc": return Definition.Models.OrderByDirection.Ascending;
                case "desc": return Definition.Models.OrderByDirection.Descending;
                default: return Definition.Models.OrderByDirection.Ascending;
            }
        }

        private TagOrderBy convertTagOrderBy(string ob)
        {
            switch (ob)
            {
                case "date_tagged": return TagOrderBy.DateTagged;
                case "name": return TagOrderBy.Name;
                case "tagger": return TagOrderBy.Tagger;
                default: return TagOrderBy.DateTagged;
            }
        }

        [HttpPost]
        [ActionName("create")]
        public async Task<ActionResult> Create(TagModel tgj)
        {
            if (tgj == null) return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            // Map from the JSON TagModel:
            Tag tg = tgj.FromJSON();

            // Persist the commit:
            var ptg = await cms.tgrepo.PersistTag(tg);

            // Return the tag model as JSON again:
            return Json(new { tag = ptg.ToJSON() }, JsonRequestBehavior.AllowGet);
        }
    }
}
