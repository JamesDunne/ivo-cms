using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace IVO.CMS
{
    /// <summary>
    /// An `IComparer` to compare `SemanticWarnings` by `Message`.
    /// </summary>
    public sealed class SemanticWarningMessageComparer : IComparer<SemanticWarning>, IComparer
    {
        public int Compare(SemanticWarning x, SemanticWarning y)
        {
            return x.Message.CompareTo(y.Message);
        }

        public int Compare(object x, object y)
        {
            return Compare((SemanticWarning)x, (SemanticWarning)y);
        }
    }
}
