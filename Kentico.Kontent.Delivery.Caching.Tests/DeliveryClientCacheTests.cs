﻿using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Kentico.Kontent.Delivery.Abstractions;
using Xunit;
using static Kentico.Kontent.Delivery.Caching.Tests.ResponseHelper;

namespace Kentico.Kontent.Delivery.Caching.Tests
{
    public class DeliveryClientCacheTests
    {
        #region GetItemTyped

        [Theory]
        [InlineData(CacheTypeEnum.Memory)]
        [InlineData(CacheTypeEnum.Distributed)]
        public async Task GetItemTypedAsync_ResponseIsCached(CacheTypeEnum cacheType)
        {
            const string codename = "codename";
            var url = $"items/{codename}";
            var item = CreateItemResponse(CreateItem(codename, "original"));
            var updatedItem = CreateItemResponse(CreateItem(codename, "updated"));

            var scenarioBuilder = new ScenarioBuilder(cacheType);

            var scenario = scenarioBuilder.WithResponse(url, item).Build();
            var firstResponse = await scenario.CachingClient.GetItemAsync<TestItem>(codename);

            scenario = scenarioBuilder.WithResponse(url, updatedItem).Build();
            var secondResponse = await scenario.CachingClient.GetItemAsync<TestItem>(codename);

            firstResponse.Should().NotBeNull();
            firstResponse.Should().BeEquivalentTo(secondResponse);
            scenario.GetRequestCount(url).Should().Be(1);
        }

        [Theory]
        [InlineData(CacheTypeEnum.Memory)]
        public async Task GetItemTypedAsync_InvalidatedByItemDependency(CacheTypeEnum cacheType)
        {
            const string codename = "codename";
            var url = $"items/{codename}";
            var item = CreateItemResponse(CreateItem(codename, "original"));
            var updatedItem = CreateItemResponse(CreateItem(codename, "updated"));

            var scenarioBuilder = new ScenarioBuilder(cacheType);

            var scenario = scenarioBuilder.WithResponse(url, item).Build();
            var firstResponse = await scenario.CachingClient.GetItemAsync<TestItem>(codename);

            scenario = scenarioBuilder.WithResponse(url, updatedItem).Build();
            scenario.InvalidateDependency(CacheHelpers.GetItemDependencyKey(codename));
            var secondResponse = await scenario.CachingClient.GetItemAsync<TestItem>(codename);

            firstResponse.Should().NotBeNull();
            secondResponse.Should().NotBeNull();
            firstResponse.Should().NotBeEquivalentTo(secondResponse);
            scenario.GetRequestCount(url).Should().Be(2);
        }

        [Theory]
        [InlineData(CacheTypeEnum.Memory)]
        [InlineData(CacheTypeEnum.Distributed)]
        public async Task GetItemTypedAsync_InvalidatedByItemKey(CacheTypeEnum cacheType)
        {
            const string codename = "codename";
            var url = $"items/{codename}";
            var item = CreateItemResponse(CreateItem(codename, "original"));
            var updatedItem = CreateItemResponse(CreateItem(codename, "updated"));

            var scenarioBuilder = new ScenarioBuilder(cacheType);

            var scenario = scenarioBuilder.WithResponse(url, item).Build();
            var firstResponse = await scenario.CachingClient.GetItemAsync<TestItem>(codename);

            scenario = scenarioBuilder.WithResponse(url, updatedItem).Build();
            scenario.InvalidateDependency(CacheHelpers.GetItemTypedKey(codename, Enumerable.Empty<IQueryParameter>()));
            var secondResponse = await scenario.CachingClient.GetItemAsync<TestItem>(codename);

            firstResponse.Should().NotBeNull();
            secondResponse.Should().NotBeNull();
            firstResponse.Should().NotBeEquivalentTo(secondResponse);
            scenario.GetRequestCount(url).Should().Be(2);
        }

