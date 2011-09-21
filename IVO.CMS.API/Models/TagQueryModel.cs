using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace IVO.CMS.API.Models
{
    [Serializable]
    public sealed class TagQueryModel
    {
        // Date range searching:
        public DateTimeOffset? dateFrom { get; set; }
        public DateTimeOffset? dateTo { get; set; }

        // Filter by name
        public string name { get; set; }

        // Filter by tagger
        public string tagger { get; set; }

        // Ordering of results
        public OrderByModel<OrderBy>[] ordering { get; set; }

        public enum OrderBy
        {
            date_tagged,
            name,
            tagger
        }

        public int? pageNumber { get; set; }
        public int? pageSize { get; set; }
    }
}