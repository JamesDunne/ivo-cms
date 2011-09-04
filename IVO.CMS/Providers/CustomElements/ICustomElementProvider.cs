using System.Text;
using System.Xml;
using IVO.Definition.Models;

namespace IVO.CMS.Providers.CustomElements
{
    /// <summary>
    /// An interface for consumers of IVOCMS to implement in order to process custom cms-
    /// elements encountered while rendering content from blobs.
    /// </summary>
    public interface ICustomElementProvider
    {
        /// <summary>
        /// Gets the next provider in the chain or null.
        /// </summary>
        ICustomElementProvider Next { get; }

        bool ProcessCustomElement(string elementName, RenderState state);
    }
}
