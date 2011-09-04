using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IVO.CMS.Providers.CustomElements
{
    public sealed class TargetedElementProvider : ICustomElementProvider
    {
        public TargetedElementProvider(ICustomElementProvider next)
        {
            this.Next = next;
        }

        #region ICustomElementProvider Members

        public ICustomElementProvider Next { get; private set; }

        public bool ProcessCustomElement(string elementName, RenderState state)
        {
            if (elementName != "cms-targeted") return false;

            processTargetedElement(state);

            return true;
        }

        #endregion

        private void processTargetedElement(RenderState st)
        {
            // Order matters. Most specific targets come first; least specific targets go last.
            // Target attributes are user-defined. They must be valid XML attributes.
            // The custom attributes are collected into a Dictionary<string, string> and passed to
            // the "target evaluation provider" to evaluate if the target attributes indicate that
            // the content applies to the current user viewing the content.
            // <cms-targeted>
            //   <if userType="Employee" department="Sales">
            //     ... employee-targeted content here, specifically for Sales department ...
            //   </if>
            //   <if userType="Manager">
            //     ... manager-targeted content here, not specific to a department ...
            //   </if>
            //   <if userType="Employee">
            //     ... employee-targeted content here, not specific to a department ...
            //   </if>
            //   <else>
            //     ... default content displayed if the above targets do not match ...
            //   </else>
            // </cms-targeted>

            st.SkipElementAndChildren("cms-targeted");
        }
    }
}
