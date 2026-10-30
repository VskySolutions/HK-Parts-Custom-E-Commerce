namespace HKPartsEcommerce.Domain.Enums;

/// <summary>
/// How a product attribute is presented on the storefront and in the admin attribute library
/// (WO-15, REQ-CAT-003). Swatch values additionally carry a colour (ColorHex). CustomInput has no
/// predefined values at all — the buyer types their own, constrained by InputType/MaxLength/IsRequired.
/// </summary>
public enum ProductAttributeDisplayType
{
    Dropdown = 0,
    Button = 1,
    Swatch = 2,
    CustomInput = 3,
}

/// <summary>The kind of value a <see cref="ProductAttributeDisplayType.CustomInput"/> attribute accepts.</summary>
public enum ProductAttributeInputType
{
    Text = 0,
    Number = 1,
}
