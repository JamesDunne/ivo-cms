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
            var etg = await cms.tgrepo.GetTag(id);
            if (etg.IsRight) return Json(new { error = etg.Right.ToJSON() }, JsonRequestBehavior.AllowGet);

            return Json(new { tag = etg.Left.ToJSON() }, JsonRequestBehavior.AllowGet);
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
                        count = results.Count,
                        items = results.SelectAsArray(tg => tg.ToJSON())
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
            Tag tg = tgj.FromJSON();

            // Persist the commit:
            var ptg = await cms.tgrepo.PersistTag(tg);

            // Return the tag model as JSON again:
            return Json(new { tag = ptg.ToJSON() }, JsonRequestBehavior.AllowGet);
        }
    }
}
