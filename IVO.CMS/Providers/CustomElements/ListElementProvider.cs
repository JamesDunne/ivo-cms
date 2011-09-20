using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IVO.CMS.Providers.CustomElements
{
    public sealed class ListElementProvider : ICustomElementProvider
    {
        public ListElementProvider(ICustomElementProvider next = null)
        {
            this.Next = next;
        }

        #region ICustomElementProvider Members

        public ICustomElementProvider Next { get; private set; }

        public Task<bool> ProcessCustomElement(string elementName, RenderState state)
        {
            // TODO: design and implement a 'cms-list' element handler.
            return TaskEx.FromResult(false);
        }

        #endregion
    }
}