        [Fact]
        public async Task GetItemTypedAsync_InvalidatedByLinkedItemDependency()
        {
            const string codename = "codename";
            var url = $"items/{codename}";
            const string modularCodename = "modular_codename";
            var modularContent = new[] { (modularCodename, CreateItem(modularCodename)) };
            var item = CreateItemResponse(CreateItem(codename, "original"), modularContent);
            var updatedItem = CreateItemResponse(CreateItem(codename, "updated"), modularContent);

            var scenarioBuilder = new ScenarioBuilder();

            var scenario = scenarioBuilder.WithResponse(url, item).Build();
            var firstResponse = await scenario.CachingClient.GetItemAsync<TestItem>(codename);

            scenario = scenarioBuilder.WithResponse(url, updatedItem).Build();
            scenario.InvalidateDependency(CacheHelpers.GetItemDependencyKey(modularCodename));
            var secondResponse = await scenario.CachingClient.GetItemAsync<TestItem>(codename);

            firstResponse.Should().NotBeNull();
            secondResponse.Should().NotBeNull();
            firstResponse.Should().NotBeEquivalentTo(secondResponse);
            scenario.GetRequestCount(url).Should().Be(2);
        }

        [Fact]
        public async Task GetItemTypedAsync_NotInvalidatedByComponentDependency()
        {
            const string codename = "codename";
            var url = $"items/{codename}";
            var component = CreateComponent();
            var modularContent = new[] { component };
            var item = CreateItemResponse(CreateItem(codename, "original"), modularContent);
            var updatedItem = CreateItemResponse(CreateItem(codename, "updated"), modularContent);

            var scenarioBuilder = new ScenarioBuilder();

            var scenario = scenarioBuilder.WithResponse(url, item).Build();
            var firstResponse = await scenario.CachingClient.GetItemAsync<TestItem>(codename);

            scenario = scenarioBuilder.WithResponse(url, updatedItem).Build();
            scenario.InvalidateDependency(CacheHelpers.GetItemDependencyKey(component.codename));
            var secondResponse = await scenario.CachingClient.GetItemAsync<TestItem>(codename);

            firstResponse.Should().NotBeNull();
            firstResponse.Should().BeEquivalentTo(secondResponse);
            scenario.GetRequestCount(url).Should().Be(1);
        }

        [Fact]
        public async Task GetItemTypedAsync_TooManyItems_InvalidatedByItemsDependency()
        {
            const string codename = "codename";
            var url = $"items/{codename}";
            var modularContent = Enumerable.Range(1, 51).Select(i => $"modular_{i}").Select(cn => (cn, CreateItem(cn))).ToList();
            var item = CreateItemResponse(CreateItem(codename, "original"), modularContent);
            var updatedItem = CreateItemResponse(CreateItem(codename, "updated"), modularContent);

            var scenarioBuilder = new ScenarioBuilder();

            var scenario = scenarioBuilder.WithResponse(url, item).Build();
            var firstResponse = await scenario.CachingClient.GetItemAsync<TestItem>(codename);

            scenario = scenarioBuilder.WithResponse(url, updatedItem).Build();
            scenario.InvalidateDependency(CacheHelpers.GetItemsDependencyKey());
            var secondResponse = await scenario.CachingClient.GetItemAsync<TestItem>(codename);

            firstResponse.Should().NotBeNull();
            secondResponse.Should().NotBeNull();
            firstResponse.Should().NotBeEquivalentTo(secondResponse);
            scenario.GetRequestCount(url).Should().Be(2);
        }

        #endregion

        #region GetItemsTyped

        [Theory]
        [InlineData(CacheTypeEnum.Memory)]
        [InlineData(CacheTypeEnum.Distributed)]
        public async Task GetItemsTypedAsync_ResponseIsCached(CacheTypeEnum cacheType)
        {
            var url = "items";
            var itemB = CreateItem("b", "original");
            var items = CreateItemsResponse(new[] { CreateItem("a", "original"), itemB });
            var updatedItems = CreateItemsResponse(new[] { CreateItem("a", "updated"), itemB });

            var scenarioBuilder = new ScenarioBuilder(cacheType);

            var scenario = scenarioBuilder.WithResponse(url, items).Build();
            var firstResponse = await scenario.CachingClient.GetItemsAsync<TestItem>();

            scenario = scenarioBuilder.WithResponse(url, updatedItems).Build();
            var secondResponse = await scenario.CachingClient.GetItemsAsync<TestItem>();

            firstResponse.Should().NotBeNull();
            firstResponse.Should().BeEquivalentTo(secondResponse);
            scenario.GetRequestCount(url).Should().Be(1);
        }

