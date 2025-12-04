using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Utils
{
    public static class HtmlExtension
    {
        public static String FormatCurrency(this HtmlHelper helper, long? price)
        {
            return price.GetValueOrDefault(0).ToString("#,##0");
        }

        public static String FormatCurrency(this HtmlHelper helper, long price)
        {
            return price.ToString("#,##0");
        }

        public static String FormatCurrency(this HtmlHelper helper, int price)
        {
            return price.ToString("#,##0");
        }

        public static String FormatCurrency(int? price)
        {
            return price.GetValueOrDefault(0).ToString("#,##0");
        }

        public static String FormatCurrency(long? price)
        {
            return price.GetValueOrDefault(0).ToString("#,##0");
        }

        public static String FormatCurrency(long price)
        {
            return price.ToString("#,##0");
        }

        public static String FormatCurrency(int price)
        {
            return price.ToString("#,##0");
        }

        // ValidationLabel
        public static MvcHtmlString ValidationLabel(this HtmlHelper htmlHelper,
                                                    string modelName,
                                                    string labelText,
                                                    IDictionary<string, object> htmlAttributes)
        {
            if (modelName == null) { throw new ArgumentNullException("modelName"); }
            ModelState modelState = htmlHelper.ViewData.ModelState[modelName];
            ModelErrorCollection modelErrors = (modelState == null) ? null : modelState.Errors;
            ModelError modelError = ((modelErrors == null) || (modelErrors.Count == 0)) ? null : modelErrors[0];
            // If there is no error, we want to show a label.  If there is an error,
            // we want to show the error message.
            string tagText = labelText;
            string tagClass = "form_field_label_normal";
            if ((modelState != null) && (modelError != null))
            {
                tagText = modelError.ErrorMessage;
                tagClass = "form_field_label_error";
            }
            // Build out the tag
            TagBuilder builder = new TagBuilder("label");
            builder.MergeAttributes(htmlAttributes);
            builder.MergeAttribute("class", tagClass);
            builder.MergeAttribute("validationlabelfor", modelName);
            builder.SetInnerText(tagText);
            return MvcHtmlString.Create(builder.ToString(TagRenderMode.Normal));
        }

        public static MvcHtmlString ValidationLabel(this HtmlHelper htmlHelper, string modelName, string labelClass = null)
        {
            Dictionary<string, object> attr = null;
            if (labelClass != null)
                attr = new Dictionary<String, object> { { "class", labelClass } };
            return HtmlExtension.ValidationLabel(htmlHelper, modelName, null, attr);
        }

        //
        // Checkboxes
        //

        public static MvcHtmlString CheckBoxFor<TModel>(this HtmlHelper<TModel> htmlHelper,
            Expression<Func<TModel, bool>> expression, bool renderHiddenInput)
        {
            if (renderHiddenInput)
            {
                return System.Web.Mvc.Html.InputExtensions.CheckBoxFor(htmlHelper, expression);
            }
            return CheckBoxFor(htmlHelper, expression, null, false);
        }

        public static MvcHtmlString CheckBoxFor<TModel>(this HtmlHelper<TModel> htmlHelper,
            Expression<Func<TModel, bool>> expression, object htmlAttributes, bool renderHiddenInput)
        {
            if (renderHiddenInput)
            {
                return System.Web.Mvc.Html.InputExtensions.CheckBoxFor(htmlHelper, expression, htmlAttributes);
            }
            return CheckBoxFor(htmlHelper, expression, HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes), false);
        }

        public static MvcHtmlString CheckBoxFor<TModel>(this HtmlHelper<TModel> htmlHelper,
            Expression<Func<TModel, bool>> expression, IDictionary<string, object> htmlAttributes,
            bool renderHiddenInput)
        {
            if (renderHiddenInput)
            {
                return System.Web.Mvc.Html.InputExtensions.CheckBoxFor(htmlHelper, expression, htmlAttributes);
            }

            if (expression == null)
            {
                throw new ArgumentNullException("expression");
            }

            ModelMetadata metadata = ModelMetadata.FromLambdaExpression(expression, htmlHelper.ViewData);
            bool? isChecked = null;
            if (metadata.Model != null)
            {
                bool modelChecked;
                if (Boolean.TryParse(metadata.Model.ToString(), out modelChecked))
                {
                    isChecked = modelChecked;
                }
            }

            return CheckBoxHelper(htmlHelper, metadata, ExpressionHelper.GetExpressionText(expression), isChecked, htmlAttributes);
        }

        private static MvcHtmlString CheckBoxHelper(HtmlHelper htmlHelper, ModelMetadata metadata, string name, bool? isChecked, IDictionary<string, object> htmlAttributes)
        {
            RouteValueDictionary attributes = ToRouteValueDictionary(htmlAttributes);

            bool explicitValue = isChecked.HasValue;
            if (explicitValue)
            {
                attributes.Remove("checked"); // Explicit value must override dictionary
            }

            return InputHelper(htmlHelper,
                               InputType.CheckBox,
                               metadata,
                               name,
                               value: "true",
                               useViewData: !explicitValue,
                               isChecked: isChecked ?? false,
                               setId: true,
                               isExplicitValue: false,
                               format: null,
                               htmlAttributes: attributes);
        }

        //
        // Helper methods
        //

        private static MvcHtmlString InputHelper(HtmlHelper htmlHelper, InputType inputType, ModelMetadata metadata, string name, object value, bool useViewData, bool isChecked, bool setId, bool isExplicitValue, string format, IDictionary<string, object> htmlAttributes)
        {
            string fullName = htmlHelper.ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(name);
            if (string.IsNullOrEmpty(fullName))
            {
                throw new ArgumentException("Value cannot be null or empty.", "name");
            }

            var tagBuilder = new TagBuilder("input");
            tagBuilder.MergeAttributes(htmlAttributes);
            tagBuilder.MergeAttribute("type", HtmlHelper.GetInputTypeString(inputType));
            tagBuilder.MergeAttribute("name", fullName, true);

            string valueParameter = htmlHelper.FormatValue(value, format);
            var usedModelState = false;

            bool? modelStateWasChecked = GetModelStateValue(htmlHelper.ViewData, fullName, typeof(bool)) as bool?;
            if (modelStateWasChecked.HasValue)
            {
                isChecked = modelStateWasChecked.Value;
                usedModelState = true;
            }
            if (!usedModelState)
            {
                string modelStateValue = GetModelStateValue(htmlHelper.ViewData, fullName, typeof(string)) as string;
                if (modelStateValue != null)
                {
                    isChecked = string.Equals(modelStateValue, valueParameter, StringComparison.Ordinal);
                    usedModelState = true;
                }
            }
            if (!usedModelState && useViewData)
            {
                isChecked = EvalBoolean(htmlHelper.ViewData, fullName);
            }
            if (isChecked)
            {
                tagBuilder.MergeAttribute("checked", "checked");
            }
            tagBuilder.MergeAttribute("value", valueParameter, isExplicitValue);

            if (setId)
            {
                tagBuilder.GenerateId(fullName);
            }

            // If there are any errors for a named field, we add the css attribute.
            ModelState modelState;
            if (htmlHelper.ViewData.ModelState.TryGetValue(fullName, out modelState))
            {
                if (modelState.Errors.Count > 0)
                {
                    tagBuilder.AddCssClass(HtmlHelper.ValidationInputCssClassName);
                }
            }

            tagBuilder.MergeAttributes(htmlHelper.GetUnobtrusiveValidationAttributes(name, metadata));

            return MvcHtmlString.Create(tagBuilder.ToString(TagRenderMode.SelfClosing));
        }

        private static RouteValueDictionary ToRouteValueDictionary(IDictionary<string, object> dictionary)
        {
            return dictionary == null ? new RouteValueDictionary() : new RouteValueDictionary(dictionary);
        }

        private static object GetModelStateValue(ViewDataDictionary viewData, string key, Type destinationType)
        {
            ModelState modelState;
            if (viewData.ModelState.TryGetValue(key, out modelState))
            {
                if (modelState.Value != null)
                {
                    return modelState.Value.ConvertTo(destinationType, culture: null);
                }
            }
            return null;
        }

        private static bool EvalBoolean(ViewDataDictionary viewData, string key)
        {
            return Convert.ToBoolean(viewData.Eval(key), CultureInfo.InvariantCulture);
        }

        // Breadcrumb helper
        public static MvcHtmlString ShowBreadcrumb(this HtmlHelper htmlHelper, GroupProduct group, int currGroupID = -1)
        {
            if (group == null)
                return MvcHtmlString.Empty;

            if (currGroupID == -1)
            {
                currGroupID = group.GroupID;
            }

            var sb = new StringBuilder();
            BuildBreadcrumb(sb, htmlHelper, group, currGroupID);
            return MvcHtmlString.Create(sb.ToString());
        }

        public static MvcHtmlString ShowBreadcrumb<TModel>(this HtmlHelper<TModel> htmlHelper, GroupProduct group, int currGroupID = -1)
        {
            return ShowBreadcrumb((HtmlHelper)htmlHelper, group, currGroupID);
        }

        private static void BuildBreadcrumb(StringBuilder sb, HtmlHelper htmlHelper, GroupProduct group, int currGroupID)
        {
            if (group.ParentGroupID != null && group.GroupProduct1 != null)
            {
                BuildBreadcrumb(sb, htmlHelper, group.GroupProduct1, currGroupID);
            }

            var url = new UrlHelper(htmlHelper.ViewContext.RequestContext);
            var categoryUrl = url.Action("Detail", "Category", new { id = group.GroupID });
            var encodedGroupName = HttpUtility.HtmlEncode(group.GroupName);

            if (group.GroupID == currGroupID)
            {
                sb.AppendFormat("<li class=\"active\"><a href=\"{0}\">{1}</a></li>", categoryUrl, encodedGroupName);
            }
            else
            {
                sb.AppendFormat("<li><a href=\"{0}\">{1}</a></li>", categoryUrl, encodedGroupName);
            }
        }
    }
}