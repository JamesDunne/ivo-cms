using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IVO.Definition.Errors;
using System.Threading.Tasks;

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

        public Task<Errorable<bool>> EvaluateConditional(Dictionary<string, string> attributes)
        {
            return TaskEx.FromResult((Errorable<bool>) false);
        }

        #endregion
    }
}