        [Theory]
        [InlineData(CacheTypeEnum.Memory)]
        [InlineData(CacheTypeEnum.Distributed)]
        public async Task GetItemsTypedAsync_InvalidatedByItemsKey(CacheTypeEnum cacheType)
        {
            var url = "items";
            var itemB = CreateItem("b", "original");
            var items = CreateItemsResponse(new[] { CreateItem("a", "original"), itemB });
            var updatedItems = CreateItemsResponse(new[] { CreateItem("a", "updated"), itemB });

            var scenarioBuilder = new ScenarioBuilder(cacheType);

            var scenario = scenarioBuilder.WithResponse(url, items).Build();
            var firstResponse = await scenario.CachingClient.GetItemsAsync<TestItem>();

            scenario = scenarioBuilder.WithResponse(url, updatedItems).Build();
            scenario.InvalidateDependency(CacheHelpers.GetItemsTypedKey(Enumerable.Empty<IQueryParameter>()));
            var secondResponse = await scenario.CachingClient.GetItemsAsync<TestItem>();

            firstResponse.Should().NotBeNull();
            secondResponse.Should().NotBeNull();
            firstResponse.Should().NotBeEquivalentTo(secondResponse);
            scenario.GetRequestCount(url).Should().Be(2);
        }

        [Fact]
        public async Task GetItemsTypedAsync_InvalidatedByItemsDependency()
        {
            var url = "items";
            var itemB = CreateItem("b", "original");
            var items = CreateItemsResponse(new[] { CreateItem("a", "original"), itemB });
            var updatedItems = CreateItemsResponse(new[] { CreateItem("a", "updated"), itemB });

            var scenarioBuilder = new ScenarioBuilder();

            var scenario = scenarioBuilder.WithResponse(url, items).Build();
            var firstResponse = await scenario.CachingClient.GetItemsAsync<TestItem>();

            scenario = scenarioBuilder.WithResponse(url, updatedItems).Build();
            scenario.InvalidateDependency(CacheHelpers.GetItemsDependencyKey());
            var secondResponse = await scenario.CachingClient.GetItemsAsync<TestItem>();

            firstResponse.Should().NotBeNull();
            secondResponse.Should().NotBeNull();
            firstResponse.Should().NotBeEquivalentTo(secondResponse);
            scenario.GetRequestCount(url).Should().Be(2);
        }

        #endregion

        #region GetType

        [Theory]
        [InlineData(CacheTypeEnum.Memory)]
        [InlineData(CacheTypeEnum.Distributed)]
        public async Task GetTypeAsync_ResponseIsCached(CacheTypeEnum cacheType)
        {
            const string codename = "codename";
            var url = $"types/{codename}";
            var type = CreateType(codename, "Original");
            var updatedType = CreateType(codename, "Updated");

            var scenarioBuilder = new ScenarioBuilder(cacheType);

            var scenario = scenarioBuilder.WithResponse(url, type).Build();
            var firstResponse = await scenario.CachingClient.GetTypeAsync(codename, true);

            scenario = scenarioBuilder.WithResponse(url, updatedType).Build();
            var secondResponse = await scenario.CachingClient.GetTypeAsync(codename, true);

            firstResponse.Should().NotBeNull();
            firstResponse.Should().BeEquivalentTo(secondResponse, o => o.DateTimesBsonCorrection());
            scenario.GetRequestCount(url).Should().Be(1);
        }

