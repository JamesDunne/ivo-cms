﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using System.Diagnostics;
using System.ComponentModel;
using IVO.Definition.Errors;

namespace IVO.CMS.API.Code
{
    public sealed class ErrorableModelBinder : IModelBinder
    {
        public object BindModel(ControllerContext controllerContext, ModelBindingContext bindingContext)
        {
            // Assert that we're binding an Errorable<T> model:
            Debug.Assert(bindingContext.ModelType.IsGenericType && bindingContext.ModelType.Name == "Errorable`1" && bindingContext.ModelType.Namespace == "IVO.Definition.Errors");

            // Get the (string) value for this model:
            ValueProviderResult modelValue = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            // No ValueProviderResult means no value set at all:
            if (modelValue == null)
                return null;

            // Pick out the T so we know what to convert:
            Type[] args = bindingContext.ModelType.GetGenericArguments();
            Debug.Assert(args.Length == 1);

            Type errorableContainedType = args[0];
            Debug.Assert(errorableContainedType != null);

            // Check if the T type can be converted from `string` to `Errorable<T>`:
            TypeConverter cvt = TypeDescriptor.GetConverter(errorableContainedType);
            if (!cvt.CanConvertTo(bindingContext.ModelType))
                return Activator.CreateInstance(bindingContext.ModelType, (object)(ErrorBase)new InputError("TypeConverter '{0}' cannot convert to '{1}' for model name '{2}'", cvt.GetType().FullName, bindingContext.ModelType.FullName, bindingContext.ModelName));

            // Null RawValue means ... what?
            string value;
            object rawValue = modelValue.RawValue;
            if (rawValue == null)
                return Activator.CreateInstance(bindingContext.ModelType, (object)(ErrorBase)new InputError("Value for '{0}' not provided", bindingContext.ModelName));

            // Get the `string` or `string[]` and render a final string value:
            Type rawValueType = rawValue.GetType();
            if (rawValueType == typeof(string)) value = (string)modelValue.RawValue;
            else if (rawValueType == typeof(string[]))
            {
                string[] rawValues = (string[])modelValue.RawValue;

                Debug.Assert(rawValues.Length > 0);

                if (rawValues.Length == 1) value = rawValues[0];
                else value = String.Join(",", rawValues);
            }
            else value = modelValue.RawValue.ToString();

            // Run the converter from `string` to `Errorable<T>`:
            object result = cvt.ConvertTo(
                (ITypeDescriptorContext)null,
                System.Globalization.CultureInfo.InvariantCulture,
                value,
                bindingContext.ModelType
            );

            return result;
        }
    }
}
