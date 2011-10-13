using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using IVO.CMS.API.Code;
using IVO.CMS.Web.Internal.Mvc;
using IVO.Definition.Models;
using IVO.CMS.API.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using IVO.Definition.Errors;

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

        private JsonResult ErrorJson<T>(Errorable<T> errored)
        {
            return Json(new { errors = errored.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        #endregion

        [HttpGet]
        [ActionName("getByID")]
        public async Task<ActionResult> GetTagByID(Errorable<TagID.Partial> epid)
        {
            if (epid.HasErrors) return ErrorJson(epid);

            var eid = await cms.tgrepo.ResolvePartialID(epid.Value);
            if (eid.HasErrors) return ErrorJson(eid);

            var etg = await cms.tgrepo.GetTag(eid.Value);
            if (etg.HasErrors) return ErrorJson(etg);

            Tag tg = etg.Value;
            Debug.Assert(tg != null);

            return Json(new { tag = tg.ToJSON() }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [ActionName("getByName")]
        public async Task<ActionResult> GetTagByName(Errorable<TagName> etagName)
        {
            if (etagName.HasErrors) return ErrorJson(etagName);

            var etg = await cms.tgrepo.GetTagByName(etagName.Value);
            if (etg.HasErrors) return Json(new { errors = etg.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            Tag tg = etg.Value;
            Debug.Assert(tg != null);

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
                // Convert the ordering instructions "column:asc,column:desc,...":
                var orderBy = new ReadOnlyCollection<OrderByApplication<TagOrderBy>>(
                    (
                        from o in ob
                        let spl = o.Split(':')
                        let tgob = convertTagOrderBy(spl[0])
                        let dir = spl.Length > 1 ? convertDirection(spl[1]) : defaultOrderBy(tgob)
                        select new OrderByApplication<TagOrderBy>(tgob, dir)
                    ).ToArray(ob.Length)
                );

                // Determine if paging is requested and is valid:
                if (ps.HasValue && pn.HasValue && ps.Value > 0 && pn > 0)
                {
                    // Paging looks valid:
                    var results = await cms.tgrepo.SearchTags(tq, orderBy, new PagingRequest(pn.Value, ps.Value));

                    return Json(new
                    {
                        results = new
                        {
                            count = results.TotalCount,
                            pageCount = results.PageCount,
                            pageNumber = results.Paging.PageNumber,
                            pageSize = results.Paging.PageSize,
                            isFirstPage = results.IsFirstPage,
                            isLastPage = results.IsLastPage,
                            orderedBy = results.OrderedBy.SelectAsArray(x => new { dir = convertDirection(x.Direction), by = convertTagOrderBy(x.OrderBy) }),
                            page = results.Collection.SelectAsArray(tg => tg.ToJSON())
                        }
                    }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    // No paging or invalid paging parameters:
                    var results = await cms.tgrepo.SearchTags(tq, orderBy);

                    return Json(new
                    {
                        results = new
                        {
                            count = results.Collection.Count,
                            orderedBy = results.OrderedBy.SelectAsArray(x => new { dir = convertDirection(x.Direction), by = convertTagOrderBy(x.OrderBy) }),
                            items = results.Collection.SelectAsArray(tg => tg.ToJSON())
                        }
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            else
            {
                // No ordering or paging:
                var results = await cms.tgrepo.SearchTags(tq);

                return Json(new
                {
                    results = new
                    {
                        count = results.Collection.Count,
                        items = results.Collection.SelectAsArray(tg => tg.ToJSON())
                    }
                }, JsonRequestBehavior.AllowGet);
            }
        }

        private OrderByDirection defaultOrderBy(TagOrderBy tgob)
        {
            switch (tgob)
            {
                case TagOrderBy.DateTagged: return OrderByDirection.Descending;
                case TagOrderBy.Name: return OrderByDirection.Ascending;
                case TagOrderBy.Tagger: return OrderByDirection.Ascending;
                default: return OrderByDirection.Ascending;
            }
        }

        private string convertDirection(OrderByDirection dir)
        {
            switch (dir)
            {
                case OrderByDirection.Ascending: return "asc";
                case OrderByDirection.Descending: return "desc";
                default: return "asc";
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

        private string convertTagOrderBy(TagOrderBy ob)
        {
            switch (ob)
            {
                case TagOrderBy.DateTagged: return "date_tagged";
                case TagOrderBy.Name: return "name";
                case TagOrderBy.Tagger: return "tagger";
                default: return String.Empty;
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
        public async Task<ActionResult> Create(TagRequest tgj)
        {
            if (tgj == null) return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            // Map from the JSON TagModel:
            var etgb = tgj.FromJSON();
            if (etgb.HasErrors) return Json(new { errors = etgb.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            Tag tg = etgb.Value;

            // Persist the commit:
            var eptg = await cms.tgrepo.PersistTag(tg);
            if (eptg.HasErrors) return Json(new { errors = eptg.Errors.ToJSON() }, JsonRequestBehavior.AllowGet);

            // Return the tag model as JSON again:
            return Json(new { tag = eptg.Value.ToJSON() }, JsonRequestBehavior.AllowGet);
        }
    }
}