        [Fact]
        public async Task GetTypeAsync_InvalidatedByTypesDependency()
        {
            const string codename = "codename";
            var url = $"types/{codename}";
            var type = CreateType(codename, "Original");
            var updatedType = CreateType(codename, "Updated");

            var scenarioBuilder = new ScenarioBuilder();

            var scenario = scenarioBuilder.WithResponse(url, type).Build();
            var firstResponse = await scenario.CachingClient.GetTypeAsync(codename, true);

            scenario = scenarioBuilder.WithResponse(url, updatedType).Build();
            scenario.InvalidateDependency(CacheHelpers.GetTypesDependencyKey());
            var secondResponse = await scenario.CachingClient.GetTypeAsync(codename, true);

            firstResponse.Should().NotBeNull();
            secondResponse.Should().NotBeNull();
            firstResponse.Should().NotBeEquivalentTo(secondResponse);
            scenario.GetRequestCount(url).Should().Be(2);
        }

        #endregion

        #region GetTypes

        [Theory]
        [InlineData(CacheTypeEnum.Memory)]
        [InlineData(CacheTypeEnum.Distributed)]
        public async Task GetTypesAsync_ResponseIsCached(CacheTypeEnum cacheType)
        {
            var url = "types";
            var typeA = CreateType("a");
            var types = CreateTypesResponse(new[] { typeA });
            var updatedTypes = CreateTypesResponse(new[] { typeA, CreateType("b") });

            var scenarioBuilder = new ScenarioBuilder(cacheType);

            var scenario = scenarioBuilder.WithResponse(url, types).Build();
            var firstResponse = await scenario.CachingClient.GetTypesAsync();

            scenario = scenarioBuilder.WithResponse(url, updatedTypes).Build();
            var secondResponse = await scenario.CachingClient.GetTypesAsync();

            firstResponse.Should().NotBeNull();
            firstResponse.Should().BeEquivalentTo(secondResponse, o => o.DateTimesBsonCorrection());
            scenario.GetRequestCount(url).Should().Be(1);
        }

        [Fact]
        public async Task GetTypesAsync_InvalidatedByTypesDependency()
        {
            var url = "types";
            var typeA = CreateType("a");
            var types = CreateTypesResponse(new[] { typeA });
            var updatedTypes = CreateTypesResponse(new[] { typeA, CreateType("b") });

            var scenarioBuilder = new ScenarioBuilder();

            var scenario = scenarioBuilder.WithResponse(url, types).Build();
            var firstResponse = await scenario.CachingClient.GetTypesAsync();

            scenario = scenarioBuilder.WithResponse(url, updatedTypes).Build();
            scenario.InvalidateDependency(CacheHelpers.GetTypesDependencyKey());
            var secondResponse = await scenario.CachingClient.GetTypesAsync();

            firstResponse.Should().NotBeNull();
            secondResponse.Should().NotBeNull();
            firstResponse.Should().NotBeEquivalentTo(secondResponse);
            scenario.GetRequestCount(url).Should().Be(2);
        }

        #endregion

        #region GetContentElement

        [Theory]
        [InlineData(CacheTypeEnum.Memory)]
        [InlineData(CacheTypeEnum.Distributed)]
        public async Task GetContentElementAsync_ResponseIsCached(CacheTypeEnum cacheType)
        {
            const string typeCodename = "type";
            const string elementCodename = "element";
            var url = $"types/{typeCodename}/elements/{elementCodename}";
            var contentElement = CreateContentElement(elementCodename, "Original");
            var updatedContentElement = CreateContentElement(elementCodename, "Updated");

            var scenarioBuilder = new ScenarioBuilder(cacheType);

            var scenario = scenarioBuilder.WithResponse(url, contentElement).Build();
            var firstResponse = await scenario.CachingClient.GetContentElementAsync(typeCodename, elementCodename);

            scenario = scenarioBuilder.WithResponse(url, updatedContentElement).Build();
            var secondResponse = await scenario.CachingClient.GetContentElementAsync(typeCodename, elementCodename);

            firstResponse.Should().NotBeNull();
            firstResponse.Should().BeEquivalentTo(secondResponse);
            scenario.GetRequestCount(url).Should().Be(1);
        }

