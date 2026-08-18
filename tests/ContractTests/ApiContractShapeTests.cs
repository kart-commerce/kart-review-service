using FluentAssertions;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace Kart.Review.ContractTests;

/// <summary>
/// Validates the vendored `contracts/api-contract.yaml` itself is well-formed and declares
/// exactly the 6 operations this service implements — every endpoint's actual request/response
/// shape fidelity against this contract is covered live, end-to-end, by `IntegrationTests`
/// (real HTTP calls against the real running API), which is the stronger check; this test's job
/// is to catch the contract file itself drifting out of sync with the implementation (a
/// removed/renamed operationId, a path typo) independent of whether any single test happens to
/// exercise that endpoint.
/// </summary>
public sealed class ApiContractShapeTests
{
    private static YamlMappingNode LoadContract()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "contracts", "api-contract.yaml");
        File.Exists(path).Should().BeTrue($"the contract must be vendored into the test output at {path}");

        using var reader = new StreamReader(path);
        var yaml = new YamlStream();
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    [Theory]
    [InlineData("/v1/reviews", "post", "submitReview")]
    [InlineData("/v1/reviews", "get", "listReviews")]
    [InlineData("/v1/reviews/{id}", "patch", "editReview")]
    [InlineData("/v1/reviews/{id}", "delete", "retractReview")]
    [InlineData("/v1/reviews/{id}/moderate", "patch", "moderateReview")]
    [InlineData("/v1/product-ratings/{sku}", "get", "getProductRatingSummary")]
    public void Contract_DeclaresEveryImplementedOperation(string path, string method, string expectedOperationId)
    {
        var root = LoadContract();
        var paths = (YamlMappingNode)root.Children[new YamlScalarNode("paths")];
        var pathNode = (YamlMappingNode)paths.Children[new YamlScalarNode(path)];
        var operation = (YamlMappingNode)pathNode.Children[new YamlScalarNode(method)];

        var operationId = ((YamlScalarNode)operation.Children[new YamlScalarNode("operationId")]).Value;
        operationId.Should().Be(expectedOperationId);
    }

    [Fact]
    public void Contract_ReviewViewSchema_DeclaresEveryFieldTheApiActuallyReturns()
    {
        var root = LoadContract();
        var components = (YamlMappingNode)root.Children[new YamlScalarNode("components")];
        var schemas = (YamlMappingNode)components.Children[new YamlScalarNode("schemas")];
        var reviewView = (YamlMappingNode)schemas.Children[new YamlScalarNode("ReviewView")];
        var properties = (YamlMappingNode)reviewView.Children[new YamlScalarNode("properties")];

        var expectedFields = new[]
        {
            "reviewId", "orderId", "sku", "userId", "rating", "bodyText", "status",
            "pendingRevision", "firstPublishedAt", "createdAt", "lastEditedAt", "retractedAt",
        };

        foreach (var field in expectedFields)
        {
            properties.Children.Should().ContainKey(new YamlScalarNode(field));
        }
    }

    [Fact]
    public void Contract_SubmitReview_RequiresIdempotencyKeyHeader()
    {
        var root = LoadContract();
        var paths = (YamlMappingNode)root.Children[new YamlScalarNode("paths")];
        var reviewsPath = (YamlMappingNode)paths.Children[new YamlScalarNode("/v1/reviews")];
        var post = (YamlMappingNode)reviewsPath.Children[new YamlScalarNode("post")];
        var parameters = (YamlSequenceNode)post.Children[new YamlScalarNode("parameters")];

        parameters.Children.OfType<YamlMappingNode>()
            .Select(p => ((YamlScalarNode)p.Children[new YamlScalarNode("name")]).Value)
            .Should().Contain("Idempotency-Key");
    }
}
