using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SchoolManagement.IntegrationTests;

[CollectionDefinition("ApiIntegration")]
public sealed class ApiIntegrationCollection : ICollectionFixture<ApiWebApplicationFactory>;
