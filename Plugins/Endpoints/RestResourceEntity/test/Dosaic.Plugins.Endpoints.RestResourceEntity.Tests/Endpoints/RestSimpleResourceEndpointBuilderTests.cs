using System.Diagnostics.CodeAnalysis;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using NUnit.Framework;
using Dosaic.Extensions.Abstractions;
using Dosaic.Hosting.Abstractions.Middlewares.Models;
using Dosaic.Plugins.Endpoints.RestResourceEntity.Endpoints;
using Dosaic.Testing;
using Dosaic.Testing.NUnit;
using HttpMethod = Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpMethod;

namespace Dosaic.Plugins.Endpoints.RestResourceEntity.Tests.Endpoints
{
    public class RestSimpleResourceEndpointBuilderTests
    {
        private const string Resource = "test";
        private IEndpointRouteBuilder _endpointRouteBuilder = null!;
        private GlobalResponseOptions _globalResponseOptions = null!;
        private RestSimpleResourceEndpointBuilder<TestEntity> _builder = null!;
        private ICollection<EndpointDataSource> _endpoints = null!;

        [SetUp]
        public void Setup()
        {
            _endpointRouteBuilder = Substitute.For<IEndpointRouteBuilder>();
            _endpoints = new List<EndpointDataSource>();
            _endpointRouteBuilder.DataSources.Returns(_endpoints);
            _endpointRouteBuilder.ServiceProvider.Returns(TestingDefaults.ServiceProvider());
            _globalResponseOptions = new GlobalResponseOptions();
            _builder = new RestSimpleResourceEndpointBuilder<TestEntity>(_endpointRouteBuilder, Resource,
                _globalResponseOptions);
        }

        private IEnumerable<Route> GetRoutes() =>
            _endpoints.SelectMany(x => x.Endpoints)
                .OfType<RouteEndpoint>()
                .Select(Route.FromRouteEndpoint);

