using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IVO.CMS.API.Models
{
    [Serializable]
    public sealed class CommitTreeResponse
    {
        public string id { get; set; }
        public string treeid { get; set; }
        public string committer { get; set; }
        public string date_committed { get; set; }
        public string message { get; set; }
        public bool parents_retrieved { get; set; }
        public CommitTreeResponse[] parents { get; set; }
    }
}
