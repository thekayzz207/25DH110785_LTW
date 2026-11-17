using System;
using System.Globalization;
using System.Web.Mvc;
namespace LTW_25DH110785
{
    // HTML number fields and query strings use a dot, regardless of the display culture.
    public class DecimalModelBinder : IModelBinder
    {
        public object BindModel(ControllerContext controllerContext, ModelBindingContext bindingContext)
        {
            var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            bool nullable = Nullable.GetUnderlyingType(bindingContext.ModelType) != null;
            if (value == null || string.IsNullOrWhiteSpace(value.AttemptedValue)) return nullable ? null : (object)0m;
            bindingContext.ModelState.SetModelValue(bindingContext.ModelName,value);
            decimal parsed;
            if (decimal.TryParse(value.AttemptedValue, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out parsed)) return parsed;
            bindingContext.ModelState.AddModelError(bindingContext.ModelName,"Nhập số hợp lệ, dùng dấu chấm cho phần thập phân.");
            return nullable ? null : (object)0m;
        }
    }
}
