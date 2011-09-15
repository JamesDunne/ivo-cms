using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.Web.Mvc.Async;
using System.Web.Mvc;

namespace IVO.CMS.Web.Mvc
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
