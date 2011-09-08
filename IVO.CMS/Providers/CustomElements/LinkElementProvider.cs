using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IVO.Definition.Models;

namespace IVO.CMS.Providers.CustomElements
{
    public sealed class LinkElementProvider : ICustomElementProvider
    {
        public LinkElementProvider(ICustomElementProvider next = null)
        {
            this.Next = next;
        }

        #region ICustomElementProvider Members

        public ICustomElementProvider Next { get; private set; }

        public bool ProcessCustomElement(string elementName, RenderState state)
        {
            if (elementName != "cms-link") return false;

            processLinkElement(state);

            return true;
        }

        #endregion

        private void processLinkElement(RenderState st)
        {
            // A 'cms-link' is translated directly into an anchor tag with the 'path' attribute
            // translated and canonicalized into an absolute 'href' attribute, per system
            // configuration. All other attributes are copied to the anchor tag as-is.

            // e.g.:
            //   <cms-link path="/absolute/path" ...>contents</cms-link>
            // becomes:
            //   <a href="/content/absolute/path" ...>contents</a>
            // if the CMS requests are "mounted" to the /content/ root URL path.

            // Either relative or absolute paths are allowed for 'path':
            //   <cms-link path="../../hello/world" target="_blank">Link text.</cms-link>
            //   <cms-link path="/hello/world" target="_blank">Link text.</cms-link>

            // Set the current element depth so we know where to read up to on error:
            int knownDepth = st.Reader.Depth;
            bool isEmpty = st.Reader.IsEmptyElement;

            if (!st.Reader.HasAttributes)
            {
                st.Error("cms-link has no attributes");
                goto errored;
            }

            bool foundPath = false;

            st.Writer.Append("<a");

            st.Reader.MoveToFirstAttribute();
            do
            {
                string value = st.Reader.Value;

                if (st.Reader.LocalName == "path")
                {
                    foundPath = true;

                    // Get the canonicalized blob path (from either absolute or relative):
                    var abspath = Path.ParseBlobPath(value);
                    CanonicalBlobPath path = abspath.Collapse(abs => abs.Canonicalize(), rel => (st.Item.TreePath.Path.Tree + rel).Canonicalize());
                    
                    // TODO: apply the reverse-mount prefix path from the system configuration,
                    // or just toss the CanonicalBlobPath over to a provider implementation and
                    // it can give us the final absolute URL path.
                    st.Writer.AppendFormat(" href=\"{0}\"", path);
                    continue;
                }

                // Append the normal attribute:
                st.Writer.AppendFormat(" {0}={2}{1}{2}", st.Reader.LocalName, value, st.Reader.QuoteChar);
            } while (st.Reader.MoveToNextAttribute());

            // Jump back to the element node from the attributes:
            st.Reader.MoveToElement();

            if (!foundPath)
            {
                // Issue a warning and append an "href='#'" attribute:
                st.WarningSuppressComment("expected 'path' attribute on 'cms-link' element was not found");
                st.Writer.Append(" href=\"#\"");
            }

            // Self-close the <a /> if the <cms-link /> is empty:
            if (isEmpty)
            {
                st.Writer.Append(" />");
                return;
            }

            // Copy the inner contents and close out the </a>.
            st.Writer.Append(">");
            st.CopyElementChildren("cms-link");
            st.Writer.Append("</a>");
            return;

        errored:
            // Skip to the end of the cms-link element:
            if (!isEmpty)
                while (st.Reader.Read() && st.Reader.Depth > knownDepth) { }
            return;
        }
    }
}
