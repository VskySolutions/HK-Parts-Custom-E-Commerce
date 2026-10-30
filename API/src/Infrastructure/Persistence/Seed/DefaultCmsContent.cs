using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Persistence.Seed;

/// <summary>
/// The canonical set of pre-seeded CMS page groups and informational pages (WO-54). Groups carry fixed
/// Guids so pages can reference them deterministically across reseeds; every seeded page is Published and
/// flagged <see cref="CMSPage.IsSystemPage"/> so the admin UI protects it from deletion.
/// </summary>
public static class DefaultCmsContent
{
    // Fixed group ids — stable across reseeds so BuildPages can wire PageGroupId without a DB round-trip.
    private static readonly Guid InformationGroupId = new("a1b2c3d4-0000-0000-0000-000000000001");
    private static readonly Guid AboutGroupId = new("a1b2c3d4-0000-0000-0000-000000000002");
    private static readonly Guid CustomerServiceGroupId = new("a1b2c3d4-0000-0000-0000-000000000003");

    private sealed record GroupSeed(Guid Id, string Name, string Slug, int DisplayOrder);

    private sealed record PageSeed(
        Guid Id,
        string Title,
        string Slug,
        string GroupSlug,
        int DisplayOrder,
        string MetaDescription,
        string Body);

    private static readonly GroupSeed[] GroupSeeds =
    {
        new(InformationGroupId, "Information", "information", 0),
        new(AboutGroupId, "About", "about", 1),
        new(CustomerServiceGroupId, "Customer Service", "customer-service", 2),
    };

    private static readonly PageSeed[] PageSeeds =
    {
        new(new("b2c3d4e5-0000-0000-0000-000000000001"),
            "Shipping & Returns", "shipping-returns", "information", 0,
            "Learn about our shipping options, delivery times, and hassle-free returns.",
            "<h1>Shipping &amp; Returns</h1>"
            + "<h2>Shipping</h2><p>We aim to dispatch all orders within 1&ndash;2 business days. Delivery times "
            + "vary by destination and the shipping method selected at checkout. Tracking details are emailed to "
            + "you as soon as your order ships.</p>"
            + "<h2>Returns</h2><p>Not completely satisfied? You may return most items within 30 days of delivery "
            + "for a refund or exchange, provided they are unused and in their original packaging. Please contact "
            + "our support team to start a return.</p>"),

        new(new("b2c3d4e5-0000-0000-0000-000000000002"),
            "Privacy Policy", "privacy-policy", "information", 1,
            "How we collect, use, and protect your personal information.",
            "<h1>Privacy Policy</h1>"
            + "<p>Your privacy matters to us. This policy explains what information we collect, how we use it, and "
            + "the choices you have.</p>"
            + "<h2>Information We Collect</h2><p>We collect the details you provide when you create an account, place "
            + "an order, or contact us, along with limited technical data needed to operate our store.</p>"
            + "<h2>How We Use Your Information</h2><p>We use your information to process orders, provide support, and "
            + "improve your shopping experience. We never sell your personal data.</p>"),

        new(new("b2c3d4e5-0000-0000-0000-000000000003"),
            "Conditions of Use", "conditions-of-use", "information", 2,
            "The terms and conditions that govern your use of our store.",
            "<h1>Conditions of Use</h1>"
            + "<p>By accessing and using this website you accept the following terms and conditions in full.</p>"
            + "<h2>Use of the Site</h2><p>You agree to use the site only for lawful purposes and in a way that does "
            + "not infringe the rights of, or restrict the use of, this site by any third party.</p>"
            + "<h2>Pricing &amp; Availability</h2><p>All products are subject to availability. We reserve the right to "
            + "correct pricing errors and to update these terms from time to time.</p>"),

        new(new("b2c3d4e5-0000-0000-0000-000000000004"),
            "About Us", "about-us", "about", 0,
            "Who we are and what we stand for.",
            "<h1>About Us</h1>"
            + "<p>We are passionate about bringing you great products and an effortless shopping experience. Since "
            + "day one our mission has been simple: quality you can trust and service you can rely on.</p>"
            + "<p>Thank you for being part of our story. We are constantly growing our catalogue and improving our "
            + "service based on your feedback.</p>"),

        new(new("b2c3d4e5-0000-0000-0000-000000000005"),
            "Contact Us", "contact-us", "about", 1,
            "Get in touch with our team — we are here to help.",
            "<h1>Contact Us</h1>"
            + "<p>Have a question or need a hand? We would love to hear from you.</p>"
            + "<ul><li><strong>Email:</strong> support@example.com</li>"
            + "<li><strong>Phone:</strong> +1 (555) 000-0000</li>"
            + "<li><strong>Hours:</strong> Monday&ndash;Friday, 9am&ndash;5pm</li></ul>"
            + "<p>We aim to respond to all enquiries within one business day.</p>"),

        new(new("b2c3d4e5-0000-0000-0000-000000000006"),
            "Customer Service", "customer-service", "customer-service", 0,
            "Support resources, order help, and how to reach us.",
            "<h1>Customer Service</h1>"
            + "<p>We are committed to making your experience a great one. Below are the quickest ways to get help.</p>"
            + "<h2>Order Help</h2><p>Track an order, update your details, or manage returns from your account "
            + "dashboard at any time.</p>"
            + "<h2>Need More Help?</h2><p>Our support team is available Monday&ndash;Friday and typically responds "
            + "within one business day.</p>"),

        new(new("b2c3d4e5-0000-0000-0000-000000000007"),
            "FAQ", "faq", "customer-service", 1,
            "Answers to the questions we are asked most often.",
            "<h1>Frequently Asked Questions</h1>"
            + "<h2>How long does delivery take?</h2><p>Most orders are delivered within 3&ndash;7 business days "
            + "depending on your location and chosen shipping method.</p>"
            + "<h2>Can I change or cancel my order?</h2><p>If your order has not yet shipped, contact us as soon as "
            + "possible and we will do our best to help.</p>"
            + "<h2>What payment methods do you accept?</h2><p>We accept all major credit and debit cards along with "
            + "the additional payment options shown at checkout.</p>"),
    };

    /// <summary>Builds fresh <see cref="CMSPageGroup"/> entities for seeding (footer/nav columns).</summary>
    public static IReadOnlyList<CMSPageGroup> BuildGroups() =>
        GroupSeeds.Select(g => new CMSPageGroup
        {
            Id = g.Id,
            Name = g.Name,
            Slug = g.Slug,
            DisplayOrder = g.DisplayOrder,
        }).ToList();

    /// <summary>
    /// Builds the pre-seeded informational <see cref="CMSPage"/> entities, wiring each to its group via the
    /// provided <paramref name="groups"/> (matched by slug). Pages are Published system pages.
    /// </summary>
    public static IReadOnlyList<CMSPage> BuildPages(IReadOnlyList<CMSPageGroup> groups)
    {
        var groupIdBySlug = groups
            .Where(g => g.Slug is not null)
            .ToDictionary(g => g.Slug!, g => g.Id, StringComparer.OrdinalIgnoreCase);

        return PageSeeds.Select(p => new CMSPage
        {
            Id = p.Id,
            Title = p.Title,
            Slug = p.Slug,
            Body = p.Body,
            MetaTitle = p.Title,
            MetaDescription = p.MetaDescription,
            Status = CmsContentStatus.Published,
            IsSystemPage = true,
            PageGroupId = groupIdBySlug.TryGetValue(p.GroupSlug, out var groupId) ? groupId : null,
            DisplayOrder = p.DisplayOrder,
        }).ToList();
    }
}