        [Fact]
        public async Task GetContentElementAsync_InvalidatedByTypesDependency()
        {
            const string typeCodename = "type";
            const string elementCodename = "element";
            var url = $"types/{typeCodename}/elements/{elementCodename}";
            var contentElement = CreateContentElement(elementCodename, "Original");
            var updatedContentElement = CreateContentElement(elementCodename, "Updated");

            var scenarioBuilder = new ScenarioBuilder();

            var scenario = scenarioBuilder.WithResponse(url, contentElement).Build();
            var firstResponse = await scenario.CachingClient.GetContentElementAsync(typeCodename, elementCodename);

            scenario = scenarioBuilder.WithResponse(url, updatedContentElement).Build();
            scenario.InvalidateDependency(CacheHelpers.GetTypesDependencyKey());
            var secondResponse = await scenario.CachingClient.GetContentElementAsync(typeCodename, elementCodename);

            firstResponse.Should().NotBeNull();
            secondResponse.Should().NotBeNull();
            firstResponse.Should().NotBeEquivalentTo(secondResponse);
            scenario.GetRequestCount(url).Should().Be(2);
        }

        #endregion

        #region GetTaxonomy

        [Theory]
        [InlineData(CacheTypeEnum.Memory)]
        [InlineData(CacheTypeEnum.Distributed)]
        public async Task GetTaxonomyAsync_ResponseIsCached(CacheTypeEnum cacheType)
        {
            const string codename = "codename";
            var url = $"taxonomies/{codename}";
            var taxonomy = CreateTaxonomy(codename, new[] { "term1" });
            var updatedTaxonomy = CreateTaxonomy(codename, new[] { "term1", "term2" });

            var scenarioBuilder = new ScenarioBuilder(cacheType);

            var scenario = scenarioBuilder.WithResponse(url, taxonomy).Build();
            var firstResponse = await scenario.CachingClient.GetTaxonomyAsync(codename);

            scenario = scenarioBuilder.WithResponse(url, updatedTaxonomy).Build();
            var secondResponse = await scenario.CachingClient.GetTaxonomyAsync(codename);

            firstResponse.Should().NotBeNull();
            firstResponse.Should().BeEquivalentTo(secondResponse, o => o.DateTimesBsonCorrection());
            scenario.GetRequestCount(url).Should().Be(1);
        }

        [Fact]
        public async Task GetTaxonomyAsync_InvalidatedByTaxonomyDependency()
        {
            const string codename = "codename";
            var url = $"taxonomies/{codename}";
            var taxonomy = CreateTaxonomy(codename, new[] { "term1" });
            var updatedTaxonomy = CreateTaxonomy(codename, new[] { "term1", "term2" });

            var scenarioBuilder = new ScenarioBuilder();

            var scenario = scenarioBuilder.WithResponse(url, taxonomy).Build();
            var firstResponse = await scenario.CachingClient.GetTaxonomyAsync(codename);

            scenario = scenarioBuilder.WithResponse(url, updatedTaxonomy).Build();
            scenario.InvalidateDependency(CacheHelpers.GetTaxonomyDependencyKey(codename));
            var secondResponse = await scenario.CachingClient.GetTaxonomyAsync(codename);

            firstResponse.Should().NotBeNull();
            secondResponse.Should().NotBeNull();
            firstResponse.Should().NotBeEquivalentTo(secondResponse);
            scenario.GetRequestCount(url).Should().Be(2);
        }

        [Theory]
        [InlineData(CacheTypeEnum.Memory)]
        [InlineData(CacheTypeEnum.Distributed)]
        public async Task GetTaxonomyAsync_InvalidatedByTaxonomyKey(CacheTypeEnum cacheType)
        {
            const string codename = "codename";
            var url = $"taxonomies/{codename}";
            var taxonomy = CreateTaxonomy(codename, new[] { "term1" });
            var updatedTaxonomy = CreateTaxonomy(codename, new[] { "term1", "term2" });

            var scenarioBuilder = new ScenarioBuilder(cacheType);

            var scenario = scenarioBuilder.WithResponse(url, taxonomy).Build();
            var firstResponse = await scenario.CachingClient.GetTaxonomyAsync(codename);

            scenario = scenarioBuilder.WithResponse(url, updatedTaxonomy).Build();
            scenario.InvalidateDependency(CacheHelpers.GetTaxonomyKey(codename));
            var secondResponse = await scenario.CachingClient.GetTaxonomyAsync(codename);

            firstResponse.Should().NotBeNull();
            secondResponse.Should().NotBeNull();
            firstResponse.Should().NotBeEquivalentTo(secondResponse);
            scenario.GetRequestCount(url).Should().Be(2);
        }

