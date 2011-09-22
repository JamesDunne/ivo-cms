using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IVO.CMS.API.Models
{
    [Serializable]
    public sealed class TagModel
    {
        public string id { get; set; }

        public string name { get; set; }
        public string commitid { get; set; }
        public string tagger { get; set; }
        public string date_tagged { get; set; }
        public string message { get; set; }
    }
}
