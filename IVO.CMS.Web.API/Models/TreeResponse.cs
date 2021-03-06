﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IVO.CMS.API.Models
{
    [Serializable]
    public sealed class TreeResponse
    {
        public string id { get; set; }
        public TreeBlobRefResponse[] blobs { get; set; }
        public TreeTreeRefResponse[] trees { get; set; }
    }
}
