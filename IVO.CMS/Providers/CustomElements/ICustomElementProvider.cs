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
        /// Gets the next provider in the chain or null to signal the end of the chain.
        /// </summary>
        ICustomElementProvider Next { get; }

        /// <summary>
        /// Handles a custom "cms-" element in the context of the <paramref name="state"/>.
        /// </summary>
        /// <param name="elementName">The name of the element, beginning with "cms-".</param>
        /// <param name="state">The current rendering state.</param>
        /// <returns>true to mark element as processed; false to continue to next provider in the chain.
        /// If the last provider in the chain returns false, the custom element is skipped entirely and
        /// ignored.</returns>
        bool ProcessCustomElement(string elementName, RenderState state);
    }
}
