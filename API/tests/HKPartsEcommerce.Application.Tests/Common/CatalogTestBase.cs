using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Persistence;

namespace HKPartsEcommerce.Application.Tests.Common;

/// <summary>
/// Base for catalog handler tests, backed by a real SQL Server database (the production provider).
///
/// A dedicated <c>VSkyECommerce_Test</c> database is dropped and re-created once per test run from
/// the live <see cref="AppDbContext"/> model. Every test then runs on its own connection inside a
/// transaction that is rolled back on dispose, so tests are isolated and leave no residue — without
/// paying to rebuild the schema each time. Test parallelism is disabled assembly-wide
/// (see <c>TestParallelization.cs</c>) so the shared database sees one test at a time.
/// </summary>
public abstract class CatalogTestBase : IDisposable
{
    private const string DatabaseName = "VSkyECommerce_Test";

    /// <summary>Base connection (no database) — override via env var for CI / other instances.</summary>
    private static readonly string ServerConnectionString =
        Environment.GetEnvironmentVariable("VSKY_TEST_SQLSERVER")
        ?? "Server=VSky-MT\\SQLEXPRESS;User Id=sa;Password=soft;TrustServerCertificate=True;MultipleActiveResultSets=True;";

    private static readonly string TestConnectionString =
        new SqlConnectionStringBuilder(ServerConnectionString) { InitialCatalog = DatabaseName }.ConnectionString;

    private static readonly object InitLock = new();
    private static bool _initialized;

    private readonly SqlConnection _connection;
    // Exposed to derived fixtures that need to build a context carrying a specific current user (e.g. to
    // exercise audit stamping) while staying inside the test's rolled-back transaction.
    protected readonly SqlTransaction _transaction;
    protected readonly DbContextOptions<AppDbContext> _options;

