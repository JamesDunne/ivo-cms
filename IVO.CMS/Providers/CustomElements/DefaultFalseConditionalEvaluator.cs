using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IVO.CMS.Providers.CustomElements
{
    public sealed class DefaultFalseConditionalEvaluator : IConditionalEvaluator
    {
        public DefaultFalseConditionalEvaluator(EitherAndOr andOr, IConditionalEvaluator next = null)
        {
            this.Next = next;
            this.AndOr = andOr;
        }

        #region IConditionalEvaluator Members

        public IConditionalEvaluator Next { get; private set; }
        public EitherAndOr AndOr { get; private set; }

        public bool EvaluateConditional(Dictionary<string, string> attributes)
        {
            return false;
        }

        #endregion
    }
}
