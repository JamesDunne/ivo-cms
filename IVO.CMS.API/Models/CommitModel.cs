﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IVO.CMS.API.Models
{
    [Serializable]
    public sealed class CommitModel
    {
        public string id { get; set; }
        public string treeid { get; set; }
        public string[] parents { get; set; }
        public string committer { get; set; }
        public DateTimeOffset date_committed { get; set; }
        public string message { get; set; }
    }
}