    protected CatalogTestBase()
    {
        EnsureDatabaseCreated();

        _connection = new SqlConnection(TestConnectionString);
        _connection.Open();
        _transaction = _connection.BeginTransaction();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_connection)
            .Options;
    }

    /// <summary>A new context over the shared per-test connection, enlisted in the test's transaction.</summary>
    protected AppDbContext NewContext()
    {
        var ctx = new AppDbContext(_options);
        ctx.Database.UseTransaction(_transaction);
        return ctx;
    }

    private static void EnsureDatabaseCreated()
    {
        if (_initialized) return;
        lock (InitLock)
        {
            if (_initialized) return;

            var master = new SqlConnectionStringBuilder(ServerConnectionString) { InitialCatalog = "master" }.ConnectionString;
            using (var conn = new SqlConnection(master))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $@"
IF DB_ID('{DatabaseName}') IS NOT NULL
BEGIN
    ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{DatabaseName}];
END";
                cmd.ExecuteNonQuery();
            }

            using (var ctx = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(TestConnectionString).Options))
            {
                ctx.Database.EnsureCreated();
            }

            _initialized = true;
        }
    }

    // ---- Seed helpers ---------------------------------------------------------------------------
    // Each writes inside the current test's transaction; ids are visible to later NewContext() reads.

    protected Guid SeedTaxCategory(string name = "Standard")
    {
        using var db = NewContext();
        var entity = new TaxCategory { Name = name };
        db.TaxCategories.Add(entity);
        db.SaveChanges();
        return entity.Id;
    }

    protected Guid SeedManufacturer(string name = "Acme")
    {
        using var db = NewContext();
        var entity = new Manufacturer { Name = name };
        db.Manufacturers.Add(entity);
        db.SaveChanges();
        return entity.Id;
    }

    protected Guid SeedCategory(string name = "Category")
    {
        using var db = NewContext();
        var entity = new Category { Name = name };
        db.Categories.Add(entity);
        db.SaveChanges();
        return entity.Id;
    }

    /// <summary>
    /// Seeds a product (creating a tax category when none is supplied). Use <paramref name="configure"/>
    /// to set type, publish state, display order, etc. Returns the product id.
    /// </summary>
    protected Guid SeedProduct(Action<Product>? configure = null, Guid? taxCategoryId = null)
    {
        var taxId = taxCategoryId ?? SeedTaxCategory();
        using var db = NewContext();
        var product = new Product
        {
            Name = "Product",
            ProductType = ProductType.Simple,
            TaxCategoryId = taxId,
        };
        configure?.Invoke(product);
        db.Products.Add(product);
        db.SaveChanges();
        return product.Id;
    }

    /// <summary>Seeds a product attribute plus ordered values; returns the attribute id and value ids.</summary>
    protected (Guid AttributeId, Guid[] ValueIds) SeedAttribute(string name, params string[] values)
    {
        using var db = NewContext();
        var attribute = new ProductAttribute { Name = name };
        var order = 0;
        foreach (var value in values)
            attribute.Values.Add(new ProductAttributeValue { Value = value, DisplayOrder = order++ });
        db.ProductAttributes.Add(attribute);
        db.SaveChanges();
        return (attribute.Id, attribute.Values.OrderBy(v => v.DisplayOrder).Select(v => v.Id).ToArray());
    }

    /// <summary>
    /// Seeds a CustomInput attribute — one the buyer types a value into rather than picking from, so it
    /// carries input settings instead of values. Returns the attribute id.
    /// </summary>
    protected Guid SeedCustomInputAttribute(
        string name,
        ProductAttributeInputType inputType = ProductAttributeInputType.Text,
        int? maxLength = null,
        bool required = false,
        int displayOrder = 0)
    {
        using var db = NewContext();
        var attribute = new ProductAttribute
        {
            Name = name,
            DisplayType = ProductAttributeDisplayType.CustomInput,
            InputType = inputType,
            MaxLength = maxLength,
            IsRequired = required,
            DisplayOrder = displayOrder,
        };
        db.ProductAttributes.Add(attribute);
        db.SaveChanges();
        return attribute.Id;
    }

    /// <summary>Assigns an already-seeded attribute to a product (mirrors SetProductAttributes).</summary>
    protected void AssignAttribute(Guid productId, Guid attributeId, int displayOrder = 0)
    {
        using var db = NewContext();
        db.ProductAttributeMappings.Add(new ProductAttributeMapping
        {
            ProductId = productId,
            ProductAttributeId = attributeId,
            DisplayOrder = displayOrder,
        });
        db.SaveChanges();
    }

    /// <summary>Seeds a single variant under a product; returns the variant id.</summary>
    protected Guid SeedVariant(Guid productId, Action<ProductVariant>? configure = null)
    {
        using var db = NewContext();
        var variant = new ProductVariant { ProductId = productId, IsEnabled = true };
        configure?.Invoke(variant);
        db.ProductVariants.Add(variant);
        db.SaveChanges();
        return variant.Id;
    }

    /// <summary>Seeds a specification attribute with a single option; returns the option id.</summary>
    protected Guid SeedSpecificationOption(string attributeName = "Spec", string value = "Value")
    {
        using var db = NewContext();
        var attribute = new SpecificationAttribute { Name = attributeName };
        var option = new SpecificationAttributeOption { Value = value };
        attribute.Options.Add(option);
        db.SpecificationAttributes.Add(attribute);
        db.SaveChanges();
        return option.Id;
    }

    /// <summary>Seeds a Media asset (image by default); returns the media id.</summary>
    protected Guid SeedMedia(string url = "https://cdn/img.png", MediaType mediaType = MediaType.Image, string? altText = null)
    {
        using var db = NewContext();
        var media = new Media
        {
            OriginalFileName = "img.png",
            SeoFileName = "img-" + Guid.NewGuid().ToString("n"),
            AssetKey = url,
            Url = url,
            MediaType = mediaType,
            MimeType = mediaType == MediaType.Video ? "text/html" : "image/png",
            AltText = altText,
        };
        db.Media.Add(media);
        db.SaveChanges();
        return media.Id;
    }

    /// <summary>Seeds a Media-backed picture for a product (or a variant when <paramref name="variantId"/> is set); returns the picture id.</summary>
    protected Guid SeedPicture(Guid productId, Guid? variantId = null, string url = "https://cdn/img.png", int displayOrder = 0, MediaType mediaType = MediaType.Image)
    {
        var mediaId = SeedMedia(url, mediaType);
        using var db = NewContext();
        var picture = new ProductPicture
        {
            ProductId = productId,
            ProductVariantId = variantId,
            MediaId = mediaId,
            DisplayOrder = displayOrder,
        };
        db.ProductPictures.Add(picture);
        db.SaveChanges();
        return picture.Id;
    }

    public void Dispose()
    {
        _transaction.Rollback();
        _transaction.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
