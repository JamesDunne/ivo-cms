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

        [HttpGet]
        [ActionName("query")]
        public async Task<ActionResult> Query(TagQueryModel qm)
        {
            if (qm == null) qm = new TagQueryModel();

            // Convert the simple filtering criteria:
            TagQuery tq = new TagQuery(qm.dateFrom, qm.dateTo, qm.name, qm.tagger);

            // Is ordering desired? Possibly paging?
            if (qm.ordering != null)
            {
                // Convert the JSON model of ordering:
                var orderBy = new ReadOnlyCollection<OrderByApplication<TagOrderBy>>(
                    qm.ordering.SelectAsArray(ob => new OrderByApplication<TagOrderBy>(convertTagOrderBy(ob.by), convertDirection(ob.dir)))
                );

                // Determine if paging is requested and is valid:
                if (qm.pageSize.HasValue && qm.pageNumber.HasValue && qm.pageSize.Value > 0 && qm.pageNumber > 0)
                {
                    // Paging looks valid:
                    var results = await cms.tgrepo.SearchTags(tq, orderBy, new PagingRequest(qm.pageNumber.Value, qm.pageSize.Value));

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

        private Definition.Models.OrderByDirection convertDirection(Models.OrderByDirModel orderByDirection)
        {
            switch (orderByDirection)
            {
                case Models.OrderByDirModel.asc: return Definition.Models.OrderByDirection.Ascending;
                case Models.OrderByDirModel.desc: return Definition.Models.OrderByDirection.Descending;
                default: return Definition.Models.OrderByDirection.Ascending;
            }
        }

        private TagOrderBy convertTagOrderBy(TagQueryModel.OrderBy ob)
        {
            switch (ob)
            {
                case TagQueryModel.OrderBy.date_tagged: return TagOrderBy.DateTagged;
                case TagQueryModel.OrderBy.name: return TagOrderBy.Name;
                case TagQueryModel.OrderBy.tagger: return TagOrderBy.Tagger;
                default: return TagOrderBy.DateTagged;
            }
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
