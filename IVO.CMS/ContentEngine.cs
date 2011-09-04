using System;
using System.IO;
using System.Text;
using System.Web;
using System.Xml;
using IVO.Definition.Models;
using IVO.Definition.Repositories;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using IVO.CMS.Providers;

namespace IVO.CMS
{
    public sealed class ContentEngine
    {
        private const string rootElementName = "_root_";

        private static readonly byte[] rootOpen = Encoding.UTF8.GetBytes("<" + rootElementName + ">");
        private static readonly byte[] rootClose = Encoding.UTF8.GetBytes("</" + rootElementName +">");

        private ITreeRepository trrepo;
        private IBlobRepository blrepo;
        private DateTimeOffset viewDate;
        private bool throwOnError;
        private bool injectErrorComments;

        private List<SemanticError> errors;

        public ContentEngine(ITreeRepository trrepo, IBlobRepository blrepo, DateTimeOffset viewDate, bool throwOnError = false, bool injectErrorComments = true)
        {
            this.trrepo = trrepo;
            this.blrepo = blrepo;
            this.viewDate = viewDate;
            this.throwOnError = throwOnError;
            this.injectErrorComments = injectErrorComments;
            this.errors = new List<SemanticError>();
        }

        public ITreeRepository Trees { get { return trrepo; } }
        public IBlobRepository Blobs { get { return blrepo; } }
        public DateTimeOffset ViewDate { get { return viewDate; } }

        public bool ThrowOnError { get { return throwOnError; } }
        public bool InjectErrorComments { get { return injectErrorComments; } }

        public ReadOnlyCollection<SemanticError> GetErrors()
        {
            return new ReadOnlyCollection<SemanticError>(errors);
        }

        public void ReportError(SemanticError err)
        {
            if (throwOnError) throw err;

            // Track the error:
            errors.Add(err);
        }

        public HTMLFragment RenderContentItem(BlobTreePath item)
        {
            RenderState rs = new RenderState(this);
            rs.Render(item);

            string result = rs.Writer.ToString();
            return new HTMLFragment(result);
        }
    }
}
