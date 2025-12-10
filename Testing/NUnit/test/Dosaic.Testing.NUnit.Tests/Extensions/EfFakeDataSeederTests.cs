using AwesomeAssertions;
using Bogus;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Database;
using Dosaic.Plugins.Persistence.EfCore.NpgSql;
using Dosaic.Testing.NUnit.Extensions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NUnit.Framework;

namespace Dosaic.Testing.NUnit.Tests.Extensions
{
    public class EfFakeDataSeederTests
    {
        [Test]
        public async Task SeedFakeData()
        {
            await using var context = new TestDb();
            var config = EfFakeDataSeederConfig.For(context)
                .WithTotalCount<Customer>(5)
                .WithTotalCount<Product>(2)
                .WithRelationCount<Order>(x => x.Customer, 2)
                .WithRelationCount<OrderLine>(x => x.Order, 1, 10);

            var seeder = new EfFakeDataSeeder(config);
            await seeder.SeedAsync(CancellationToken.None).ConfigureAwait(false);

            var customers = await context.Set<Customer>().ToListAsync();
            var products = await context.Set<Product>().ToListAsync();
            var orders = await context.Set<Order>().Include(x => x.OrderLines).ToListAsync();

            customers.Should().HaveCount(5);
            products.Should().HaveCount(2);
            orders.Should().HaveCount(5 * 2);
            foreach (var order in orders)
            {
                foreach (var line in order.OrderLines ?? [])
                {
                    line.CustomerId.Should().Be(order.CustomerId);
                }
            }
        }

        [Test]
        public async Task SeedSimple()
        {
            await using var db = new TestDb();
            var config = EfFakeDataSeederConfig.For(db);
            var seeder = new EfFakeDataSeeder(config);
            await seeder.SeedAsync(CancellationToken.None);

            var customers = await db.Set<Customer>().ToListAsync();
            customers.Should().NotBeEmpty();

            var products = await db.Set<Product>().ToListAsync();
            products.Should().NotBeEmpty();

            var orders = await db.Set<Order>().ToListAsync();
            orders.Should().NotBeEmpty();
        }

        [Test, Explicit]
        public async Task SeedSimplePostgres()
        {
            var options = new DbContextOptionsBuilder<TestDb>()
                .UseNpgsql(new NpgsqlDataSourceBuilder("Host=localhost;Database=testdb;Username=postgres;Password=postgres").MapDbEnums<TestDb>().Build(), o => o.UseDbEnums<TestDb>())
                .Options;
            await using var db = new TestDb(options);
            await db.Database.EnsureDeletedAsync(CancellationToken.None);
            await db.Database.EnsureCreatedAsync(CancellationToken.None);
            var config = EfFakeDataSeederConfig.For(db)
                .WithTotalCount<Customer>(100);
            var seeder = new EfFakeDataSeeder(config);
            await seeder.SeedAsync(CancellationToken.None);

            var customers = await db.Set<Customer>().ToListAsync();
            customers.Should().NotBeEmpty();

            var products = await db.Set<Product>().ToListAsync();
            products.Should().NotBeEmpty();

            var orders = await db.Set<Order>().ToListAsync();
            orders.Should().NotBeEmpty();
        }
    }

    internal class TestDb : DbContext
    {
        public TestDb(DbContextOptions<TestDb> options = null) : base(options ?? new DbContextOptionsBuilder<TestDb>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var customer = modelBuilder.Entity<Customer>();
            customer.HasKey(x => x.Id);
            customer.HasMany(x => x.Orders).WithOne(x => x.Customer).HasForeignKey(x => x.CustomerId);

            var product = modelBuilder.Entity<Product>();
            product.HasKey(x => x.Id);

            var order = modelBuilder.Entity<Order>();
            order.HasKey(x => x.Id);
            order.HasMany(x => x.OrderLines).WithOne(x => x.Order).HasForeignKey(x => x.OrderId);
            order.HasOne(x => x.Customer).WithMany(x => x.Orders).HasForeignKey(x => x.CustomerId);

            var orderLine = modelBuilder.Entity<OrderLine>();
            orderLine.HasKey(x => x.Id);
            orderLine.HasOne(x => x.Order).WithMany(x => x.OrderLines).HasForeignKey(x => x.OrderId);
            orderLine.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
            orderLine.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            base.OnModelCreating(modelBuilder);
        }
    }

    # region Models

    [DbEnum("customer_state", "public")]
    internal enum CustomerState
    {
        New = 0,
        Active = 1,
        Inactive = 2
    }

    internal class Customer
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public CustomerState State { get; set; }
        public virtual ICollection<Order> Orders { get; set; }
    }

    internal class Order
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public DateTime Date { get; set; }
        public virtual ICollection<OrderLine> OrderLines { get; set; }
        public virtual Customer Customer { get; set; }
    }

    internal class Product
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
    }

    internal class OrderLine
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid ProductId { get; set; }
        public Guid CustomerId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }

        public virtual Order Order { get; set; }
        public virtual Product Product { get; set; }
        public virtual Customer Customer { get; set; }
    }
    #endregion

    #region TestDataConfig

    internal class CustomerTestDataConfig : IFakeDataSetup<Customer>
    {

        public void ConfigureRules(Faker<Customer> faker)
        {
            faker.RuleFor(x => x.Id, f => f.Random.Guid());
            faker.RuleFor(x => x.Name, f => f.Name.FullName());
            faker.RuleFor(x => x.State, f => f.PickRandom<CustomerState>());
        }
    }

    internal class ProductTestDataConfig : IFakeDataSetup<Product>
    {

        public void ConfigureRules(Faker<Product> faker)
        {
            faker.RuleFor(x => x.Id, f => f.Random.Guid());
            faker.RuleFor(x => x.Name, f => f.Commerce.Product());
            faker.RuleFor(x => x.Price, f => Math.Round(f.Random.Decimal(1, 100), 2));
        }
    }

    internal class OrderTestDataConfig : IFakeDataSetup<Order>
    {

        public void ConfigureRules(Faker<Order> faker)
        {
            faker.RuleFor(x => x.Id, f => f.Random.Guid());
            faker.RuleFor(x => x.Date, f => f.Date.Past().ToUniversalTime());
        }
    }

    internal class OrderLineTestDataConfig : IFakeDataSetup<OrderLine>
    {

        public void ConfigureRules(Faker<OrderLine> faker)
        {
            faker.RuleFor(x => x.Id, f => f.Random.Guid());
            faker.RuleFor(x => x.Quantity, f => f.Random.Int(1, 10));
            faker.RuleFor(x => x.Price, f => Math.Round(f.Random.Decimal(1, 1000), 2));
        }
    }

    #endregion
}
