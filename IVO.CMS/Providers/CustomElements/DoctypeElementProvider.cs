using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IVO.Definition.Models;
using System.Threading.Tasks;
using IVO.Definition.Errors;

namespace IVO.CMS.Providers.CustomElements
{
    public sealed class DoctypeElementProvider : ICustomElementProvider
    {
        public DoctypeElementProvider(ICustomElementProvider next = null)
        {
            this.Next = next;
        }

        #region ICustomElementProvider Members

        public ICustomElementProvider Next { get; private set; }

        public async Task<Errorable<bool>> ProcessCustomElement(string elementName, RenderState state)
        {
            if (elementName != "cms-doctype") return false;

            var err = await processDoctypeElement(state).ConfigureAwait(continueOnCapturedContext: false);
            if (err.HasErrors) return err.Errors;

            return true;
        }

        #endregion

        private Task<Errorable> processDoctypeElement(RenderState st)
        {
            // <cms-doctype type="html" />
            //   generates
            // <!DOCTYPE html>
            
            var xr = st.Reader;

            if (!xr.IsEmptyElement)
            {
                st.Error("cms-doctype must be an empty element");
                st.SkipElementAndChildren("cms-doctype");
                return Task.FromResult(Errorable.NoErrors);
            }

            if (!xr.MoveToAttribute("type"))
            {
                st.Error("cms-doctype must have a type attribute");
                xr.MoveToElement();
                return Task.FromResult(Errorable.NoErrors);
            }

            string type = xr.Value;
            xr.MoveToElement();

            if (type == "html")
            {
                // HTML5 doctype:
                st.Writer.Append("<!DOCTYPE html>\r\n\r\n");
                return Task.FromResult(Errorable.NoErrors);
            }
            else
            {
                st.Error("cms-doctype has unknown type value '{0}'", type);
                return Task.FromResult(Errorable.NoErrors);
            }
        }
    }
}
