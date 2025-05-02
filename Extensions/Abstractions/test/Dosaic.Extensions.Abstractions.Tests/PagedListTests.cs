using FluentAssertions;
using NUnit.Framework;

namespace Dosaic.Extensions.Abstractions.Tests
{
    [TestFixture]
    public class PagedListTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void DefaultConstructorInitializesProperties()
        {
            var pagedList = new PagedList<string>();

            pagedList.Items.Should().BeNull();
            pagedList.Page.Should().BeNull();
        }

        [Test]
        public void ParameterizedConstructorSetsPropertiesCorrectly()
        {
            var items = new List<string> { "item1", "item2", "item3" };
            var totalElements = 10;
            var page = 2;
            var size = 3;

            var pagedList = new PagedList<string>(items, totalElements, page, size);

            pagedList.Items.Should().BeEquivalentTo(items);
            pagedList.Page.Size.Should().Be(size);
            pagedList.Page.Current.Should().Be(page);
            pagedList.Page.TotalElements.Should().Be(totalElements);
            pagedList.Page.TotalPages.Should().Be(4); // Ceiling of 10/3 = 4
        }

        [Test]
        public void ParameterizedConstructorWithEmptyItemsCreatesEmptyList()
        {
            var items = new List<int>();
            var totalElements = 0;
            var page = 0;
            var size = 10;

            var pagedList = new PagedList<int>(items, totalElements, page, size);

            pagedList.Items.Should().BeEmpty();
            pagedList.Page.TotalPages.Should().Be(0);
        }

        [Test]
        public void ItemsPropertyCanBeSetAfterCreation()
        {
            var pagedList = new PagedList<int>();
            var newItems = new List<int> { 1, 2, 3 };

            pagedList.Items = newItems;

            pagedList.Items.Should().BeEquivalentTo(newItems);
        }

        [Test]
        public void PagePropertyCanBeSetAfterCreation()
        {
            var pagedList = new PagedList<int>();
            var newPage = new Page(10, 2, 100, 10);

            pagedList.Page = newPage;

            pagedList.Page.Should().BeEquivalentTo(newPage);
        }
    }
}
