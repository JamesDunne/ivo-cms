using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace IVO.CMS.API.Models
{
    [Serializable]
    public sealed class OrderByModel<TorderBy>
        where TorderBy : struct
    {
        public TorderBy by { get; set; }
        public OrderByDirModel dir { get; set; }
    }
}