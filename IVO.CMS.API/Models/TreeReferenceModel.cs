﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IVO.CMS.API.Models
{
    [Serializable]
    public sealed class TreeReferenceModel
    {
        public string name { get; set; }
        public TreeModel tree { get; set; }
    }
}
