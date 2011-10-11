using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;

namespace IVO.CMS.API.Code
{
    public sealed class ErrorableModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder GetBinder(Type modelType)
        {
            // Only bind models of type `Errorable<T>`:
            if (modelType.IsGenericType && modelType.Name == "Errorable`1" && modelType.Namespace == "IVO.Definition.Errors")
                return new ErrorableModelBinder();

            // Null is good for unknown?
            return null;
        }
    }
}
