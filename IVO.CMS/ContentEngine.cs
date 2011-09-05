using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using IVO.CMS.Providers;
using IVO.CMS.Providers.CustomElements;
using IVO.Definition.Models;
using IVO.Definition.Repositories;

namespace IVO.CMS
{
    public sealed class ContentEngine
    {
        private ITreeRepository trrepo;
        private IBlobRepository blrepo;
        private DateTimeOffset viewDate;
        private bool throwOnError;
        private bool injectErrorComments;
        private ICustomElementProvider providerRoot;

        private List<SemanticError> errors;

        public ContentEngine(ITreeRepository trrepo, IBlobRepository blrepo, DateTimeOffset viewDate, IConditionalEvaluator evaluator = null, ICustomElementProvider provider = null, bool throwOnError = false, bool injectErrorComments = true)
        {
            this.trrepo = trrepo;
            this.blrepo = blrepo;
            this.viewDate = viewDate;
            this.throwOnError = throwOnError;
            this.injectErrorComments = injectErrorComments;
            this.errors = new List<SemanticError>();

            // If no evaluator given, use the default false-returning evaluator:
            if (evaluator == null) evaluator = new DefaultFalseConditionalEvaluator(EitherAndOr.Or);
            // Wrap the given provider in the default chain:
            this.providerRoot =
                new ImportElementProvider(
                    new ScheduledElementProvider(
                        new ListElementProvider(
                            new LinkElementProvider(
                                new ConditionalElementProvider(evaluator, provider)
                            )
                        )
                    )
                );
        }

        public ITreeRepository Trees { get { return trrepo; } }
        public IBlobRepository Blobs { get { return blrepo; } }
        public DateTimeOffset ViewDate { get { return viewDate; } }
        public ICustomElementProvider CustomElementProviderRoot { get { return providerRoot; } }

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
