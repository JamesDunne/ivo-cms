using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IVO.CMS.Providers.CustomElements
{
    public sealed class ConditionalElementProvider : ICustomElementProvider
    {
        private IConditionalEvaluator evaluator;

        public ConditionalElementProvider(IConditionalEvaluator evaluator, ICustomElementProvider next = null)
        {
            this.Next = next;
            this.evaluator = evaluator;
        }

        #region ICustomElementProvider Members

        public ICustomElementProvider Next { get; private set; }

        public bool ProcessCustomElement(string elementName, RenderState state)
        {
            if (elementName == "cms-conditional") return processConditionalElement(state);
            else if (elementName == "cms-if") return processIfElement(state);

            return false;
        }

        #endregion

        private bool processConditionalElement(RenderState st)
        {
            // <cms-conditional>
            //     <if department="Sales">Hello, Sales dept!</if>
            //     <elif department="Accounting">Hello, Accounting dept!</elif>
            //     <elif department="Management">Hello, Management dept!</elif>
            //     <else>Hello, unknown dept!</else>
            // </cms-conditional>

            st.SkipElementAndChildren("cms-conditional");

            return true;
        }

        private bool processIfElement(RenderState st)
        {
            // <cms-if department="Sales">
            //     <then>Hello, Sales dept!</then>
            //     <else>Whatever.</else>
            // </cms-if>

            st.SkipElementAndChildren("cms-if");

            return true;
        }
    }
}
