using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GrantTracker.Api.Binding;

/// <summary>
/// Binds an enum in a query string or route by its name only, ignoring case (DESIGN.md §5). It replaces the
/// framework's enum binder, which also accepts a number (<c>?status=1</c>) or a comma list (<c>Open,Closed</c>). A number
/// depends on the enum's order, which clients can't see and which may change. Enums in a JSON body are read by the JSON
/// settings instead, which already require the name.
/// </summary>
public sealed class EnumNameModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var enumType = context.Metadata.UnderlyingOrModelType;
        return enumType.IsEnum ? new EnumNameModelBinder(enumType) : null;
    }
}

public sealed class EnumNameModelBinder(Type enumType) : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (value == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, value);

        var text = value.FirstValue?.Trim();
        if (string.IsNullOrEmpty(text) && bindingContext.ModelMetadata.IsReferenceOrNullableType)
        {
            // An empty value is the same as no value, as with the framework's binders.
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        var names = Enum.GetNames(enumType);
        var name = names.FirstOrDefault(candidate => string.Equals(candidate, text, StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"Use one of: {string.Join(", ", names)}.");
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success(Enum.Parse(enumType, name));
        return Task.CompletedTask;
    }
}
