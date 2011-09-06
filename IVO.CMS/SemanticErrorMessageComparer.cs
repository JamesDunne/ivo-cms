using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace IVO.CMS
{
    /// <summary>
    /// An `IComparer` to compare `SemanticErrors` by `Message`.
    /// </summary>
    public sealed class SemanticErrorMessageComparer : IComparer<SemanticError>, IComparer
    {
        public int Compare(SemanticError x, SemanticError y)
        {
            return x.Message.CompareTo(y.Message);
        }

        public int Compare(object x, object y)
        {
            return Compare((SemanticError)x, (SemanticError)y);
        }
    }
}
