using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using System.Web.Mvc.Async;

namespace IVO.CMS.Web.Internal.Mvc
{
    public class ReflectedTaskAsyncControllerDescriptor : ReflectedAsyncControllerDescriptor
    {
        public ReflectedTaskAsyncControllerDescriptor(Type type)
            : base(type)
        {
        }

        public override ActionDescriptor FindAction(ControllerContext controllerContext, string actionName)
        {
            var actionDescriptor = base.FindAction(controllerContext, actionName);
            return ReflectedTaskAsyncActionDescriptor.CreateIfTaskAsync(actionDescriptor);
        }
    }
}