        [Test]
        public void CanAddGetRoute()
        {
            _builder.ForGet(c => c.WithPolicies("test"));
            var route = GetRoutes().Single();
            route.Should().NotBeNull();
            route.HttpMethod.Should().Be(HttpMethod.Get);
            route.Path.Should().Be("/test/{id:guid}");
            route.Name.Should().Be("Get-" + Resource);
            route.NeedsAuth.Should().BeTrue();
            route.Policies.Should().Contain("DEFAULT");
            route.Policies.Should().Contain("test");
            route.Tags.Should().Contain(Resource);
            route.Responses.Should().Contain(x => x.StatusCode == 200 && x.Type == typeof(TestEntity));
            route.Responses.Should().Contain(x => x.StatusCode == 400 && x.Type == typeof(ValidationErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 401 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 403 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 404 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 500 && x.Type == typeof(ErrorResponse));
        }

        [Test]
        public void CanAddGetListRoute()
        {
            _globalResponseOptions.Add(HttpStatusCode.Processing, typeof(ErrorResponse));
            _builder.ForGetList(c => c.AllowAnonymous());
            var route = GetRoutes().Single();
            route.Should().NotBeNull();
            route.HttpMethod.Should().Be(HttpMethod.Get);
            route.Path.Should().Be("/test");
            route.Name.Should().Be("GetList-" + Resource);
            route.NeedsAuth.Should().BeFalse();
            route.Policies.Should().HaveCount(0);
            route.Tags.Should().Contain(Resource);
            route.Responses.Should().Contain(x => x.StatusCode == 102 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 200 && x.Type == typeof(PagedList<TestEntity>));
            route.Responses.Should().Contain(x => x.StatusCode == 400 && x.Type == typeof(ValidationErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 401 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 403 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 404 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 500 && x.Type == typeof(ErrorResponse));
        }

        [Test]
        public void CanAddPostRoute()
        {
            _builder.ForPost();
            var route = GetRoutes().Single();
            route.Should().NotBeNull();
            route.HttpMethod.Should().Be(HttpMethod.Post);
            route.Path.Should().Be("/test");
            route.Name.Should().Be("Create-" + Resource);
            route.NeedsAuth.Should().BeTrue();
            route.Policies.Should().Contain("DEFAULT");
            route.Tags.Should().Contain(Resource);

            route.Responses.Should().Contain(x => x.StatusCode == 201 && x.Type == typeof(TestEntity));
            route.Responses.Should().Contain(x => x.StatusCode == 400 && x.Type == typeof(ValidationErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 401 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 403 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 404 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 500 && x.Type == typeof(ErrorResponse));
        }

        [Test]
        public void CanAddPutRoute()
        {
            _builder.ForPut();
            var route = GetRoutes().Single();
            route.Should().NotBeNull();
            route.HttpMethod.Should().Be(HttpMethod.Put);
            route.Path.Should().Be("/test/{id:guid}");
            route.Name.Should().Be("Update-" + Resource);
            route.NeedsAuth.Should().BeTrue();
            route.Policies.Should().Contain("DEFAULT");
            route.Tags.Should().Contain(Resource);

            route.Responses.Should().Contain(x => x.StatusCode == 200 && x.Type == typeof(TestEntity));
            route.Responses.Should().Contain(x => x.StatusCode == 400 && x.Type == typeof(ValidationErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 401 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 403 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 404 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 500 && x.Type == typeof(ErrorResponse));
        }

        [Test]
        public void CanAddDeleteRoute()
        {
            _globalResponseOptions.Add(HttpStatusCode.Processing);
            _builder.ForDelete(c => c.WithDisplayName("deleter")
                .WithGroupName("tester")
                .WithOpenApiTags("test", "tester")
                .WithPolicies("deleter")
                .Produces<ErrorResponse>(HttpStatusCode.Accepted)
                .Produces<ErrorResponse>(HttpStatusCode.OK)
            );
            var route = GetRoutes().Single();
            route.Should().NotBeNull();
            route.HttpMethod.Should().Be(HttpMethod.Delete);
            route.Path.Should().Be("/test/{id:guid}");
            route.Name.Should().Be("Delete-" + Resource);
            route.DisplayName.Should().Be("deleter");
            route.Group.Should().Be("tester");
            route.NeedsAuth.Should().BeTrue();
            route.Policies.Should().Contain("DEFAULT");
            route.Policies.Should().Contain("deleter");
            route.Tags.Should().Contain("test");
            route.Tags.Should().Contain("tester");

            route.Responses.Should().Contain(x => x.StatusCode == 102 && x.Type == typeof(void));
            route.Responses.Should().Contain(x => x.StatusCode == 200 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 202 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 204 && x.Type == typeof(void));
            route.Responses.Should().Contain(x => x.StatusCode == 400 && x.Type == typeof(ValidationErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 401 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 403 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 404 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 500 && x.Type == typeof(ErrorResponse));
        }

        [Test]
        public void ResponsesCanBeDisabled()
        {
            _globalResponseOptions.Add(HttpStatusCode.Processing);
            _builder.ForDelete(c => c.WithDisplayName("deleter")
                .WithGroupName("tester")
                .WithOpenApiTags("test", "tester")
                .WithPolicies("deleter")
                .Produces<ErrorResponse>(HttpStatusCode.Accepted)
                .Produces<ErrorResponse>(HttpStatusCode.OK)
                .DisableDefaultResponses()
            );
            var route = GetRoutes().Single();
            route.Should().NotBeNull();
            route.HttpMethod.Should().Be(HttpMethod.Delete);
            route.Path.Should().Be("/test/{id:guid}");
            route.Name.Should().Be("Delete-" + Resource);
            route.Responses.Should().HaveCount(3 + _globalResponseOptions.DefaultResponses.Count);

            route.Responses.Should().Contain(x => x.StatusCode == 200 && x.Type == typeof(ErrorResponse));
            route.Responses.Should().Contain(x => x.StatusCode == 202 && x.Type == typeof(ErrorResponse));
        }

        [Test]
        public void ForAllRegisterEveryPossibleRoute()
        {
            _builder.ForAll(
                c => c.DisableDefaultResponses(),
                c => c.DisableDefaultResponses(),
                c => c.DisableDefaultResponses(),
                c => c.DisableDefaultResponses(),
                c => c.DisableDefaultResponses());
            var routes = GetRoutes().ToList();
            routes.Should().Contain(r => r.HttpMethod == HttpMethod.Get && r.Path == "/test");
            routes.Should().Contain(r => r.HttpMethod == HttpMethod.Post && r.Path == "/test");
            routes.Should().Contain(r => r.HttpMethod == HttpMethod.Get && r.Path == "/test/{id:guid}");
            routes.Should().Contain(r => r.HttpMethod == HttpMethod.Put && r.Path == "/test/{id:guid}");
            routes.Should().Contain(r => r.HttpMethod == HttpMethod.Delete && r.Path == "/test/{id:guid}");
        }

        [ExcludeFromCodeCoverage]
        private record Route(
            HttpMethod HttpMethod,
            string Path,
            string Name,
            string DisplayName,
            string Group,
            bool NeedsAuth,
            IList<string> Policies,
            IList<string> Tags,
            IReadOnlyList<IProducesResponseTypeMetadata> Responses)
        {
            public static Route FromRouteEndpoint(RouteEndpoint endpoint)
            {
                var httpMethodMetaData = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!;
                var httpMethod = Enum.Parse<HttpMethod>(httpMethodMetaData.HttpMethods[0], true);
                var name = endpoint.Metadata.GetMetadata<RouteNameMetadata>()!.RouteName!;
                var groupName = endpoint.Metadata.GetMetadata<EndpointGroupNameAttribute>()?.EndpointGroupName!;
                var path = endpoint.RoutePattern.RawText!;
                var policies = endpoint.Metadata.GetOrderedMetadata<AuthorizeAttribute>()
                    .Where(x => !string.IsNullOrEmpty(x.Policy)).Select(x => x.Policy!).ToList();
                var needsAuth = policies.Any();
                var tags = endpoint.Metadata.GetOrderedMetadata<TagsAttribute>().SelectMany(x => x.Tags).ToList();
                var responses = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();
                var displayName = endpoint.DisplayName!;
                return new Route(httpMethod, path, name, displayName, groupName, needsAuth, policies, tags, responses);
            }
        }
    }
}
