using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using System.Web.Mvc.Async;

namespace IVO.CMS.Web.Internal.Mvc
{
    public class TaskAsyncControllerActionInvoker : AsyncControllerActionInvoker
    {
        static readonly ConcurrentDictionary<Type, ReflectedTaskAsyncControllerDescriptor> _cache = new ConcurrentDictionary<Type, ReflectedTaskAsyncControllerDescriptor>();

        protected override ControllerDescriptor GetControllerDescriptor(ControllerContext controllerContext)
        {
            Type controllerType = controllerContext.Controller.GetType();
            return _cache.GetOrAdd(controllerType, type => new ReflectedTaskAsyncControllerDescriptor(type));
        }
    }
}
