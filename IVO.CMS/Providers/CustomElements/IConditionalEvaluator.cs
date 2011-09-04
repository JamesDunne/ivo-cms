using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IVO.CMS.Providers.CustomElements
{
    public interface IConditionalEvaluator
    {
        /// <summary>
        /// Gets the next conditional evaluator in the provider chain.
        /// </summary>
        IConditionalEvaluator Next { get; }

        /// <summary>
        /// Determines how to combine this provider's result with the next provider's result with either boolean AND or boolean OR.
        /// </summary>
        EitherAndOr AndOr { get; }

        /// <summary>
        /// Evaluates a set of XML attributes to determine a final true/false value.
        /// </summary>
        /// <param name="attributes">The set of XML attributes pulled from a conditional element; dictionary keys are case-sensitive.</param>
        /// <returns></returns>
        bool EvaluateConditional(Dictionary<string, string> attributes);
    }

    public enum EitherAndOr
    {
        And,
        Or
    }
}