        #endregion

        #region GetTaxonomies

        [Theory]
        [InlineData(CacheTypeEnum.Memory)]
        [InlineData(CacheTypeEnum.Distributed)]
        public async Task GetTaxonomiesAsync_ResponseIsCached(CacheTypeEnum cacheType)
        {
            var url = "taxonomies";
            var taxonomyA = CreateTaxonomy("a", new[] { "term1" });
            var taxonomies = CreateTaxonomiesResponse(new[] { taxonomyA });
            var updatedTaxonomies = CreateTaxonomiesResponse(new[] { taxonomyA, CreateTaxonomy("b", new[] { "term3" }) });

            var scenarioBuilder = new ScenarioBuilder(cacheType);

            var scenario = scenarioBuilder.WithResponse(url, taxonomies).Build();
            var firstResponse = await scenario.CachingClient.GetTaxonomiesAsync();

            scenario = scenarioBuilder.WithResponse(url, updatedTaxonomies).Build();
            var secondResponse = await scenario.CachingClient.GetTaxonomiesAsync();

            firstResponse.Should().NotBeNull();
            firstResponse.Should().BeEquivalentTo(secondResponse, o => o.DateTimesBsonCorrection());
            scenario.GetRequestCount(url).Should().Be(1);
        }

        [Theory]
        [InlineData(CacheTypeEnum.Memory)]
        [InlineData(CacheTypeEnum.Distributed)]
        public async Task GetTaxonomiesAsync_InvalidatedByTaxonomiesKey(CacheTypeEnum cacheType)
        {
            var url = "taxonomies";
            var taxonomyA = CreateTaxonomy("a", new[] { "term1" });
            var taxonomies = CreateTaxonomiesResponse(new[] { taxonomyA });
            var updatedTaxonomies = CreateTaxonomiesResponse(new[] { taxonomyA, CreateTaxonomy("b", new[] { "term3" }) });

            var scenarioBuilder = new ScenarioBuilder(cacheType);

            var scenario = scenarioBuilder.WithResponse(url, taxonomies).Build();
            var firstResponse = await scenario.CachingClient.GetTaxonomiesAsync();

            scenario = scenarioBuilder.WithResponse(url, updatedTaxonomies).Build();
            scenario.InvalidateDependency(CacheHelpers.GetTaxonomiesKey(null));
            var secondResponse = await scenario.CachingClient.GetTaxonomiesAsync();

            firstResponse.Should().NotBeNull();
            secondResponse.Should().NotBeNull();
            firstResponse.Should().NotBeEquivalentTo(secondResponse);
            scenario.GetRequestCount(url).Should().Be(2);
        }

        [Fact]
        public async Task GetTaxonomiesAsync_InvalidatedByTaxonomiesDependency()
        {
            var url = "taxonomies";
            var taxonomyA = CreateTaxonomy("a", new[] { "term1" });
            var taxonomies = CreateTaxonomiesResponse(new[] { taxonomyA });
            var updatedTaxonomies = CreateTaxonomiesResponse(new[] { taxonomyA, CreateTaxonomy("b", new[] { "term3" }) });

            var scenarioBuilder = new ScenarioBuilder();

            var scenario = scenarioBuilder.WithResponse(url, taxonomies).Build();
            var firstResponse = await scenario.CachingClient.GetTaxonomiesAsync();

            scenario = scenarioBuilder.WithResponse(url, updatedTaxonomies).Build();
            scenario.InvalidateDependency(CacheHelpers.GetTaxonomiesDependencyKey());
            var secondResponse = await scenario.CachingClient.GetTaxonomiesAsync();

            firstResponse.Should().NotBeNull();
            secondResponse.Should().NotBeNull();
            firstResponse.Should().NotBeEquivalentTo(secondResponse);
            scenario.GetRequestCount(url).Should().Be(2);
        }

        #endregion
    }
}
