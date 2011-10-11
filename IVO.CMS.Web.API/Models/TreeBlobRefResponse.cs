using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IVO.Definition.Models;

namespace IVO.CMS.API.Models
{
    [Serializable]
    public sealed class TreeBlobRefResponse
    {
        public string name { get; set; }
        public string blobid { get; set; }
    }
}
