﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IVO.CMS.API.Models
{
    [Serializable]
    public sealed class TreeTreeRefRequest
    {
        public string name { get; set; }
        public string treeid { get; set; }
        public TreeRequest tree { get; set; }
    }
}
